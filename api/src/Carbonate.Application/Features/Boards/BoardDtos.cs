using Carbonate.Domain.Common;

namespace Carbonate.Application.Features.Boards;

public class BoardDto
{
    public Guid BoardId { get; set; }

    /// <summary>Null for the admin tasks board.</summary>
    public Guid? EventId { get; set; }

    public BoardType BoardType { get; set; }
    public string Name { get; set; } = "";
    public IReadOnlyList<BoardColumnDto> Columns { get; set; } = [];
}

public class BoardColumnDto
{
    public Guid ColumnId { get; set; }
    public string Name { get; set; } = "";
    public int Position { get; set; }

    /// <summary>Shown as a warning only; it never blocks a move (FR-23).</summary>
    public int? WipLimit { get; set; }

    public bool IsDoneColumn { get; set; }
    public IReadOnlyList<CardDto> Cards { get; set; } = [];
}

public class CardDto
{
    public Guid CardId { get; set; }
    public Guid ColumnId { get; set; }
    public Guid? MilestoneId { get; set; }
    public string Subject { get; set; } = "";

    /// <summary>Sanitised HTML. The only free-text field that allows formatting.</summary>
    public string? Description { get; set; }

    public CardPriority Priority { get; set; }
    public DateTime? DueAt { get; set; }
    public int Position { get; set; }

    /// <summary>Open or Done on event boards; Assigned, InProgressOrNeedsReview or Complete on the admin board.</summary>
    public string Status { get; set; } = "";

    public string? ReviewNotes { get; set; }
    public Guid? ReturnedByUserId { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
    public IReadOnlyList<CardAssigneeDto> Assignees { get; set; } = [];
    public int AttachmentCount { get; set; }
    public string RowVersion { get; set; } = "";
}

public class CardAssigneeDto
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = "";
}

public class CreateCardRequest
{
    public Guid ColumnId { get; set; }
    public string Subject { get; set; } = "";
    public string? Description { get; set; }
    public CardPriority Priority { get; set; } = CardPriority.Normal;
    public DateTime? DueAt { get; set; }
    public Guid? MilestoneId { get; set; }
    public IReadOnlyList<Guid> AssigneeIds { get; set; } = [];
}

public class UpdateCardRequest
{
    public string? Subject { get; set; }
    public string? Description { get; set; }
    public CardPriority? Priority { get; set; }
    public DateTime? DueAt { get; set; }
    public string RowVersion { get; set; } = "";
}

public class MoveCardRequest
{
    public Guid ColumnId { get; set; }
    public int Position { get; set; }
    public string RowVersion { get; set; } = "";
}

public class AssigneesRequest
{
    public IReadOnlyList<Guid> UserIds { get; set; } = [];
    public string RowVersion { get; set; } = "";
}

/// <summary>Body of complete and return. Only the manager who created the task may send either.</summary>
public class ReviewCardRequest
{
    public string? ReviewNotes { get; set; }
    public string RowVersion { get; set; } = "";
}

public class CardAttachmentDto
{
    public Guid AttachmentId { get; set; }
    public Guid CardId { get; set; }
    public string FileName { get; set; } = "";
    public Guid UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; }
    public string? DownloadUrl { get; set; }
}
