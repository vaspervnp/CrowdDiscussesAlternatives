using CDA.Domain.Evaluation;
using CDA.Domain.Groups;
using CDA.Domain.Topics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CDA.Infrastructure.Persistence.Configurations;

public sealed class RequirementWeightConfiguration : IEntityTypeConfiguration<RequirementWeight>
{
    public void Configure(EntityTypeBuilder<RequirementWeight> builder)
    {
        builder.ToTable("RequirementWeights", table =>
            table.HasCheckConstraint(
                "CK_RequirementWeights_Range",
                $"`Weight` BETWEEN {RequirementWeight.Minimum} AND {RequirementWeight.Maximum}"));

        // One weight per person per requirement — the key expresses that weights belong to the
        // topic rather than to any one alternative.
        builder.HasKey(w => new { w.UserId, w.RequirementId });

        builder.HasIndex(w => new { w.UserId, w.TopicId });

        builder.HasOne<Requirement>()
            .WithMany()
            .HasForeignKey(w => w.RequirementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Topic>()
            .WithMany()
            .HasForeignKey(w => w.TopicId)
            // The requirement cascade already removes these; a second path would give MariaDB
            // two ways to delete the same row.
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class RequirementScoreConfiguration : IEntityTypeConfiguration<RequirementScore>
{
    public void Configure(EntityTypeBuilder<RequirementScore> builder)
    {
        builder.ToTable("RequirementScores", table =>
            table.HasCheckConstraint(
                "CK_RequirementScores_Range",
                $"`Score` BETWEEN {RequirementScore.Minimum} AND {RequirementScore.Maximum}"));

        builder.HasKey(s => new { s.UserId, s.GroupId, s.RequirementId });

        // "Everything I have evaluated in this topic", for the side-by-side comparison.
        builder.HasIndex(s => new { s.UserId, s.TopicId });

        builder.HasOne<ProposalGroup>()
            .WithMany()
            .HasForeignKey(s => s.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Requirement>()
            .WithMany()
            .HasForeignKey(s => s.RequirementId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Topic>()
            .WithMany()
            .HasForeignKey(s => s.TopicId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
