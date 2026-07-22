using CDA.Domain.Groups;
using CDA.Domain.Proposals;
using CDA.Domain.Topics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CDA.Infrastructure.Persistence.Configurations;

public sealed class ProposalGroupConfiguration : IEntityTypeConfiguration<ProposalGroup>
{
    public void Configure(EntityTypeBuilder<ProposalGroup> builder)
    {
        builder.ToTable("ProposalGroups");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();

        builder.Property(g => g.Description)
            .HasMaxLength(ProposalGroup.DescriptionMaxLength)
            .IsRequired();

        builder.HasIndex(g => new { g.TopicId, g.ScoreSum, g.Id }).HasDatabaseName("IX_Groups_Score");
        builder.HasIndex(g => new { g.TopicId, g.CreatedAtUtc, g.Id }).HasDatabaseName("IX_Groups_Created");
        builder.HasIndex(g => new { g.TopicId, g.CreatedByUserId });

        builder.HasOne<Topic>()
            .WithMany()
            .HasForeignKey(g => g.TopicId)
            .OnDelete(DeleteBehavior.Cascade);

        // A variant points at the alternative it refines. Deleting that one must not take the
        // variant with it — the refinement is still a solution in its own right.
        builder.HasOne<ProposalGroup>()
            .WithMany()
            .HasForeignKey(g => g.ImprovesGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(g => g.HasBeenJudged);
    }
}

public sealed class GroupItemConfiguration : IEntityTypeConfiguration<GroupItem>
{
    public void Configure(EntityTypeBuilder<GroupItem> builder)
    {
        builder.ToTable("GroupItems");

        // A set, not a list: membership is unique and carries no order.
        builder.HasKey(item => new { item.GroupId, item.ProposalId });

        // "Which alternatives include this proposal" — asked from the proposal page.
        builder.HasIndex(item => item.ProposalId);

        builder.HasOne<ProposalGroup>()
            .WithMany()
            .HasForeignKey(item => item.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Proposal>()
            .WithMany()
            .HasForeignKey(item => item.ProposalId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
