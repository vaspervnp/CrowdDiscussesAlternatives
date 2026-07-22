using CDA.Domain.Proposals;
using CDA.Domain.Topics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CDA.Infrastructure.Persistence.Configurations;

public sealed class ProposalConfiguration : IEntityTypeConfiguration<Proposal>
{
    public void Configure(EntityTypeBuilder<Proposal> builder)
    {
        builder.ToTable("Proposals");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Text).HasMaxLength(Proposal.TextMaxLength).IsRequired();

        // One index per ordering the list offers, each ending in Id so keyset paging has a
        // stable tiebreaker. Without these, sorting a pool of thousands of proposals by score
        // is a filesort on every page.
        builder.HasIndex(p => new { p.TopicId, p.ScoreSum, p.Id }).HasDatabaseName("IX_Proposals_Score");
        builder.HasIndex(p => new { p.TopicId, p.CreatedAtUtc, p.Id }).HasDatabaseName("IX_Proposals_Created");
        builder.HasIndex(p => new { p.TopicId, p.LastCommentAtUtc, p.Id }).HasDatabaseName("IX_Proposals_LastComment");
        builder.HasIndex(p => new { p.TopicId, p.AuthorId }).HasDatabaseName("IX_Proposals_Author");

        builder.HasOne<Topic>()
            .WithMany()
            .HasForeignKey(p => p.TopicId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
