using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Features.Boards;
using Carbonate.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Carbonate.IntegrationTests.Features.Boards;

//FR-20: the assignee hands a task in, and only the manager who handed it out completes it or returns it with notes
[Collection(DatabaseApiCollection.Name)]
public class AdminReviewEndpointTests(DatabaseApiFactory factory)
{
    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static BoardColumn Column(Board board, int position) => board.Columns.Single(c => c.Position == position);

    private static object Version(TaskCard card) => new { rowVersion = Convert.ToBase64String(card.RowVersion) };

    //----------------------------------------------------------\\
    //                              THE WHOLE JOURNEY
    //----------------------------------------------------------\\

    [Fact]
    public async Task A_task_goes_out_comes_back_with_notes_and_is_signed_off()
    {
        await using var db = factory.CreateContext();
        var data = new TestData(db);
        var ops = await data.UserAsync("Ops Manager");
        var thabo = await data.UserAsync("Thabo N.");
        var board = await data.AdminBoardAsync();
        var manager = factory.ClientFor(ops, RoleNames.OperationsManager);
        var crew = factory.ClientFor(thabo, RoleNames.CrewLead);

        var card = await Json(await manager.PostAsJsonAsync($"/api/boards/{board.BoardId}/cards", new
        {
            subject = $"Renew liquor licence {TestData.Suffix()}",
            assigneeIds = new[] { thabo.UserId },
        }));
        var cardId = card.GetProperty("cardId").GetGuid();

        async Task<JsonElement> HandIn(JsonElement current) => await Json(await crew.PostAsJsonAsync(
            $"/api/cards/{cardId}/move", new
            {
                columnId = Column(board, AdminTaskRules.NeedsReview).ColumnId,
                position = 0,
                rowVersion = current.GetProperty("rowVersion").GetString(),
            }));

        var handedIn = await HandIn(card);
        handedIn.GetProperty("status").GetString().ShouldBe("InProgressOrNeedsReview");

        var returned = await Json(await manager.PostAsJsonAsync($"/api/cards/{cardId}/return", new
        {
            reviewNotes = "Attach the signed renewal form",
            rowVersion = handedIn.GetProperty("rowVersion").GetString(),
        }));
        returned.GetProperty("status").GetString().ShouldBe("Assigned");
        returned.GetProperty("columnId").GetGuid().ShouldBe(Column(board, AdminTaskRules.Assigned).ColumnId);
        returned.GetProperty("reviewNotes").GetString().ShouldBe("Attach the signed renewal form");
        returned.GetProperty("returnedBy").GetProperty("fullName").GetString().ShouldBe("Ops Manager");
        returned.GetProperty("returnedAt").GetString().ShouldEndWith("Z");

        var handedInAgain = await HandIn(returned);
        var completed = await Json(await manager.PostAsJsonAsync($"/api/cards/{cardId}/complete", new
        {
            rowVersion = handedInAgain.GetProperty("rowVersion").GetString(),
        }));
        completed.GetProperty("status").GetString().ShouldBe("Complete");
        completed.GetProperty("columnId").GetGuid().ShouldBe(Column(board, AdminTaskRules.Complete).ColumnId);
        completed.GetProperty("completedAt").GetString().ShouldEndWith("Z");
        //the return notes stay, so the history of the task isn't lost
        completed.GetProperty("reviewNotes").GetString().ShouldBe("Attach the signed renewal form");

        var actions = await db.AuditEntries.Where(a => a.EntityId == cardId.ToString())
            .OrderBy(a => a.AuditId).Select(a => a.Action).ToListAsync();
        actions.ShouldBe(["card.created", "card.moved", "card.returned", "card.moved", "card.completed"]);
    }

    //----------------------------------------------------------\\
    //                              WHO SIGNS OFF
    //----------------------------------------------------------\\

    [Fact]
    public async Task A_different_manager_can_neither_complete_nor_return_it()
    {
        await using var db = factory.CreateContext();
        var data = new TestData(db);
        var ops = await data.UserAsync();
        var director = await data.UserAsync();
        var thabo = await data.UserAsync();
        var board = await data.AdminBoardAsync();
        var card = await data.CardAsync(Column(board, AdminTaskRules.NeedsReview), ops.UserId,
            $"Quarterly ice machine service {TestData.Suffix()}", "InProgressOrNeedsReview", thabo.UserId);
        var other = factory.ClientFor(director, RoleNames.Director);

        var complete = await other.PostAsJsonAsync($"/api/cards/{card.CardId}/complete", Version(card));
        var giveBack = await other.PostAsJsonAsync($"/api/cards/{card.CardId}/return",
            new { reviewNotes = "Not mine to judge", rowVersion = Convert.ToBase64String(card.RowVersion) });

        complete.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Json(complete)).GetProperty("detail").GetString().ShouldBe(
            "Only the manager who handed out this task can complete or return it.");
        giveBack.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_assignee_cannot_sign_off_their_own_task_even_with_a_manager_role()
    {
        await using var db = factory.CreateContext();
        var data = new TestData(db);
        var ops = await data.UserAsync();
        var board = await data.AdminBoardAsync();
        //ops handed the task to themselves, so they're both creator and assignee
        var card = await data.CardAsync(Column(board, AdminTaskRules.NeedsReview), ops.UserId,
            $"Health and safety file {TestData.Suffix()}", "InProgressOrNeedsReview", ops.UserId);

        var response = await factory.ClientFor(ops, RoleNames.OperationsManager)
            .PostAsJsonAsync($"/api/cards/{card.CardId}/complete", Version(card));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Roles_without_admin_task_review_are_turned_away()
    {
        await using var db = factory.CreateContext();
        var data = new TestData(db);
        var em = await data.UserAsync();
        var board = await data.AdminBoardAsync();
        var card = await data.CardAsync(Column(board, AdminTaskRules.NeedsReview), em.UserId,
            $"Update PPE stock list {TestData.Suffix()}", "InProgressOrNeedsReview", em.UserId);

        var response = await factory.ClientFor(em, RoleNames.EventManager)
            .PostAsJsonAsync($"/api/cards/{card.CardId}/complete", Version(card));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    //----------------------------------------------------------\\
    //                              WHEN
    //----------------------------------------------------------\\

    [Fact]
    public async Task A_task_not_yet_handed_in_cannot_be_completed()
    {
        await using var db = factory.CreateContext();
        var data = new TestData(db);
        var ops = await data.UserAsync();
        var thabo = await data.UserAsync();
        var board = await data.AdminBoardAsync();
        var card = await data.CardAsync(Column(board, AdminTaskRules.Assigned), ops.UserId,
            $"Renew licence {TestData.Suffix()}", "Assigned", thabo.UserId);

        var response = await factory.ClientFor(ops, RoleNames.OperationsManager)
            .PostAsJsonAsync($"/api/cards/{card.CardId}/complete", Version(card));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Json(response)).GetProperty("type").GetString().ShouldBe("/problems/invalid-transition");
    }

    [Fact]
    public async Task Returning_without_notes_is_a_field_error()
    {
        await using var db = factory.CreateContext();
        var data = new TestData(db);
        var ops = await data.UserAsync();
        var board = await data.AdminBoardAsync();
        var card = await data.CardAsync(Column(board, AdminTaskRules.NeedsReview), ops.UserId,
            $"Task {TestData.Suffix()}", "InProgressOrNeedsReview", (await data.UserAsync()).UserId);

        var response = await factory.ClientFor(ops, RoleNames.OperationsManager)
            .PostAsJsonAsync($"/api/cards/{card.CardId}/return", Version(card));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Json(response)).GetProperty("errors").TryGetProperty("reviewNotes", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task A_stale_version_cannot_sign_a_task_off()
    {
        await using var db = factory.CreateContext();
        var data = new TestData(db);
        var ops = await data.UserAsync();
        var board = await data.AdminBoardAsync();
        var card = await data.CardAsync(Column(board, AdminTaskRules.NeedsReview), ops.UserId,
            $"Task {TestData.Suffix()}", "InProgressOrNeedsReview", (await data.UserAsync()).UserId);

        var response = await factory.ClientFor(ops, RoleNames.OperationsManager)
            .PostAsJsonAsync($"/api/cards/{card.CardId}/complete", new { rowVersion = Convert.ToBase64String(new byte[8]) });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Json(response)).GetProperty("type").GetString().ShouldBe("/problems/concurrency-conflict");
    }

    //----------------------------------------------------------\\
    //                              DRAGS
    //----------------------------------------------------------\\

    [Fact]
    public async Task On_the_admin_board_a_drag_only_reorders_or_hands_in()
    {
        await using var db = factory.CreateContext();
        var data = new TestData(db);
        var ops = await data.UserAsync();
        var thabo = await data.UserAsync();
        var board = await data.AdminBoardAsync();
        var waiting = await data.CardAsync(Column(board, AdminTaskRules.Assigned), ops.UserId,
            $"Waiting {TestData.Suffix()}", "Assigned", thabo.UserId);
        var inReview = await data.CardAsync(Column(board, AdminTaskRules.NeedsReview), ops.UserId,
            $"In review {TestData.Suffix()}", "InProgressOrNeedsReview", thabo.UserId);
        var crew = factory.ClientFor(thabo, RoleNames.CrewLead);
        var accounts = factory.ClientFor(await data.UserAsync(), RoleNames.Accounts);

        object To(int position, TaskCard card, int index = 0) => new
        {
            columnId = Column(board, position).ColumnId,
            position = index,
            rowVersion = Convert.ToBase64String(card.RowVersion),
        };

        //skipping the manager's sign-off
        (await crew.PostAsJsonAsync($"/api/cards/{inReview.CardId}/move", To(AdminTaskRules.Complete, inReview)))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
        //handing in someone else's task
        (await accounts.PostAsJsonAsync($"/api/cards/{waiting.CardId}/move", To(AdminTaskRules.NeedsReview, waiting)))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        //reordering their own task within its column is fine
        (await crew.PostAsJsonAsync($"/api/cards/{waiting.CardId}/move", To(AdminTaskRules.Assigned, waiting, 0)))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Event_cards_are_finished_by_moving_them_not_by_complete()
    {
        await using var db = factory.CreateContext();
        var data = new TestData(db);
        var ops = await data.UserAsync();
        var ev = await data.EventAsync(ops.UserId);
        var board = await data.EventBoardAsync(ev.EventId);
        var card = await data.CardAsync(board.Columns.Single(c => c.Position == 0), ops.UserId, "Book ice");

        var response = await factory.ClientFor(ops, RoleNames.OperationsManager)
            .PostAsJsonAsync($"/api/cards/{card.CardId}/complete", Version(card));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }
}
