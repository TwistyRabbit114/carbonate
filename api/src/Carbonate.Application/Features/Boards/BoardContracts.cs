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

public interface IBoardService
{
    Task<BoardDto> GetAdminBoardAsync(CancellationToken ct);
    Task<BoardDto> GetEventBoardAsync(Guid eventId, CancellationToken ct);
    Task<CardDto> GetCardAsync(Guid cardId, CancellationToken ct);
    Task<CardDto> MoveCardAsync(Guid cardId, MoveCardRequest request, CancellationToken ct);
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
    /// Saves, checking the moved card still has <paramref name="expectedRowVersion"/>. False when someone
    /// changed it in the meantime.
    /// </summary>
    Task<bool> TrySaveMoveAsync(TaskCard card, byte[] expectedRowVersion, CancellationToken ct);
}
