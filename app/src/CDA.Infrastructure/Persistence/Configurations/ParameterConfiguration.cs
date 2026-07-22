using CDA.Domain.Parameters;
using CDA.Domain.Topics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CDA.Infrastructure.Persistence.Configurations;

public sealed class ParameterTableConfiguration : IEntityTypeConfiguration<ParameterTable>
{
    public void Configure(EntityTypeBuilder<ParameterTable> builder)
    {
        builder.ToTable("ParameterTables");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Name).HasMaxLength(ParameterTable.NameMaxLength).IsRequired();

        // "My tables and everyone's shared ones, in this topic."
        builder.HasIndex(t => new { t.TopicId, t.OwnerId });
        builder.HasIndex(t => new { t.TopicId, t.IsShared });

        builder.HasOne<Topic>()
            .WithMany()
            .HasForeignKey(t => t.TopicId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ParameterConfiguration : IEntityTypeConfiguration<Parameter>
{
    public void Configure(EntityTypeBuilder<Parameter> builder)
    {
        builder.ToTable("Parameters");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Name).HasMaxLength(Parameter.NameMaxLength).IsRequired();

        builder.HasIndex(p => new { p.TableId, p.Order });

        builder.HasOne<ParameterTable>()
            .WithMany()
            .HasForeignKey(p => p.TableId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ParameterInfluenceConfiguration : IEntityTypeConfiguration<ParameterInfluence>
{
    public void Configure(EntityTypeBuilder<ParameterInfluence> builder)
    {
        builder.ToTable("ParameterInfluences", table =>
            // A factor's effect on itself says nothing; the diagonal is never filled in.
            table.HasCheckConstraint(
                "CK_ParameterInfluences_NotSelf", "`FromParameterId` <> `ToParameterId`"));

        // One judgement per ordered pair. The pair is ordered on purpose: "fuel price affects
        // congestion" and "congestion affects fuel price" are different claims, and a matrix
        // that could not hold both would lose most of what makes it worth drawing.
        builder.HasKey(i => new { i.TableId, i.FromParameterId, i.ToParameterId });

        builder.Property(i => i.Note).HasMaxLength(ParameterInfluence.NoteMaxLength);

        builder.HasOne<ParameterTable>()
            .WithMany()
            .HasForeignKey(i => i.TableId)
            .OnDelete(DeleteBehavior.Cascade);

        // No cascade from the parameters: the table already removes these, and MariaDB will not
        // accept three paths to the same row.
        builder.HasOne<Parameter>()
            .WithMany()
            .HasForeignKey(i => i.FromParameterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Parameter>()
            .WithMany()
            .HasForeignKey(i => i.ToParameterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
