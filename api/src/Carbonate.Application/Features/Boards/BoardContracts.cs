using Carbonate.Application.Common;
using Carbonate.Domain.Common;
using Carbonate.Domain.Features.Boards;

namespace Carbonate.Application.Features.Boards;

//----------------------------------------------------------\\
//                              READS
//----------------------------------------------------------\\

//wipLimit is display only (FR-23), nothing stops a column going over it
public record BoardDto(
    Guid BoardId,
    BoardType BoardType,
    Guid? EventId,
    string Name,
    IReadOnlyList<ColumnDto> Columns);

public record ColumnDto(
    Guid ColumnId,
    string Name,
    int Position,
    int? WipLimit,
    bool IsDoneColumn,
    IReadOnlyList<CardDto> Cards);

//no money lives on a card, so there's nothing here for the masker
public record CardDto(
    Guid CardId,
    Guid BoardId,
    Guid ColumnId,
    string Subject,
    string? Description,
    CardPriority Priority,
    DateTime? DueAt,
    int Position,
    string Status,
    Guid? MilestoneId,
    IReadOnlyList<UserRefDto> Assignees,
    UserRefDto CreatedBy,
    string? ReviewNotes,
    UserRefDto? ReturnedBy,
    DateTime? ReturnedAt,
    DateTime? CompletedAt,
    int AttachmentCount,
    string RowVersion);

//----------------------------------------------------------\\
//                              MOVES
//----------------------------------------------------------\\

//position is the card's index in the target column, counted without the card itself
public record MoveCardRequest(Guid ColumnId, int Position, string? RowVersion);

//----------------------------------------------------------\\
//                              CREATE AND EDIT
//----------------------------------------------------------\\

//new cards go to the bottom of their column. admin tasks always start in Assigned, with someone to do them
public record CreateCardRequest(
    string? Subject,
    string? Description, //the one field allowed formatting, cleaned on the way in
    CardPriority? Priority,
    DateTimeOffset? DueAt,
    Guid? MilestoneId,
    Guid? ColumnId, //event boards only, the first column when left out
    IReadOnlyList<Guid>? AssigneeIds);

//replaces every editable field, so send them all: one left out is cleared
public record UpdateCardRequest(
    string? Subject,
    string? Description,
    CardPriority? Priority,
    DateTimeOffset? DueAt,
    Guid? MilestoneId,
    string? RowVersion);

//the full set of people on the card, replacing whoever was there
public record AssigneesRequest(IReadOnlyList<Guid>? UserIds);

//----------------------------------------------------------\\
//                              ADMIN REVIEW (FR-20)
//----------------------------------------------------------\\

//the manager signs a handed-in task off
public record CompleteTaskRequest(string? RowVersion);

//the manager sends a handed-in task back to Assigned, saying what still needs doing
public record ReturnTaskRequest(string? ReviewNotes, string? RowVersion);

public interface IBoardService
{
    Task<BoardDto> GetAdminBoardAsync(CancellationToken ct);
    Task<BoardDto> GetEventBoardAsync(Guid eventId, CancellationToken ct);
    Task<CardDto> GetCardAsync(Guid cardId, CancellationToken ct);
    Task<CardDto> MoveCardAsync(Guid cardId, MoveCardRequest request, CancellationToken ct);
    Task<CardDto> CreateCardAsync(Guid boardId, CreateCardRequest request, CancellationToken ct);
    Task<CardDto> UpdateCardAsync(Guid cardId, UpdateCardRequest request, CancellationToken ct);
    Task<CardDto> SetAssigneesAsync(Guid cardId, AssigneesRequest request, CancellationToken ct);
    Task<CardDto> CompleteTaskAsync(Guid cardId, CompleteTaskRequest request, CancellationToken ct);
    Task<CardDto> ReturnTaskAsync(Guid cardId, ReturnTaskRequest request, CancellationToken ct);
}

//----------------------------------------------------------\\
//                              STORAGE
//----------------------------------------------------------\\

//a card with the names it shows, ready to map
public record CardRow(
    TaskCard Card,
    Guid BoardId,
    IReadOnlyList<UserRefDto> Assignees,
    UserRefDto CreatedBy,
    UserRefDto? ReturnedBy,
    int AttachmentCount);

//where a card sits: enough to decide who may see or move it
public record CardPlace(Guid CardId, Guid BoardId, BoardType BoardType, Guid? EventId, IReadOnlyList<Guid> AssigneeIds);

public interface IBoardRepository
{
    /// <summary>The board with its columns, no cards.</summary>
    Task<Board?> FindAdminBoardAsync(CancellationToken ct);

    /// <summary>The board with its columns, no cards.</summary>
    Task<Board?> FindEventBoardAsync(Guid eventId, CancellationToken ct);

    /// <summary>Cards on the board, or only those assigned to <paramref name="onlyAssignedTo"/> when set.</summary>
    Task<IReadOnlyList<CardRow>> ListCardsAsync(Guid boardId, Guid? onlyAssignedTo, CancellationToken ct);

    Task<CardRow?> FindCardAsync(Guid cardId, CancellationToken ct);
    Task<CardPlace?> FindCardPlaceAsync(Guid cardId, CancellationToken ct);

    /// <summary>The board with every column and card tracked, so a move can renumber positions.</summary>
    Task<Board?> LoadBoardForMoveAsync(Guid boardId, CancellationToken ct);

    /// <summary>
    /// Saves, checking the card still has <paramref name="expectedRowVersion"/>. False when someone
    /// changed it in the meantime.
    /// </summary>
    Task<bool> TrySaveCardAsync(TaskCard card, byte[] expectedRowVersion, CancellationToken ct);

    /// <summary>The board with its columns, no cards.</summary>
    Task<Board?> FindBoardAsync(Guid boardId, CancellationToken ct);

    Task<int> CountCardsAsync(Guid columnId, CancellationToken ct);
    void Add(TaskCard card);

    /// <summary>The card, tracked for changes, with its assignments.</summary>
    Task<TaskCard?> LoadCardForEditAsync(Guid cardId, CancellationToken ct);

    /// <summary>
    /// Adds an assignment to a card that's already saved. Ids are made in code, so one added only
    /// through the card's list would look like an existing row and be updated rather than inserted.
    /// </summary>
    void AddAssignment(TaskAssignment assignment);

    Task<bool> IsMilestoneOfEventAsync(Guid milestoneId, Guid eventId, CancellationToken ct);

    /// <summary>Which of these ids aren't active users.</summary>
    Task<IReadOnlyList<Guid>> MissingOrInactiveUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);

    /// <summary>Which of these users neither hold <c>event.view_all</c> nor are crewed on the event.</summary>
    Task<IReadOnlyList<Guid>> UsersWhoCannotSeeEventAsync(Guid eventId, IReadOnlyCollection<Guid> userIds,
        CancellationToken ct);

    /// <summary>Queues the card's due date for Google Calendar (FR-40), saved with the card.</summary>
    void QueueCalendarPush(Guid cardId, bool remove);

    Task SaveChangesAsync(CancellationToken ct);
}
