using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Common;
using Carbonate.Domain.Features.Boards;
using Carbonate.Domain.Features.Events;
using Carbonate.Domain.Platform;
using Carbonate.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Carbonate.IntegrationTests.Features.Boards;

//adding, editing and assigning cards on both boards (FR-19, FR-21), with descriptions cleaned on write
[Collection(DatabaseApiCollection.Name)]
public class CardEditEndpointTests(DatabaseApiFactory factory)
{
    private sealed record World(AppUser Manager, AppUser Crew, AppUser Outsider, Event Event, Board Board, TaskCard Card)
    {
        public BoardColumn First => Board.Columns.Single(c => c.Position == 0);
        public BoardColumn Doing => Board.Columns.Single(c => c.Position == 1);
        public BoardColumn Done => Board.Columns.Single(c => c.Position == 2);
    }

    //an event board with one card already on it, assigned to a crew member who is on the event
    private static async Task<World> ArrangeAsync(TestData data)
    {
        var manager = await data.UserAsync("Sarah M.");
        var crew = await data.UserAsync("Priya R.");
        var outsider = await data.UserAsync("Not On The Event");
        var ev = await data.EventAsync(manager.UserId);
        await data.AssignAsync(ev.EventId, crew.UserId);
        var board = await data.EventBoardAsync(ev.EventId);
        var card = await data.CardAsync(board.Columns.Single(c => c.Position == 0), manager.UserId, "Pull stock",
            assignees: crew.UserId);
        return new World(manager, crew, outsider, ev, board, card);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static string RowVersion(TaskCard card) => Convert.ToBase64String(card.RowVersion);

    //----------------------------------------------------------\\
    //                              CREATE
    //----------------------------------------------------------\\

    [Fact]
    public async Task A_manager_adds_a_card_to_the_bottom_of_a_column_with_its_description_cleaned()
    {
        await using var db = factory.CreateContext();
        var world = await ArrangeAsync(new TestData(db));

        var response = await factory.ClientFor(world.Manager, RoleNames.EventManager).PostAsJsonAsync(
            $"/api/boards/{world.Board.BoardId}/cards", new
            {
                subject = "Confirm the loading bay",
                description = "<p>Call <strong>security</strong> first</p><script>alert(1)</script>" +
                    "<a href=\"javascript:alert(1)\">x</a><a href=\"https://carbon.example\">map</a>",
                priority = "High",
                dueAt = "2026-12-02T06:00:00Z",
                columnId = world.First.ColumnId,
                assigneeIds = new[] { world.Crew.UserId },
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var card = await Json(response);
        var cardId = card.GetProperty("cardId").GetGuid();
        response.Headers.Location!.AbsolutePath.ShouldBe($"/api/cards/{cardId}");
        card.GetProperty("position").GetInt32().ShouldBe(1);
        card.GetProperty("status").GetString().ShouldBe("Open");
        card.GetProperty("priority").GetString().ShouldBe("High");
        card.GetProperty("dueAt").GetString().ShouldBe("2026-12-02T06:00:00Z");
        card.GetProperty("createdBy").GetProperty("fullName").GetString().ShouldBe("Sarah M.");
        card.GetProperty("assignees")[0].GetProperty("fullName").GetString().ShouldBe("Priya R.");

        var description = card.GetProperty("description").GetString()!;
        description.ShouldStartWith("<p>Call <strong>security</strong> first</p>");
        description.ShouldNotContain("<script");
        description.ShouldNotContain("javascript:");
        description.ShouldContain("rel=\"noopener noreferrer\"");

        (await db.CalendarOutbox.SingleAsync(o => o.SourceEntityId == cardId)).Operation.ShouldBe(OutboxOperation.Upsert);
        (await db.AuditEntries.SingleAsync(a => a.EntityId == cardId.ToString())).Action.ShouldBe("card.created");
    }

    [Fact]
    public async Task Without_a_column_a_card_goes_in_the_first_and_one_added_to_done_starts_done()
    {
        await using var db = factory.CreateContext();
        var world = await ArrangeAsync(new TestData(db));
        var client = factory.ClientFor(world.Manager, RoleNames.EventManager);
        var url = $"/api/boards/{world.Board.BoardId}/cards";

        var plain = await Json(await client.PostAsJsonAsync(url, new { subject = "Book ice" }));
        var done = await Json(await client.PostAsJsonAsync(url, new { subject = "Sent invoice", columnId = world.Done.ColumnId }));

        plain.GetProperty("columnId").GetGuid().ShouldBe(world.First.ColumnId);
        plain.GetProperty("priority").GetString().ShouldBe("Normal");
        (await db.CalendarOutbox.AnyAsync(o => o.SourceEntityId == plain.GetProperty("cardId").GetGuid())).ShouldBeFalse();
        done.GetProperty("status").GetString().ShouldBe("Done");
        done.GetProperty("completedAt").GetString().ShouldEndWith("Z");
    }

    [Fact]
    public async Task Casual_crew_and_accounts_cannot_add_cards_and_crew_off_the_event_cannot_find_the_board()
    {
        await using var db = factory.CreateContext();
        var data = new TestData(db);
        var world = await ArrangeAsync(data);
        var url = $"/api/boards/{world.Board.BoardId}/cards";
        var card = new { subject = "Not allowed" };

        (await factory.ClientFor(world.Crew, RoleNames.CasualCrew).PostAsJsonAsync(url, card))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await factory.ClientFor(await data.UserAsync(), RoleNames.Accounts).PostAsJsonAsync(url, card))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await factory.ClientFor(world.Outsider, RoleNames.CrewLead).PostAsJsonAsync(url, card))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task People_must_be_on_the_event_and_active_and_milestones_must_be_the_event_s_own()
    {
        await using var db = factory.CreateContext();
        var data = new TestData(db);
        var world = await ArrangeAsync(data);
        var leaver = await data.UserAsync(active: false);
        var otherEvent = await data.EventAsync(world.Manager.UserId);
        var otherMilestone = await data.MilestoneAsync(otherEvent.EventId);
        var ownMilestone = await data.MilestoneAsync(world.Event.EventId);
        var client = factory.ClientFor(world.Manager, RoleNames.EventManager);
        var url = $"/api/boards/{world.Board.BoardId}/cards";

        var offEvent = await client.PostAsJsonAsync(url, new { subject = "x", assigneeIds = new[] { world.Outsider.UserId } });
        var inactive = await client.PostAsJsonAsync(url, new { subject = "x", assigneeIds = new[] { leaver.UserId } });
        var wrongMilestone = await client.PostAsJsonAsync(url, new { subject = "x", milestoneId = otherMilestone.MilestoneId });
        var rightMilestone = await client.PostAsJsonAsync(url, new { subject = "x", milestoneId = ownMilestone.MilestoneId });

        offEvent.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        inactive.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Json(inactive)).GetProperty("errors").TryGetProperty("assigneeIds", out _).ShouldBeTrue();
        wrongMilestone.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        rightMilestone.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task A_description_still_too_long_after_cleaning_is_a_field_error()
    {
        await using var db = factory.CreateContext();
        var world = await ArrangeAsync(new TestData(db));

        var response = await factory.ClientFor(world.Manager, RoleNames.EventManager).PostAsJsonAsync(
            $"/api/boards/{world.Board.BoardId}/cards", new { subject = "Long", description = new string('a', 5001) });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Json(response)).GetProperty("errors").TryGetProperty("description", out _).ShouldBeTrue();
    }

    //----------------------------------------------------------\\
    //                              ADMIN TASKS
    //----------------------------------------------------------\\

    [Fact]
    public async Task Ops_hands_out_an_admin_task_which_always_starts_in_assigned()
    {
        await using var db = factory.CreateContext();
        var data = new TestData(db);
        var ops = await data.UserAsync("Ops Manager");
        var thabo = await data.UserAsync("Thabo N.");
        var board = await data.AdminBoardAsync();
        var client = factory.ClientFor(ops, RoleNames.OperationsManager);
        var url = $"/api/boards/{board.BoardId}/cards";

        var created = await client.PostAsJsonAsync(url, new
        {
            subject = $"Renew liquor licence {TestData.Suffix()}",
            priority = "High",
            assigneeIds = new[] { thabo.UserId },
        });
        var nobody = await client.PostAsJsonAsync(url, new { subject = "No one to do it" });
        var skipAhead = await client.PostAsJsonAsync(url, new
        {
            subject = "Straight to complete",
            columnId = board.Columns.Single(c => c.Position == 2).ColumnId,
            assigneeIds = new[] { thabo.UserId },
        });

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var card = await Json(created);
        card.GetProperty("columnId").GetGuid().ShouldBe(board.Columns.Single(c => c.Position == 0).ColumnId);
        card.GetProperty("status").GetString().ShouldBe("Assigned");
        card.GetProperty("createdBy").GetProperty("userId").GetGuid().ShouldBe(ops.UserId);
        nobody.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        skipAhead.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Only_the_director_and_ops_hand_out_admin_tasks()
    {
        await using var db = factory.CreateContext();
        var data = new TestData(db);
        var em = await data.UserAsync();
        var board = await data.AdminBoardAsync();

        var response = await factory.ClientFor(em, RoleNames.EventManager).PostAsJsonAsync(
            $"/api/boards/{board.BoardId}/cards", new { subject = "Not mine to give", assigneeIds = new[] { em.UserId } });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    //----------------------------------------------------------\\
    //                              EDIT
    //----------------------------------------------------------\\

    [Fact]
    public async Task Editing_replaces_the_fields_records_the_change_and_requeues_the_calendar()
    {
        await using var db = factory.CreateContext();
        var world = await ArrangeAsync(new TestData(db));
        var client = factory.ClientFor(world.Manager, RoleNames.EventManager);
        var url = $"/api/cards/{world.Card.CardId}";

        var edited = await client.PatchAsJsonAsync(url, new
        {
            subject = "Pull stock for the bar",
            description = "<p>Two <em>extra</em> kegs</p>",
            priority = "Critical",
            dueAt = "2026-12-01T10:00:00Z",
            rowVersion = RowVersion(world.Card),
        });

        edited.StatusCode.ShouldBe(HttpStatusCode.OK);
        var card = await Json(edited);
        card.GetProperty("subject").GetString().ShouldBe("Pull stock for the bar");
        card.GetProperty("priority").GetString().ShouldBe("Critical");
        card.GetProperty("rowVersion").GetString().ShouldNotBe(RowVersion(world.Card));
        var audit = await db.AuditEntries.SingleAsync(a => a.EntityId == world.Card.CardId.ToString());
        audit.Action.ShouldBe("card.updated");
        audit.BeforeJson.ShouldNotBeNull().ShouldContain("\"Pull stock\"");

        //clearing the date takes it off the calendar
        var cleared = await client.PatchAsJsonAsync(url, new
        {
            subject = "Pull stock for the bar",
            rowVersion = card.GetProperty("rowVersion").GetString(),
        });
        cleared.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Json(cleared)).GetProperty("dueAt").ValueKind.ShouldBe(JsonValueKind.Null);

        var queued = await db.CalendarOutbox.Where(o => o.SourceEntityId == world.Card.CardId)
            .OrderBy(o => o.EnqueuedAt).Select(o => o.Operation).ToListAsync();
        queued.ShouldBe([OutboxOperation.Upsert, OutboxOperation.Delete]);
    }

    [Fact]
    public async Task An_edit_on_a_stale_version_is_a_409()
    {
        await using var db = factory.CreateContext();
        var world = await ArrangeAsync(new TestData(db));
        var client = factory.ClientFor(world.Manager, RoleNames.EventManager);
        var url = $"/api/cards/{world.Card.CardId}";
        await client.PatchAsJsonAsync(url, new { subject = "First edit", rowVersion = RowVersion(world.Card) });

        var stale = await client.PatchAsJsonAsync(url, new { subject = "Second edit", rowVersion = RowVersion(world.Card) });

        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Json(stale)).GetProperty("current").GetProperty("subject").GetString().ShouldBe("First edit");
    }

    [Fact]
    public async Task Casual_crew_edit_their_own_cards_and_accounts_edit_none()
    {
        await using var db = factory.CreateContext();
        var data = new TestData(db);
        var world = await ArrangeAsync(data);
        var url = $"/api/cards/{world.Card.CardId}";

        var own = await factory.ClientFor(world.Crew, RoleNames.CasualCrew)
            .PatchAsJsonAsync(url, new { subject = "Stock pulled", rowVersion = RowVersion(world.Card) });
        var accounts = await factory.ClientFor(await data.UserAsync(), RoleNames.Accounts)
            .PatchAsJsonAsync(url, new { subject = "Nope", rowVersion = RowVersion(world.Card) });

        own.StatusCode.ShouldBe(HttpStatusCode.OK);
        accounts.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    //----------------------------------------------------------\\
    //                              ASSIGNEES
    //----------------------------------------------------------\\

    [Fact]
    public async Task Assigning_replaces_who_is_on_the_card()
    {
        await using var db = factory.CreateContext();
        var data = new TestData(db);
        var world = await ArrangeAsync(data);
        var thabo = await data.UserAsync("Thabo N.");
        await data.AssignAsync(world.Event.EventId, thabo.UserId);

        var response = await factory.ClientFor(world.Manager, RoleNames.EventManager).PutAsJsonAsync(
            $"/api/cards/{world.Card.CardId}/assignees", new { userIds = new[] { thabo.UserId } });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var assignees = (await Json(response)).GetProperty("assignees");
        assignees.GetArrayLength().ShouldBe(1);
        assignees[0].GetProperty("fullName").GetString().ShouldBe("Thabo N.");
        (await db.TaskAssignments.Where(a => a.CardId == world.Card.CardId).Select(a => a.UserId).ToListAsync())
            .ShouldBe([thabo.UserId]);
        (await db.AuditEntries.SingleAsync(a => a.EntityId == world.Card.CardId.ToString())).Action
            .ShouldBe("card.assignees_changed");
    }

    [Fact]
    public async Task Someone_off_the_event_cannot_be_put_on_its_card()
    {
        await using var db = factory.CreateContext();
        var world = await ArrangeAsync(new TestData(db));

        var response = await factory.ClientFor(world.Manager, RoleNames.EventManager).PutAsJsonAsync(
            $"/api/cards/{world.Card.CardId}/assignees", new { userIds = new[] { world.Outsider.UserId } });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Admin_tasks_are_reassigned_by_ops_and_always_keep_someone_on_them()
    {
        await using var db = factory.CreateContext();
        var data = new TestData(db);
        var ops = await data.UserAsync();
        var em = await data.UserAsync();
        var priya = await data.UserAsync("Priya R.");
        var board = await data.AdminBoardAsync();
        var card = await data.CardAsync(board.Columns.Single(c => c.Position == 0), ops.UserId,
            $"Update PPE stock list {TestData.Suffix()}", "Assigned", ops.UserId);
        var url = $"/api/cards/{card.CardId}/assignees";

        var byOps = await factory.ClientFor(ops, RoleNames.OperationsManager)
            .PutAsJsonAsync(url, new { userIds = new[] { priya.UserId } });
        var empty = await factory.ClientFor(ops, RoleNames.OperationsManager)
            .PutAsJsonAsync(url, new { userIds = Array.Empty<Guid>() });
        var byEm = await factory.ClientFor(em, RoleNames.EventManager)
            .PutAsJsonAsync(url, new { userIds = new[] { em.UserId } });

        byOps.StatusCode.ShouldBe(HttpStatusCode.OK);
        empty.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        byEm.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
