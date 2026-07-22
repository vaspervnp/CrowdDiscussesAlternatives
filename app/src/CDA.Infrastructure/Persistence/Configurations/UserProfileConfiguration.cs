using CDA.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CDA.Infrastructure.Persistence.Configurations;

public sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("UserProfiles");

        // Shares its key with the Identity user rather than generating one.
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.HasKey(p => p.Id);

        builder.Property(p => p.DisplayName)
            .HasMaxLength(UserProfile.DisplayNameMaxLength)
            .IsRequired();

        // Two participants sharing a display name could be mistaken for one another in a
        // discussion. The database collation is case-insensitive, so this also rules out
        // near-duplicates that differ only in case.
        builder.HasIndex(p => p.DisplayName).IsUnique();

        builder.Property(p => p.RealName).HasMaxLength(UserProfile.RealNameMaxLength);
        builder.Property(p => p.Email).HasMaxLength(320);
        builder.Property(p => p.Location).HasMaxLength(UserProfile.LocationMaxLength);
        builder.Property(p => p.Website).HasMaxLength(UserProfile.WebsiteMaxLength);
        builder.Property(p => p.Biography).HasMaxLength(UserProfile.BiographyMaxLength);

        // Presence is queried as "who is online", which is a range scan over this column.
        builder.HasIndex(p => p.LastSeenAtUtc);
    }
}
