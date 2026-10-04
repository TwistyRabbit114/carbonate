using Carbonate.Application.Common;
using Carbonate.Application.Features.Events;
using Carbonate.Application.Features.Lifecycle;
using Carbonate.Application.Features.Stock;
using Carbonate.Application.Masking;
using Carbonate.Application.Platform.Audit;
using Carbonate.Application.Platform.Auth;
using Carbonate.Domain.Common;
using Carbonate.Domain.Features.Events;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Carbonate.UnitTests.Events;

public class EventServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);

    private readonly IEventRepository _events = Substitute.For<IEventRepository>();
    private readonly IEventTemplateSeeder _seeder = Substitute.For<IEventTemplateSeeder>();
    private readonly IEventLifecycleService _lifecycle = Substitute.For<IEventLifecycleService>();
    private readonly IAuditService _audit = Substitute.For<IAuditService>();
    private readonly ICurrentUser _user = Substitute.For<ICurrentUser>();
    private readonly List<string> _steps = [];
    private readonly EventService _service;
    private readonly Guid _userId = Guid.NewGuid();

    public EventServiceTests()
    {
        _user.UserId.Returns(_userId);
        GivenRole(RoleNames.EventManager);

        _events.CheckReferencesAsync(default, default, default, default).ReturnsForAnyArgs(new EventReferences(true, true, true));
        _events.EventCodeExistsAsync(default!, default, default).ReturnsForAnyArgs(false);
        _events.GetDetailAsync(default, default, default).ReturnsForAnyArgs(new EventDetail { EventCode = "T-1" });

        _events.When(e => e.Add(Arg.Any<Event>(), Arg.Any<IEnumerable<EventMilestone>>(), Arg.Any<IEnumerable<MilestoneDependency>>()))
            .Do(_ => _steps.Add("add"));
        _events.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(_ => { _steps.Add("save"); return Task.CompletedTask; });
        _seeder.SeedAsync(default, default, default, default, default, default).ReturnsForAnyArgs(_ =>
        {
            _steps.Add("seed");
            return Task.FromResult(TemplateSeedResult.NoTemplate);
        });
        _audit.RecordAsync(default!, default!, default!, default, default, default, default).ReturnsForAnyArgs(_ =>
        {
            _steps.Add("audit");
            return Task.CompletedTask;
        });

        _service = new EventService(_events, _seeder, _lifecycle, new PassThroughTransaction(), _audit,
            new FinancialMasker(), _user, new FixedTime(Now));
    }

    [Fact]
    public async Task Creating_saves_the_event_before_seeding_because_the_seeder_reads_the_milestones()
    {
        await _service.CreateAsync(NewRequest(), default);

        _steps.ShouldBe(["add", "save", "seed", "save", "audit"]);
    }

    [Fact]
    public async Task The_seeder_is_told_who_is_creating_the_event_and_the_estimated_pack_size()
    {
        var request = NewRequest();
        request.PackSizeEstimated = 240;
        request.EventType = EventType.Wedding;

        await _service.CreateAsync(request, default);

        await _seeder.Received(1).SeedAsync(
            Arg.Any<Guid>(), EventType.Wedding, request.DivisionId, 240, _userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_event_starts_in_Enquired_with_the_nine_milestone_chain()
    {
        Event? saved = null;
        List<EventMilestone>? chain = null;
        _events.When(e => e.Add(Arg.Any<Event>(), Arg.Any<IEnumerable<EventMilestone>>(), Arg.Any<IEnumerable<MilestoneDependency>>()))
            .Do(call =>
            {
                saved = call.ArgAt<Event>(0);
                chain = [.. call.ArgAt<IEnumerable<EventMilestone>>(1)];
            });

        await _service.CreateAsync(NewRequest(), default);

        saved!.Status.ShouldBe(EventStatus.Enquired);
        saved.CreatedByUserId.ShouldBe(_userId);
        chain!.Count.ShouldBe(9);
    }

    [Fact]
    public async Task A_seeder_failure_stops_the_create_before_the_audit_entry_is_written()
    {
        _seeder.SeedAsync(default, default, default, default, default, default)
            .ThrowsAsyncForAnyArgs(new InvalidOperationException("template broke"));

        await Should.ThrowAsync<InvalidOperationException>(() => _service.CreateAsync(NewRequest(), default));

        _steps.ShouldNotContain("audit");
    }

    [Fact]
    public async Task Times_arriving_with_an_offset_are_stored_as_the_same_instant_in_utc()
    {
        Event? saved = null;
        _events.When(e => e.Add(Arg.Any<Event>(), Arg.Any<IEnumerable<EventMilestone>>(), Arg.Any<IEnumerable<MilestoneDependency>>()))
            .Do(call => saved = call.ArgAt<Event>(0));
        var request = NewRequest();
        request.StartsAt = new DateTimeOffset(2026, 11, 14, 19, 0, 0, TimeSpan.FromHours(2)).LocalDateTime;
        request.EndsAt = request.StartsAt.AddHours(5);

        await _service.CreateAsync(request, default);

        saved!.StartsAt.Kind.ShouldBe(DateTimeKind.Utc);
        saved.StartsAt.ShouldBe(new DateTime(2026, 11, 14, 17, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Every_service_method_refuses_a_caller_without_the_permission()
    {
        GivenRole(RoleNames.CasualCrew);
        var id = Guid.NewGuid();

        (await Should.ThrowAsync<ProblemException>(() => _service.CreateAsync(NewRequest(), default))).Status.ShouldBe(403);
        (await Should.ThrowAsync<ProblemException>(() => _service.DeleteAsync(id, default))).Status.ShouldBe(403);
        (await Should.ThrowAsync<ProblemException>(() => _service.AssignCrewAsync(id, new AssignCrewRequest(), default))).Status.ShouldBe(403);
        (await Should.ThrowAsync<ProblemException>(() => _service.TransitionAsync(id, new TransitionRequest(), default))).Status.ShouldBe(403);
        (await Should.ThrowAsync<ProblemException>(() => _service.RescheduleAsync(id, id, new RescheduleRequest(), default))).Status.ShouldBe(403);
        await _events.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task An_event_the_caller_may_not_see_is_not_found_not_forbidden()
    {
        _events.FindAsync(default, default, default).ReturnsForAnyArgs((Event?)null);

        var error = await Should.ThrowAsync<ProblemException>(
            () => _service.TransitionAsync(Guid.NewGuid(), new TransitionRequest { To = EventStatus.Cancelled, RowVersion = "AAAA" }, default));

        error.Status.ShouldBe(404);
        await _lifecycle.DidNotReceiveWithAnyArgs().TransitionAsync(default, default, default!, default, default);
    }

    [Fact]
    public async Task Crew_look_events_up_scoped_to_themselves_and_managers_see_all()
    {
        GivenRole(RoleNames.CasualCrew);
        await _service.GetAsync(Guid.NewGuid(), default);
        await _events.Received(1).GetDetailAsync(Arg.Any<Guid>(), _userId, Arg.Any<CancellationToken>());

        _events.ClearReceivedCalls();
        GivenRole(RoleNames.EventManager);
        await _service.GetAsync(Guid.NewGuid(), default);
        await _events.Received(1).GetDetailAsync(Arg.Any<Guid>(), null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_page_size_is_capped_at_200()
    {
        var query = new EventListQuery { Page = 0, PageSize = 5000 };
        _events.ListAsync(default!, default, default).ReturnsForAnyArgs(new PagedResult<EventListItem>());

        await _service.ListAsync(query, default);

        query.Page.ShouldBe(1);
        query.PageSize.ShouldBe(200);
    }

    [Theory]
    [InlineData(DateTimeKind.Utc, 17)]
    [InlineData(DateTimeKind.Unspecified, 17)]
    public void Utc_and_unspecified_times_keep_their_clock_value_and_become_utc(DateTimeKind kind, int hour)
    {
        var result = DateTimes.ToUtc(new DateTime(2026, 11, 14, hour, 0, 0, kind));

        result.Kind.ShouldBe(DateTimeKind.Utc);
        result.Hour.ShouldBe(hour);
    }

    private void GivenRole(string role)
    {
        _user.HasPermission(Arg.Any<string>()).Returns(call => RolePermissionMatrix.PermissionsFor(role).Contains(call.Arg<string>()));
        _user.ScopeOf(Arg.Any<string>()).Returns(call => RolePermissionMatrix.ScopeFor(role, call.Arg<string>()));
    }

    private SaveEventRequest NewRequest() => new()
    {
        EventCode = "TEST-26",
        ClientId = Guid.NewGuid(),
        VenueId = Guid.NewGuid(),
        DivisionId = Guid.NewGuid(),
        Name = "Test",
        EventType = EventType.Corporate,
        EventDate = new DateOnly(2026, 11, 14),
        StartsAt = new DateTime(2026, 11, 14, 17, 0, 0, DateTimeKind.Utc),
        EndsAt = new DateTime(2026, 11, 14, 23, 0, 0, DateTimeKind.Utc),
        PackSizeEstimated = 100,
        StaffRequired = 4,
    };

    private sealed class PassThroughTransaction : ITransactionRunner
    {
        public Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
