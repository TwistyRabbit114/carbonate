using Carbonate.Application.Common;
using Carbonate.Application.Masking;
using Carbonate.Application.Platform.Audit;
using Carbonate.Application.Platform.Auth;
using Carbonate.Application.Platform.Files;
using Carbonate.Domain.Features.Stock;

namespace Carbonate.Application.Features.Incidents;

/// <summary>
/// FR-31. Built for quick entry from a phone at a venue: an incident needs a type, a subject and a
/// sentence, and everything else is optional.
/// </summary>
public sealed class IncidentService(
    IIncidentRepository incidents,
    IFileStorage files,
    IAuditService audit,
    IFinancialMasker masker,
    ICurrentUser user,
    TimeProvider clock) : IIncidentService
{
    public async Task<IncidentDto> ReportAsync(
        Guid eventId, ReportIncident report, IncidentPhoto? photo, CancellationToken ct)
    {
        Require(PermissionCodes.IncidentCreate);

        // Crew may only report against events they are actually on. Anyone with event.view_all is
        // trusted with the lot. A 404 rather than a 403, so an event's existence is not revealed.
        var mustBeCrewed = user.HasPermission(PermissionCodes.EventViewAll) ? (Guid?)null : user.UserId;
        if (!await incidents.EventIsReportableAsync(eventId, mustBeCrewed, ct))
        {
            throw ProblemException.NotFound("That event was not found.");
        }

        var subject = await ValidateAsync(report, ct);

        var incident = new IncidentReport
        {
            EventId = eventId,
            AssetId = report.AssetId,
            StockItemId = report.StockItemId,
            ReportedByUserId = user.UserId,
            IncidentType = report.IncidentType,
            Quantity = report.Quantity,
            ReportedAt = clock.GetUtcNow().UtcDateTime,
            Description = report.Description.Trim(),
        };

        if (photo is not null)
        {
            // IFileStorage validates the type and size and computes the hash; a bad file throws a 400
            // before anything is written, so a rejected photo never leaves a half-made incident.
            var stored = await files.UploadAsync(photo.Content, photo.FileName, photo.ContentType, ct);
            incident.PhotoBlobUri = stored.BlobUri;
            incident.PhotoSha256 = stored.Sha256;
        }

        incidents.Add(incident);
        await incidents.SaveChangesAsync(ct);

        await audit.RecordAsync("incident.create", nameof(IncidentReport), incident.IncidentId.ToString(),
            null,
            new
            {
                incident.EventId,
                IncidentType = incident.IncidentType.ToString(),
                Subject = subject.Name,
                incident.Quantity,
                HasPhoto = incident.PhotoBlobUri is not null,
            },
            user.UserId, ct);

        return await DetailAsync(incident.IncidentId, ct);
    }

    public async Task<IReadOnlyList<IncidentDto>> ForEventAsync(Guid eventId, CancellationToken ct)
    {
        Require(PermissionCodes.IncidentView);

        var visibleTo = user.HasPermission(PermissionCodes.EventViewAll) ? (Guid?)null : user.UserId;
        if (!await incidents.EventIsReportableAsync(eventId, visibleTo, ct))
        {
            throw ProblemException.NotFound("That event was not found.");
        }

        var rows = await incidents.ListForEventAsync(eventId, ct);
        return await ToDtosAsync(rows, ct);
    }

    public async Task<PagedResult<IncidentDto>> ListAsync(IncidentListQuery query, CancellationToken ct)
    {
        Require(PermissionCodes.IncidentView);

        query.Page = Math.Max(query.Page, 1);
        query.PageSize = Math.Clamp(query.PageSize, 1, PageQuery.MaxPageSize);

        var visibleTo = user.HasPermission(PermissionCodes.EventViewAll) ? (Guid?)null : user.UserId;
        var page = await incidents.ListAsync(query, visibleTo, ct);

        return new PagedResult<IncidentDto>
        {
            Items = await ToDtosAsync(page.Items, ct),
            Page = page.Page,
            PageSize = page.PageSize,
            Total = page.Total,
        };
    }

    public async Task<IncidentDto> UpdateAsync(
        Guid incidentId, UpdateIncidentRequest request, CancellationToken ct)
    {
        // stock.manage, not incident.create: resolving carries ReplacementCost, which is a $cost field,
        // so it belongs with the Director and the Operations Manager (confirmed with C, 4 Oct).
        Require(PermissionCodes.StockManage);

        var incident = await incidents.FindAsync(incidentId, ct) ?? throw ProblemException.NotFound();

        if (request.ReplacementCost is < 0)
        {
            throw ProblemException.Validation(new Dictionary<string, string[]>
            {
                ["replacementCost"] = ["A cost cannot be negative."],
            });
        }

        var before = new { incident.ResolutionNotes, HadCost = incident.ReplacementCost is not null };

        incident.ResolutionNotes = request.ResolutionNotes?.Trim();

        // Same trap as the stock catalogue: a caller who cannot see $cost reads the incident with the
        // field absent, sends it back as null, and would wipe a figure they were never shown. Absent
        // means "leave it".
        if (user.HasPermission(PermissionCodes.FinanceViewInternalCost))
        {
            incident.ReplacementCost = request.ReplacementCost;
        }

        await incidents.SaveChangesAsync(ct);
        await audit.RecordAsync("incident.resolve", nameof(IncidentReport), incidentId.ToString(),
            before, new { incident.ResolutionNotes, HadCost = incident.ReplacementCost is not null },
            user.UserId, ct);

        return await DetailAsync(incidentId, ct);
    }

    // ---- helpers ---------------------------------------------------------------------------

    private void Require(string permission)
    {
        if (!user.HasPermission(permission))
        {
            throw ProblemException.Forbidden();
        }
    }

    private async Task<IncidentSubject> ValidateAsync(ReportIncident report, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(report.Description))
        {
            errors["description"] = ["Say what happened."];
        }

        if (report.Quantity is <= 0)
        {
            errors["quantity"] = ["A quantity must be more than zero."];
        }

        // One subject or the other, never neither and never both. The database has the same CHECK, so
        // a bug here still cannot write a row with nothing attached.
        if (report.AssetId is null && report.StockItemId is null)
        {
            errors["assetId"] = ["Choose the equipment or the stock item this happened to."];
        }

        if (report.AssetId is not null && report.StockItemId is not null)
        {
            errors["assetId"] = ["Choose either a piece of equipment or a stock item, not both."];
        }

        var subject = await incidents.CheckSubjectAsync(report.AssetId, report.StockItemId, ct);

        if (report.AssetId is not null && !subject.AssetExists)
        {
            errors["assetId"] = ["That equipment was not found."];
        }

        if (report.StockItemId is not null && !subject.StockItemExists)
        {
            errors["stockItemId"] = ["That stock item was not found."];
        }

        if (errors.Count > 0)
        {
            throw ProblemException.Validation(errors);
        }

        return subject;
    }

    private async Task<IncidentDto> DetailAsync(Guid incidentId, CancellationToken ct)
    {
        var row = await incidents.GetRowAsync(incidentId, ct) ?? throw ProblemException.NotFound();
        var dto = await ToDtoAsync(row, ct);
        masker.Mask(dto, user);
        return dto;
    }

    private async Task<IReadOnlyList<IncidentDto>> ToDtosAsync(
        IEnumerable<IncidentRow> rows, CancellationToken ct)
    {
        var dtos = new List<IncidentDto>();

        foreach (var row in rows)
        {
            dtos.Add(await ToDtoAsync(row, ct));
        }

        masker.Mask(dtos, user);
        return dtos;
    }

    private async Task<IncidentDto> ToDtoAsync(IncidentRow row, CancellationToken ct) => new()
    {
        IncidentId = row.IncidentId,
        EventId = row.EventId,
        AssetId = row.AssetId,
        StockItemId = row.StockItemId,
        SubjectName = row.SubjectName,
        ReportedByUserId = row.ReportedByUserId,
        ReportedByName = row.ReportedByName,
        IncidentType = row.IncidentType,
        Quantity = row.Quantity,
        ReportedAt = row.ReportedAt,
        Description = row.Description,
        ResolutionNotes = row.ResolutionNotes,
        ReplacementCost = row.ReplacementCost,
        // A short-lived SAS link, minted per response. The blob URI itself is never sent: it is not
        // something a browser can follow, and publishing it would invite someone to try.
        PhotoUrl = row.PhotoBlobUri is null
            ? null
            : (await files.GetDownloadUrlAsync(row.PhotoBlobUri, PhotoName(row), ct)).ToString(),
    };

    private static string PhotoName(IncidentRow row) =>
        $"incident-{row.ReportedAt:yyyyMMdd}-{row.IncidentId.ToString()[..8]}.jpg";
}
