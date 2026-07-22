using CDA.Domain.Discussion;
using CDA.Domain.Topics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CDA.Infrastructure.Persistence.Configurations;

public sealed class RequirementConfiguration : IEntityTypeConfiguration<Requirement>
{
    public void Configure(EntityTypeBuilder<Requirement> builder)
    {
        builder.ToTable("Requirements");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Text).HasMaxLength(Requirement.TextMaxLength).IsRequired();

        // Always read as "the list for this topic, in order".
        builder.HasIndex(r => new { r.TopicId, r.Order });

        builder.HasOne<Topic>()
            .WithMany()
            .HasForeignKey(r => r.TopicId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("Comments", table =>
            // Extend when a new commentable type is added, so a comment can never be orphaned
            // or attached to two things at once.
            table.HasCheckConstraint("CK_Comments_SingleTarget", "(`TopicId` IS NOT NULL) = 1"));

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        // TEXT rather than VARCHAR so the full-text index in the migration can cover it.
        builder.Property(c => c.Body).HasColumnType("TEXT").IsRequired();

        // The thread is read newest-last for one target; the author filter serves "show me
        // this person's comments", which the search phase builds on.
        builder.HasIndex(c => new { c.TopicId, c.CreatedAtUtc });
        builder.HasIndex(c => c.AuthorId);

        builder.HasOne<Topic>()
            .WithMany()
            .HasForeignKey(c => c.TopicId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
