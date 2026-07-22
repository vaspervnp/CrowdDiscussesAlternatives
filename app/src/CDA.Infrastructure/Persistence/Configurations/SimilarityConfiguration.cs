using CDA.Domain.Proposals;
using CDA.Domain.Similarity;
using CDA.Domain.Topics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CDA.Infrastructure.Persistence.Configurations;

public sealed class SimilarityReportConfiguration : IEntityTypeConfiguration<SimilarityReport>
{
    public void Configure(EntityTypeBuilder<SimilarityReport> builder)
    {
        builder.ToTable("SimilarityReports", table =>
            // The pair is canonically ordered by the domain; the database enforces it too, so
            // no other route can create the mirror-image duplicate the ordering exists to stop.
            table.HasCheckConstraint("CK_Similarity_Ordered", "`ProposalAId` < `ProposalBId`"));

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Justification).HasMaxLength(SimilarityReport.JustificationMaxLength);

        // One report per pair. A second row would split the votes that decide whether the
        // claim takes effect.
        builder.HasIndex(r => new { r.ProposalAId, r.ProposalBId })
            .IsUnique()
            .HasDatabaseName("UX_Similarity_Pair");

        // "Which reports are active in this topic" is read on every collapsed proposal list.
        builder.HasIndex(r => new { r.TopicId, r.ScoreSum });
        builder.HasIndex(r => r.ProposalBId);

        builder.HasOne<Topic>()
            .WithMany()
            .HasForeignKey(r => r.TopicId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Proposal>()
            .WithMany()
            .HasForeignKey(r => r.ProposalAId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Proposal>()
            .WithMany()
            .HasForeignKey(r => r.ProposalBId)
            // Cascading from both sides would give MariaDB multiple cascade paths to the same
            // row; the A side already removes the report when a proposal goes.
            .OnDelete(DeleteBehavior.Restrict);
    }
}
