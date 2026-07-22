using CDA.Domain.Proposals;
using CDA.Domain.References;
using CDA.Domain.Topics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CDA.Infrastructure.Persistence.Configurations;

public sealed class ReferenceConfiguration : IEntityTypeConfiguration<Reference>
{
    public void Configure(EntityTypeBuilder<Reference> builder)
    {
        builder.ToTable("References");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.CanonicalUrl).HasMaxLength(ReferenceUrl.MaxLength).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(Reference.DescriptionMaxLength).IsRequired();

        // One entry per source per topic. Enforced by the database rather than by checking
        // first, so two people citing the same thing at the same moment cannot both win.
        builder.HasIndex(r => new { r.TopicId, r.CanonicalUrl })
            .IsUnique()
            .HasDatabaseName("UX_References_Topic_Url");

        builder.HasIndex(r => new { r.TopicId, r.CreatedByUserId });

        builder.HasOne<Topic>()
            .WithMany()
            .HasForeignKey(r => r.TopicId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(r => r.CombinedScore);
    }
}

public sealed class ProposalReferenceConfiguration : IEntityTypeConfiguration<ProposalReference>
{
    public void Configure(EntityTypeBuilder<ProposalReference> builder)
    {
        builder.ToTable("ProposalReferences");

        // A source is cited by a proposal once, by construction.
        builder.HasKey(link => new { link.ProposalId, link.ReferenceId });

        builder.HasIndex(link => link.ReferenceId);

        builder.HasOne<Proposal>()
            .WithMany()
            .HasForeignKey(link => link.ProposalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Reference>()
            .WithMany()
            .HasForeignKey(link => link.ReferenceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TopicUserReputationConfiguration : IEntityTypeConfiguration<TopicUserReputation>
{
    public void Configure(EntityTypeBuilder<TopicUserReputation> builder)
    {
        builder.ToTable("TopicUserReputations");
        builder.HasKey(x => new { x.TopicId, x.UserId });

        // Read as "the best-regarded citers in this topic".
        builder.HasIndex(x => new { x.TopicId, x.ReferenceScore });

        builder.HasOne<Topic>()
            .WithMany()
            .HasForeignKey(x => x.TopicId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
