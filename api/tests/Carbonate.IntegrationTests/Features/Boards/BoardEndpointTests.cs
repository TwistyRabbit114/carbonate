using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Features.Boards;
using Carbonate.Domain.Features.Events;
using Carbonate.Domain.Platform;
using Carbonate.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Carbonate.IntegrationTests.Features.Boards;

//event task boards and the admin board: who sees which cards (FR-19, FR-21) and how moves land (NFR-07, NFR-15)
[Collection(DatabaseApiCollection.Name)]
public class BoardEndpointTests(DatabaseApiFactory factory)
{
    //an event with a three-column board: A, B, C to do (A for the first crew member, B for the second) and D done
    private sealed record EventWorld(
        AppUser Manager, AppUser Crew, AppUser OtherCrew, Event Event, Board Board,
        TaskCard A, TaskCard B, TaskCard C, TaskCard D)
    {
        public BoardColumn ToDo => Board.Columns.Single(c => c.Position == 0);
        public BoardColumn Done => Board.Columns.Single(c => c.Position == 2);
    }

    private async Task<EventWorld> ArrangeEventAsync(TestData data)
    {
        var manager = await data.UserAsync("Sarah M.");
        var crew = await data.UserAsync("Priya R.");
        var otherCrew = await data.UserAsync("Thabo N.");
        var ev = await data.EventAsync(manager.UserId);
        await data.AssignAsync(ev.EventId, crew.UserId);
        await data.AssignAsync(ev.EventId, otherCrew.UserId);

        var board = await data.EventBoardAsync(ev.EventId);
        var toDo = board.Columns.Single(c => c.Position == 0);
        var done = board.Columns.Single(c => c.Position == 2);
        var a = await data.CardAsync(toDo, manager.UserId, "Confirm access route", assignees: crew.UserId);
        var b = await data.CardAsync(toDo, manager.UserId, "Pull stock", assignees: otherCrew.UserId);
        var c = await data.CardAsync(toDo, manager.UserId, "Book ice");
        var d = await data.CardAsync(done, manager.UserId, "Send crew list", status: "Done");

        return new EventWorld(manager, crew, otherCrew, ev, board, a, b, c, d);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static string[] Subjects(JsonElement column) =>
        [.. column.GetProperty("cards").EnumerateArray().Select(card => card.GetProperty("subject").GetString()!)];

    private static object Move(BoardColumn to, int position, byte[] rowVersion) =>
        new { columnId = to.ColumnId, position, rowVersion = Convert.ToBase64String(rowVersion) };

    //----------------------------------------------------------\\
    //                              EVENT BOARDS
    //----------------------------------------------------------\\

    [Fact]
    public async Task Desk_roles_see_the_whole_event_board_in_column_order()
    {
        await using var db = factory.CreateContext();
        var world = await ArrangeEventAsync(new TestData(db));

        var response = await factory.ClientFor(world.Manager, RoleNames.EventManager)
            .GetAsync($"/api/events/{world.Event.EventId}/board");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var board = await Json(response);
        board.GetProperty("boardType").GetString().ShouldBe("Event");
        var columns = board.GetProperty("columns");
        columns.EnumerateArray().Select(c => c.GetProperty("name").GetString()).ShouldBe(["To do", "Doing", "Done"]);
        columns[0].GetProperty("wipLimit").GetInt32().ShouldBe(5);
        Subjects(columns[0]).ShouldBe(["Confirm access route", "Pull stock", "Book ice"]);
        Subjects(columns[2]).ShouldBe(["Send crew list"]);

        var card = columns[0].GetProperty("cards")[0];
        card.GetProperty("priority").GetString().ShouldBe("Normal");
        card.GetProperty("dueAt").GetString().ShouldBe("2026-12-01T08:00:00Z");
        card.GetProperty("assignees")[0].GetProperty("fullName").GetString().ShouldBe("Priya R.");
        card.GetProperty("createdBy").GetProperty("fullName").GetString().ShouldBe("Sarah M.");
        card.GetProperty("rowVersion").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Crew_see_only_the_cards_assigned_to_them()
    {
        await using var db = factory.CreateContext();
        var world = await ArrangeEventAsync(new TestData(db));

        var board = await Json(await factory.ClientFor(world.Crew, RoleNames.CasualCrew)
            .GetAsync($"/api/events/{world.Event.EventId}/board"));

        var columns = board.GetProperty("columns");
        columns.GetArrayLength().ShouldBe(3);
        Subjects(columns[0]).ShouldBe(["Confirm access route"]);
        Subjects(columns[2]).ShouldBeEmpty();
    }

    [Fact]
    public async Task Crew_not_on_the_event_get_404_and_so_does_everyone_for_a_deleted_event()
    {
        await using var db = factory.CreateContext();
        var data = new TestData(db);
        var world = await ArrangeEventAsync(data);
        var stranger = await data.UserAsync();
        var deleted = await data.EventAsync(world.Manager.UserId, active: false);
        await data.EventBoardAsync(deleted.EventId);

        (await factory.ClientFor(stranger, RoleNames.CrewLead).GetAsync($"/api/events/{world.Event.EventId}/board"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await factory.ClientFor(world.Manager, RoleNames.Director).GetAsync($"/api/events/{deleted.EventId}/board"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_event_without_a_task_board_is_404()
    {
        await using var db = factory.CreateContext();
        var data = new TestData(db);
        var manager = await data.UserAsync();
        var ev = await data.EventAsync(manager.UserId);

        (await factory.ClientFor(manager, RoleNames.EventManager).GetAsync($"/api/events/{ev.EventId}/board"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Accounts_can_read_an_event_board_but_not_move_its_cards()
    {
        await using var db = factory.CreateContext();
        var data = new TestData(db);
        var world = await ArrangeEventAsync(data);
        var accounts = factory.ClientFor(await data.UserAsync(), RoleNames.Accounts);

        var board = await Json(await accounts.GetAsync($"/api/events/{world.Event.EventId}/board"));
        var move = await accounts.PostAsJsonAsync($"/api/cards/{world.C.CardId}/move",
            Move(world.Done, 0, world.C.RowVersion));

        Subjects(board.GetProperty("columns")[0]).Length.ShouldBe(3);
        move.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_single_card_read_follows_the_same_rules()
    {
        await using var db = factory.CreateContext();
        var world = await ArrangeEventAsync(new TestData(db));
        var crew = factory.ClientFor(world.Crew, RoleNames.CasualCrew);

        (await crew.GetAsync($"/api/cards/{world.A.CardId}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await crew.GetAsync($"/api/cards/{world.B.CardId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await crew.GetAsync($"/api/cards/{Guid.NewGuid()}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    //----------------------------------------------------------\\
    //                              MOVES
    //----------------------------------------------------------\\

    [Fact]
    public async Task Moving_into_the_done_column_marks_the_card_done_and_renumbers_both_columns()
    {
        await using var db = factory.CreateContext();
        var world = await ArrangeEventAsync(new TestData(db));
        var client = factory.ClientFor(world.Manager, RoleNames.EventManager);

        var response = await client.PostAsJsonAsync($"/api/cards/{world.B.CardId}/move",
            Move(world.Done, 0, world.B.RowVersion));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var moved = await Json(response);
        moved.GetProperty("columnId").GetGuid().ShouldBe(world.Done.ColumnId);
        moved.GetProperty("position").GetInt32().ShouldBe(0);
        moved.GetProperty("status").GetString().ShouldBe("Done");
        moved.GetProperty("completedAt").GetString().ShouldEndWith("Z");
        moved.GetProperty("rowVersion").GetString().ShouldNotBe(Convert.ToBase64String(world.B.RowVersion));

        var positions = await db.TaskCards.AsNoTracking()
            .Where(c => c.ColumnId == world.ToDo.ColumnId || c.ColumnId == world.Done.ColumnId)
            .OrderBy(c => c.Position)
            .ToListAsync();
        positions.Where(c => c.ColumnId == world.ToDo.ColumnId).Select(c => c.Subject).ShouldBe(["Confirm access route", "Book ice"]);
        positions.Where(c => c.ColumnId == world.Done.ColumnId).Select(c => c.Subject).ShouldBe(["Pull stock", "Send crew list"]);
        (await db.AuditEntries.SingleAsync(a => a.EntityId == world.B.CardId.ToString())).Action.ShouldBe("card.moved");

        //and straight back out again, which reopens it
        var back = await Json(await client.PostAsJsonAsync($"/api/cards/{world.B.CardId}/move",
            new { columnId = world.ToDo.ColumnId, position = 1, rowVersion = moved.GetProperty("rowVersion").GetString() }));
        back.GetProperty("status").GetString().ShouldBe("Open");
        back.GetProperty("completedAt").ValueKind.ShouldBe(JsonValueKind.Null);
        back.GetProperty("position").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task A_stale_row_version_is_a_409_with_the_card_as_it_is_now()
    {
        await using var db = factory.CreateContext();
        var world = await ArrangeEventAsync(new TestData(db));
        var client = factory.ClientFor(world.Manager, RoleNames.EventManager);
        var first = await Json(await client.PostAsJsonAsync($"/api/cards/{world.C.CardId}/move",
            Move(world.Done, 0, world.C.RowVersion)));

        //a second person still holding the old version
        var stale = await client.PostAsJsonAsync($"/api/cards/{world.C.CardId}/move",
            Move(world.ToDo, 0, world.C.RowVersion));

        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await Json(stale);
        problem.GetProperty("type").GetString().ShouldBe("/problems/concurrency-conflict");
        var current = problem.GetProperty("current");
        current.GetProperty("rowVersion").GetString().ShouldBe(first.GetProperty("rowVersion").GetString());
        current.GetProperty("columnId").GetGuid().ShouldBe(world.Done.ColumnId);
        current.GetProperty("priority").GetString().ShouldBe("Normal");
        (await db.TaskCards.AsNoTracking().SingleAsync(c => c.CardId == world.C.CardId)).ColumnId
            .ShouldBe(world.Done.ColumnId);
    }

    [Fact]
    public async Task A_column_from_another_board_is_422()
    {
        await using var db = factory.CreateContext();
        var data = new TestData(db);
        var world = await ArrangeEventAsync(data);
        var otherBoard = await data.EventBoardAsync((await data.EventAsync(world.Manager.UserId)).EventId);

        var response = await factory.ClientFor(world.Manager, RoleNames.EventManager).PostAsJsonAsync(
            $"/api/cards/{world.A.CardId}/move", Move(otherBoard.Columns[0], 0, world.A.RowVersion));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Casual_crew_move_their_own_cards_and_cannot_reach_anyone_else_s()
    {
        await using var db = factory.CreateContext();
        var world = await ArrangeEventAsync(new TestData(db));
        var crew = factory.ClientFor(world.Crew, RoleNames.CasualCrew);

        (await crew.PostAsJsonAsync($"/api/cards/{world.A.CardId}/move", Move(world.Done, 0, world.A.RowVersion)))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await crew.PostAsJsonAsync($"/api/cards/{world.B.CardId}/move", Move(world.Done, 0, world.B.RowVersion)))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_move_without_a_row_version_is_400()
    {
        await using var db = factory.CreateContext();
        var world = await ArrangeEventAsync(new TestData(db));

        var response = await factory.ClientFor(world.Manager, RoleNames.EventManager).PostAsJsonAsync(
            $"/api/cards/{world.A.CardId}/move", new { columnId = world.Done.ColumnId, position = 0 });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Json(response)).GetProperty("errors").TryGetProperty("rowVersion", out _).ShouldBeTrue();
    }

    //----------------------------------------------------------\\
    //                              ADMIN BOARD
    //----------------------------------------------------------\\

    [Fact]
    public async Task The_admin_board_shows_desk_roles_every_task_and_crew_only_their_own()
    {
        await using var db = factory.CreateContext();
        var data = new TestData(db);
        var ops = await data.UserAsync();
        var crewUser = await data.UserAsync();
        var board = await data.AdminBoardAsync();
        var assigned = board.Columns.Single(c => c.Position == 0);
        var tag = TestData.Suffix();
        await data.CardAsync(assigned, ops.UserId, $"Renew liquor licence {tag}", "Assigned", crewUser.UserId);
        await data.CardAsync(assigned, ops.UserId, $"Update PPE stock list {tag}", "Assigned", ops.UserId);

        var desk = await Json(await factory.ClientFor(ops, RoleNames.OperationsManager).GetAsync("/api/boards/admin"));
        var crew = await Json(await factory.ClientFor(crewUser, RoleNames.CrewLead).GetAsync("/api/boards/admin"));

        desk.GetProperty("boardType").GetString().ShouldBe("Admin");
        Subjects(desk.GetProperty("columns")[0]).Where(s => s.EndsWith(tag, StringComparison.Ordinal))
            .ShouldBe([$"Renew liquor licence {tag}", $"Update PPE stock list {tag}"]);
        Subjects(crew.GetProperty("columns")[0]).ShouldBe([$"Renew liquor licence {tag}"]);
        crew.GetProperty("columns")[0].GetProperty("cards")[0].GetProperty("status").GetString().ShouldBe("Assigned");
    }

    [Fact]
    public async Task Admin_tasks_move_through_their_own_actions_not_a_drag()
    {
        await using var db = factory.CreateContext();
        var data = new TestData(db);
        var ops = await data.UserAsync();
        var board = await data.AdminBoardAsync();
        var card = await data.CardAsync(board.Columns.Single(c => c.Position == 0), ops.UserId,
            $"Quarterly ice machine service {TestData.Suffix()}", "Assigned", ops.UserId);

        var response = await factory.ClientFor(ops, RoleNames.OperationsManager).PostAsJsonAsync(
            $"/api/cards/{card.CardId}/move", Move(board.Columns.Single(c => c.Position == 2), 0, card.RowVersion));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Json(response)).GetProperty("type").GetString().ShouldBe("/problems/invalid-transition");
    }
}
