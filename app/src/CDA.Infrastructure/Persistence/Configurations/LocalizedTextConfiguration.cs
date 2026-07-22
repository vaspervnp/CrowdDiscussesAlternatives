using CDA.Domain.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CDA.Infrastructure.Persistence.Configurations;

public sealed class LocalizedTextConfiguration : IEntityTypeConfiguration<LocalizedText>
{
    public void Configure(EntityTypeBuilder<LocalizedText> builder)
    {
        builder.ToTable("LocalizedTexts");

        // One value per (string, language). The composite key is the natural one and doubles as
        // the lookup index the localizer reads by.
        builder.HasKey(t => new { t.Key, t.Culture });

        // A binary collation on the key, overriding the model's case- and accent-insensitive
        // default. The key is an exact English string, and distinct strings must stay distinct:
        // under the default collation "closed" and "Closed" compare equal and collide on the
        // primary key, which would fail the whole seed. utf8mb4_bin compares byte for byte, so
        // every source string is its own row.
        builder.Property(t => t.Key).HasMaxLength(LocalizedText.KeyMaxLength).UseCollation("utf8mb4_bin");

        // BCP-47 tags are short ("el-GR"); this is roomy.
        builder.Property(t => t.Culture).HasMaxLength(35).UseCollation("utf8mb4_bin");

        builder.Property(t => t.Value).IsRequired();

        // The admin screen and the cache primer both load a whole language at once.
        builder.HasIndex(t => t.Culture);
    }
}
