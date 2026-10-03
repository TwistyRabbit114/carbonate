using Carbonate.Domain.Common;

namespace Carbonate.Domain.Features.Boards;

public class ChecklistTemplate
{
    public Guid TemplateId { get; set; } = Guid.NewGuid();
    public Guid DivisionId { get; set; }
    public string Name { get; set; } = "";
    public BoardType BoardType { get; set; }
    public string DefinitionJson { get; set; } = "{}";
    public bool IsActive { get; set; } = true;
}

public class Board
{
    public Guid BoardId { get; set; } = Guid.NewGuid();
    public Guid? EventId { get; set; }
    public Guid? TemplateId { get; set; }
    public BoardType BoardType { get; set; }
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    public List<BoardColumn> Columns { get; set; } = [];
}

public class BoardColumn
{
    public Guid ColumnId { get; set; } = Guid.NewGuid();
    public Guid BoardId { get; set; }
    public string Name { get; set; } = "";
    public int Position { get; set; }
    public int? WipLimit { get; set; }
    public bool IsDoneColumn { get; set; }

    public List<TaskCard> Cards { get; set; } = [];
}

public class TaskCard
{
    public Guid CardId { get; set; } = Guid.NewGuid();
    public Guid ColumnId { get; set; }
    public Guid? MilestoneId { get; set; }
    public string Subject { get; set; } = "";
    public string? Description { get; set; }
    public CardPriority Priority { get; set; } = CardPriority.Normal;
    public DateTime? DueAt { get; set; }
    public int Position { get; set; }
    public string Status { get; set; } = "";
    public string? ReviewNotes { get; set; }
    public Guid? ReturnedByUserId { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public List<TaskAssignment> Assignments { get; set; } = [];
    public List<CardAttachment> Attachments { get; set; } = [];
}

public class TaskAssignment
{
    public Guid AssignmentId { get; set; } = Guid.NewGuid();
    public Guid CardId { get; set; }
    public Guid UserId { get; set; }
    public DateTime AssignedAt { get; set; }
}

public class CardAttachment
{
    public Guid AttachmentId { get; set; } = Guid.NewGuid();
    public Guid CardId { get; set; }
    public Guid UploadedByUserId { get; set; }
    public string FileName { get; set; } = "";
    public string BlobUri { get; set; } = "";
    public string Sha256Hash { get; set; } = "";
    public DateTime UploadedAt { get; set; }
}
