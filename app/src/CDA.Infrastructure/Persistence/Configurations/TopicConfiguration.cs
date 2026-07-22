using CDA.Domain.Topics;
using CDA.Domain.Voting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CDA.Infrastructure.Persistence.Configurations;

public sealed class TopicConfiguration : IEntityTypeConfiguration<Topic>
{
    public void Configure(EntityTypeBuilder<Topic> builder)
    {
        builder.ToTable("Topics");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Subject).HasMaxLength(Topic.SubjectMaxLength).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(Topic.DescriptionMaxLength).IsRequired();

        // The topic list is ordered by importance and by recency; both are covering indexes
        // ending in Id so that keyset pagination has a stable tiebreaker.
        builder.HasIndex(t => new { t.ScoreSum, t.Id }).HasDatabaseName("IX_Topics_Score");
        builder.HasIndex(t => new { t.CreatedAtUtc, t.Id }).HasDatabaseName("IX_Topics_Created");
        builder.HasIndex(t => t.Visibility);
    }
}

public sealed class TopicMemberConfiguration : IEntityTypeConfiguration<TopicMember>
{
    public void Configure(EntityTypeBuilder<TopicMember> builder)
    {
        builder.ToTable("TopicMembers");

        // One membership per person per topic, by construction rather than by checking.
        builder.HasKey(m => new { m.TopicId, m.UserId });

        // "Which topics am I in?" is asked on every page that lists topics.
        builder.HasIndex(m => m.UserId);

        builder.HasOne<Topic>()
            .WithMany()
            .HasForeignKey(m => m.TopicId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class VoteConfiguration : IEntityTypeConfiguration<Vote>
{
    public void Configure(EntityTypeBuilder<Vote> builder)
    {
        builder.ToTable("Votes", table =>
        {
            table.HasCheckConstraint("CK_Votes_Value", "`Value` IN (-1, 0, 1)");

            // Exactly one target. Extend this when a new votable type is added — the point
            // of the constraint is that a vote can never be orphaned or double-attached.
            table.HasCheckConstraint("CK_Votes_SingleTarget", "(`TopicId` IS NOT NULL) = 1");
        });

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        // One vote per user per target, enforced by the database rather than by a read
        // followed by a write. MariaDB treats NULLs as distinct, so this one index does not
        // interfere with the other target columns to come.
        builder.HasIndex(v => new { v.UserId, v.TopicId })
            .IsUnique()
            .HasDatabaseName("UX_Votes_User_Topic");

        builder.HasOne<Topic>()
            .WithMany()
            .HasForeignKey(v => v.TopicId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
