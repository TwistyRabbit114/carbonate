using Carbonate.Domain.Features.Boards;
using Carbonate.Domain.Features.Events;
using Carbonate.Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Carbonate.Infrastructure.Persistence.Configurations;

internal class ChecklistTemplateConfiguration : IEntityTypeConfiguration<ChecklistTemplate>
{
    public void Configure(EntityTypeBuilder<ChecklistTemplate> b)
    {
        b.HasKey(x => x.TemplateId);
        b.Property(x => x.DefinitionJson).HasColumnType("nvarchar(max)");
        b.HasIndex(x => new { x.DivisionId, x.Name }).IsUnique();
        b.ToTable(t => t.HasCheckConstraint("CK_ChecklistTemplate_Definition", "ISJSON([DefinitionJson]) = 1"));
        b.Ref<Division>(nameof(ChecklistTemplate.DivisionId));
    }
}

internal class BoardConfiguration : IEntityTypeConfiguration<Board>
{
    public void Configure(EntityTypeBuilder<Board> b)
    {
        b.ClusterOn(x => x.CreatedAt, x => x.BoardId);
        // One task board per event; the admin board has no event and is not constrained here.
        b.HasIndex(x => x.EventId).IsUnique().HasFilter("[EventId] IS NOT NULL");
        b.ToTable(t => t.HasCheckConstraint("CK_Board_Type",
            "([BoardType] = 'Event' AND [EventId] IS NOT NULL) OR ([BoardType] = 'Admin' AND [EventId] IS NULL)"));
        b.Ref<Event>(nameof(Board.EventId), DeleteBehavior.Cascade);
        b.Ref<ChecklistTemplate>(nameof(Board.TemplateId));
        b.HasMany(x => x.Columns).WithOne()
            .HasForeignKey(x => x.BoardId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal class BoardColumnConfiguration : IEntityTypeConfiguration<BoardColumn>
{
    public void Configure(EntityTypeBuilder<BoardColumn> b)
    {
        b.HasKey(x => x.ColumnId);
        b.Property(x => x.Name).HasMaxLength(100);
        b.HasIndex(x => new { x.BoardId, x.Position }).IsUnique();
        b.ToTable(t => t.HasCheckConstraint("CK_BoardColumn_WipLimit", "[WipLimit] IS NULL OR [WipLimit] > 0"));
        b.HasMany(x => x.Cards).WithOne()
            .HasForeignKey(x => x.ColumnId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal class TaskCardConfiguration : IEntityTypeConfiguration<TaskCard>
{
    public void Configure(EntityTypeBuilder<TaskCard> b)
    {
        b.ClusterOn(x => x.CreatedAt, x => x.CardId);
        b.Property(x => x.Subject).HasMaxLength(200);
        b.Property(x => x.Description).HasColumnType("nvarchar(max)");
        b.Property(x => x.Status).HasMaxLength(30);
        b.Property(x => x.ReviewNotes).HasMaxLength(2000);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.ColumnId, x.Position });
        b.HasIndex(x => x.DueAt);
        b.Ref<EventMilestone>(nameof(TaskCard.MilestoneId));
        b.Ref<AppUser>(nameof(TaskCard.CreatedByUserId));
        b.Ref<AppUser>(nameof(TaskCard.ReturnedByUserId));
        b.HasMany(x => x.Assignments).WithOne()
            .HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Attachments).WithOne()
            .HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal class TaskAssignmentConfiguration : IEntityTypeConfiguration<TaskAssignment>
{
    public void Configure(EntityTypeBuilder<TaskAssignment> b)
    {
        b.HasKey(x => x.AssignmentId);
        b.HasIndex(x => new { x.CardId, x.UserId }).IsUnique();
        b.HasIndex(x => x.UserId);
        b.Ref<AppUser>(nameof(TaskAssignment.UserId));
    }
}

internal class CardAttachmentConfiguration : IEntityTypeConfiguration<CardAttachment>
{
    public void Configure(EntityTypeBuilder<CardAttachment> b)
    {
        b.ClusterOn(x => x.UploadedAt, x => x.AttachmentId);
        b.Property(x => x.FileName).HasMaxLength(260);
        b.Property(x => x.BlobUri).HasMaxLength(1000);
        b.Property(x => x.Sha256Hash).HasMaxLength(64);
        b.HasIndex(x => x.CardId);
        b.Ref<AppUser>(nameof(CardAttachment.UploadedByUserId));
    }
}
