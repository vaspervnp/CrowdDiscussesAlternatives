using CDA.Domain.Attachments;
using CDA.Domain.Messaging;
using CDA.Domain.Notifications;
using CDA.Domain.Proposals;
using CDA.Domain.Topics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CDA.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();

        builder.Property(n => n.Summary).HasMaxLength(Notification.SummaryMaxLength).IsRequired();
        builder.Property(n => n.Link).HasMaxLength(500).IsRequired();

        // "My notifications, newest first" and "the unread badge".
        builder.HasIndex(n => new { n.UserId, n.CreatedAtUtc });
        builder.HasIndex(n => new { n.UserId, n.ReadAtUtc });

        // The outbox: the dispatcher reads exactly this.
        builder.HasIndex(n => new { n.EmailedAtUtc, n.CreatedAtUtc })
            .HasDatabaseName("IX_Notifications_Outbox");

        builder.HasOne<Topic>()
            .WithMany()
            .HasForeignKey(n => n.TopicId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("NotificationPreferences");
        builder.HasKey(p => p.UserId);
    }
}

public sealed class PrivateMessageConfiguration : IEntityTypeConfiguration<PrivateMessage>
{
    public void Configure(EntityTypeBuilder<PrivateMessage> builder)
    {
        builder.ToTable("PrivateMessages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Body).HasMaxLength(PrivateMessage.BodyMaxLength).IsRequired();

        // A conversation is read from both directions, so both sides are indexed.
        builder.HasIndex(m => new { m.ToUserId, m.SentAtUtc });
        builder.HasIndex(m => new { m.FromUserId, m.SentAtUtc });
        builder.HasIndex(m => new { m.ToUserId, m.ReadAtUtc });
    }
}

public sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("Attachments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.FileName).HasMaxLength(Attachment.FileNameMaxLength).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(150).IsRequired();
        builder.Property(a => a.StoredName).HasMaxLength(100).IsRequired();

        builder.HasIndex(a => a.ProposalId);

        // Every download is checked against the topic it belongs to before the file is opened.
        builder.HasIndex(a => new { a.TopicId, a.Id });

        builder.HasOne<Proposal>()
            .WithMany()
            .HasForeignKey(a => a.ProposalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Topic>()
            .WithMany()
            .HasForeignKey(a => a.TopicId)
            // The proposal cascade already covers these.
            .OnDelete(DeleteBehavior.Restrict);
    }
}
