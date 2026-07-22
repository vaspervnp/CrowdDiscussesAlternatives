using System.Text;
using CDA.Application.Abstractions;
using CDA.Domain.Attachments;
using CDA.Domain.Notifications;
using CDA.Domain.Topics;
using CDA.Infrastructure;
using CDA.Infrastructure.Attachments;
using CDA.Infrastructure.Discussion;
using CDA.Infrastructure.Identity;
using CDA.Infrastructure.Messaging;
using CDA.Infrastructure.Notifications;
using CDA.Infrastructure.Persistence;
using CDA.Infrastructure.Proposals;
using CDA.Infrastructure.Similarity;
using CDA.Infrastructure.Topics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CDA.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class CommunicationTests(DatabaseFixture database) : IAsyncLifetime
{
    private CdaWebApplicationFactory _factory = null!;
    private string _uploads = string.Empty;

    public async Task InitializeAsync()
    {
        await database.ResetAsync();
        _factory = new CdaWebApplicationFactory(database);

        // Uploads go to a directory of this test's own, so a run leaves nothing behind and two
        // tests cannot see each other's files.
        _uploads = Path.Combine(Path.GetTempPath(), $"cda-uploads-{Guid.NewGuid():N}");
        _factory.Services.GetRequiredService<AttachmentOptions>().RootPath = _uploads;
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();

        if (Directory.Exists(_uploads))
        {
            Directory.Delete(_uploads, recursive: true);
        }

        return Task.CompletedTask;
    }

    private IServiceScope Scope() => _factory.Services.CreateScope();

    private async Task<Guid> UserAsync(string name)
    {
        using var scope = Scope();
        var accounts = scope.ServiceProvider.GetRequiredService<UserAccountService>();
        var result = await accounts.RegisterAsync($"{name}@example.com", name, "a long spoken passphrase");
        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        return result.UserId;
    }

    private async Task<(Guid TopicId, Guid ProposalId)> ProposalAsync(Guid ownerId)
    {
        Guid topicId;

        using (var scope = Scope())
        {
            var topic = await scope.ServiceProvider.GetRequiredService<TopicService>()
                .CreateAsync("How should we reduce traffic?", "", ownerId, TopicVisibility.Public, null);
            topicId = topic.Id;

            await scope.ServiceProvider.GetRequiredService<RequirementService>()
                .AddAsync(topicId, "Must not increase journey times");
        }

        await using (var context = database.CreateContext())
        {
            var stored = await context.Topics.SingleAsync(t => t.Id == topicId);
            stored.OpenForProposals(requirementCount: 1);
            await context.SaveChangesAsync();
        }

        using var proposalScope = Scope();
        var proposal = await proposalScope.ServiceProvider.GetRequiredService<ProposalService>()
            .CreateAsync(topicId, ownerId, "A toll fee is suggested.", null);

        Assert.True(proposal.Succeeded, proposal.Error);

        return (topicId, proposal.Id);
    }

    private async Task CommentAsync(Guid topicId, Guid proposalId, Guid authorId, string body)
    {
        using var scope = Scope();
        var result = await scope.ServiceProvider.GetRequiredService<CommentService>()
            .PostToProposalAsync(topicId, proposalId, authorId, body);

        Assert.True(result.Succeeded, result.Error);
    }

    // --- what gets recorded -----------------------------------------------------------------

    [Fact]
    public async Task Commenting_on_someone_elses_proposal_tells_them()
    {
        var author = await UserAsync("author");
        var reader = await UserAsync("reader");
        var (topicId, proposalId) = await ProposalAsync(author);

        await CommentAsync(topicId, proposalId, reader, "Have you considered the buses?");

        await using var context = database.CreateContext();
        var notification = await context.Notifications.AsNoTracking().SingleAsync();

        Assert.Equal(author, notification.UserId);
        Assert.Equal(NotificationKind.CommentOnMyProposal, notification.Kind);
        Assert.Contains("reader", notification.Summary);
        Assert.Equal($"/topics/{topicId}/proposals/{proposalId}", notification.Link);
        Assert.Null(notification.EmailedAtUtc);
    }

    [Fact]
    public async Task Commenting_on_your_own_proposal_tells_nobody()
    {
        // Being told about your own doing is noise, and noise is what makes people stop reading
        // notifications at all.
        var author = await UserAsync("author");
        var (topicId, proposalId) = await ProposalAsync(author);

        await CommentAsync(topicId, proposalId, author, "A note to myself");

        await using var context = database.CreateContext();
        Assert.Equal(0, await context.Notifications.CountAsync());
    }

    [Fact]
    public async Task A_notification_does_not_outlive_a_refused_comment()
    {
        // Both are in the same unit of work, so a refused comment cannot leave a notification
        // pointing at something that was never written.
        var author = await UserAsync("author");
        var reader = await UserAsync("reader");
        var (topicId, proposalId) = await ProposalAsync(author);

        using (var scope = Scope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<CommentService>()
                .PostToProposalAsync(topicId, proposalId, reader, "   ");

            Assert.False(result.Succeeded);
        }

        await using var context = database.CreateContext();
        Assert.Equal(0, await context.Notifications.CountAsync());
    }

    [Fact]
    public async Task Reporting_two_proposals_as_duplicates_tells_both_authors()
    {
        // The one worth hearing about promptly: enough agreement and the proposal folds out of
        // the pool's default view, so its author should know while they can still argue.
        var first = await UserAsync("first");
        var second = await UserAsync("second");
        var reporter = await UserAsync("reporter");

        var (topicId, firstProposal) = await ProposalAsync(first);

        Guid secondProposal;

        using (var scope = Scope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<ProposalService>()
                .CreateAsync(topicId, second, "A charge for driving in is suggested.", null);

            Assert.True(result.Succeeded, result.Error);
            secondProposal = result.Id;
        }

        using (var scope = Scope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<SimilarityService>()
                .ReportAsync(topicId, firstProposal, secondProposal, reporter, null,
                    "Both are a fee for entering the centre.");

            Assert.True(result.Succeeded, result.Error);
        }

        await using var context = database.CreateContext();
        var notified = await context.Notifications.AsNoTracking()
            .Where(n => n.Kind == NotificationKind.SimilarityOnMyProposal)
            .ToListAsync();

        Assert.Equal([first, second], notified.Select(n => n.UserId).Order());
        Assert.All(notified, n => Assert.Contains("reporter", n.Summary));
    }

    [Fact]
    public async Task Reporting_your_own_proposal_as_a_duplicate_does_not_tell_you()
    {
        var author = await UserAsync("author");
        var other = await UserAsync("other");

        var (topicId, mine) = await ProposalAsync(author);

        Guid theirs;

        using (var scope = Scope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<ProposalService>()
                .CreateAsync(topicId, other, "A charge for driving in is suggested.", null);

            theirs = result.Id;
        }

        using (var scope = Scope())
        {
            await scope.ServiceProvider.GetRequiredService<SimilarityService>()
                .ReportAsync(topicId, mine, theirs, author, null, "Mine says the same as theirs.");
        }

        await using var context = database.CreateContext();
        var notification = await context.Notifications.AsNoTracking().SingleAsync();

        Assert.Equal(other, notification.UserId);
    }

    [Fact]
    public async Task Turning_email_off_still_records_the_notification()
    {
        // The preference governs email only. The list is always there to look at, so turning
        // email off costs no information.
        var author = await UserAsync("author");
        var reader = await UserAsync("reader");
        var (topicId, proposalId) = await ProposalAsync(author);

        using (var scope = Scope())
        {
            await scope.ServiceProvider.GetRequiredService<NotificationService>()
                .SetDeliveryAsync(author, NotificationDelivery.None);
        }

        await CommentAsync(topicId, proposalId, reader, "Still worth telling you");

        using var readScope = Scope();
        var notifications = readScope.ServiceProvider.GetRequiredService<NotificationService>();

        Assert.Single(await notifications.ForUserAsync(author));
        Assert.Equal(1, await notifications.UnreadCountAsync(author));
    }

    [Fact]
    public async Task Marking_everything_read_clears_the_count()
    {
        var author = await UserAsync("author");
        var reader = await UserAsync("reader");
        var (topicId, proposalId) = await ProposalAsync(author);

        await CommentAsync(topicId, proposalId, reader, "Something to say");

        using var scope = Scope();
        var notifications = scope.ServiceProvider.GetRequiredService<NotificationService>();

        Assert.Equal(1, await notifications.UnreadCountAsync(author));

        await notifications.MarkAllReadAsync(author);

        Assert.Equal(0, await notifications.UnreadCountAsync(author));
        Assert.Empty(await notifications.ForUserAsync(author, unreadOnly: true));
    }

    // --- the outbox -------------------------------------------------------------------------

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = [];

        public bool CanDeliver => true;

        public Task SendAsync(string to, string subject, string body, CancellationToken _ = default)
        {
            Sent.Add((to, subject, body));
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// The dispatcher against a working mail host, which the application itself does not have.
    /// </summary>
    private static (NotificationDispatcher Dispatcher, ServiceProvider Provider) DispatcherWith(
        DatabaseFixture database, IEmailSender email, IClock clock)
    {
        var services = new ServiceCollection();

        services.AddDbContext<CdaDbContext>(options =>
            options.UseMySql(database.ConnectionString, DependencyInjection.ServerVersion));
        services.AddSingleton(clock);
        services.AddSingleton(email);

        var provider = services.BuildServiceProvider();

        return (
            new NotificationDispatcher(
                provider.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<NotificationDispatcher>.Instance),
            provider);
    }

    [Fact]
    public async Task With_no_mail_host_the_backlog_is_left_queued()
    {
        // Not stamping it means a mail host configured later delivers the backlog, rather than
        // the platform having quietly thrown it away.
        var author = await UserAsync("author");
        var reader = await UserAsync("reader");
        var (topicId, proposalId) = await ProposalAsync(author);

        await CommentAsync(topicId, proposalId, reader, "Something to say");

        using (var scope = Scope())
        {
            Assert.False(scope.ServiceProvider.GetRequiredService<IEmailSender>().CanDeliver);
        }

        await new NotificationDispatcher(
            _factory.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<NotificationDispatcher>.Instance)
            .DispatchAsync(default);

        await using var context = database.CreateContext();
        Assert.All(
            await context.Notifications.AsNoTracking().ToListAsync(),
            n => Assert.Null(n.EmailedAtUtc));
    }

    [Fact]
    public async Task Immediate_delivery_sends_as_soon_as_the_dispatcher_runs()
    {
        var author = await UserAsync("author");
        var reader = await UserAsync("reader");
        var (topicId, proposalId) = await ProposalAsync(author);

        using (var scope = Scope())
        {
            await scope.ServiceProvider.GetRequiredService<NotificationService>()
                .SetDeliveryAsync(author, NotificationDelivery.Immediate);
        }

        await CommentAsync(topicId, proposalId, reader, "Have you considered the buses?");

        var email = new RecordingEmailSender();
        var (dispatcher, provider) = DispatcherWith(database, email, new FixedClock(DateTime.UtcNow));

        await using (provider)
        {
            await dispatcher.DispatchAsync(default);
        }

        var sent = Assert.Single(email.Sent);
        Assert.Equal("author@example.com", sent.To);
        Assert.Contains("reader", sent.Subject);
        Assert.Contains($"/topics/{topicId}/proposals/{proposalId}", sent.Body);

        await using var context = database.CreateContext();
        Assert.All(
            await context.Notifications.AsNoTracking().ToListAsync(),
            n => Assert.NotNull(n.EmailedAtUtc));
    }

    [Fact]
    public async Task A_daily_digest_waits_for_its_window_and_then_goes_as_one_email()
    {
        // The default is a digest precisely so that a busy topic does not produce an inbox full
        // of separate messages.
        var author = await UserAsync("author");
        var reader = await UserAsync("reader");
        var (topicId, proposalId) = await ProposalAsync(author);

        await CommentAsync(topicId, proposalId, reader, "Have you considered the buses?");
        await CommentAsync(topicId, proposalId, reader, "And the delivery vans?");

        var email = new RecordingEmailSender();
        var clock = new FixedClock(DateTime.UtcNow);
        var (dispatcher, provider) = DispatcherWith(database, email, clock);

        await using (provider)
        {
            await dispatcher.DispatchAsync(default);

            Assert.Empty(email.Sent);

            await using (var pending = database.CreateContext())
            {
                Assert.Equal(2, await pending.Notifications.CountAsync(n => n.EmailedAtUtc == null));
            }

            clock.UtcNow = clock.UtcNow.Add(NotificationDispatcher.DigestWindow).AddMinutes(1);

            await dispatcher.DispatchAsync(default);
        }

        // One email covering both, rather than one each.
        var sent = Assert.Single(email.Sent);
        Assert.Contains("2 things happened", sent.Subject);
        Assert.Equal(2, sent.Body.Split("- reader commented on your proposal").Length - 1);
        Assert.Contains($"/topics/{topicId}/proposals/{proposalId}", sent.Body);

        await using var context = database.CreateContext();
        Assert.Equal(0, await context.Notifications.CountAsync(n => n.EmailedAtUtc == null));
    }

    [Fact]
    public async Task Someone_who_wants_no_email_leaves_the_queue_rather_than_being_reconsidered()
    {
        var author = await UserAsync("author");
        var reader = await UserAsync("reader");
        var (topicId, proposalId) = await ProposalAsync(author);

        using (var scope = Scope())
        {
            await scope.ServiceProvider.GetRequiredService<NotificationService>()
                .SetDeliveryAsync(author, NotificationDelivery.None);
        }

        await CommentAsync(topicId, proposalId, reader, "Something to say");

        var email = new RecordingEmailSender();
        var (dispatcher, provider) = DispatcherWith(database, email, new FixedClock(DateTime.UtcNow));

        await using (provider)
        {
            await dispatcher.DispatchAsync(default);
        }

        Assert.Empty(email.Sent);

        await using var context = database.CreateContext();
        Assert.All(
            await context.Notifications.AsNoTracking().ToListAsync(),
            n => Assert.NotNull(n.EmailedAtUtc));
    }

    [Fact]
    public async Task Nothing_is_emailed_twice()
    {
        var author = await UserAsync("author");
        var reader = await UserAsync("reader");
        var (topicId, proposalId) = await ProposalAsync(author);

        using (var scope = Scope())
        {
            await scope.ServiceProvider.GetRequiredService<NotificationService>()
                .SetDeliveryAsync(author, NotificationDelivery.Immediate);
        }

        await CommentAsync(topicId, proposalId, reader, "Have you considered the buses?");

        var email = new RecordingEmailSender();
        var (dispatcher, provider) = DispatcherWith(database, email, new FixedClock(DateTime.UtcNow));

        await using (provider)
        {
            await dispatcher.DispatchAsync(default);
            await dispatcher.DispatchAsync(default);
            await dispatcher.DispatchAsync(default);
        }

        Assert.Single(email.Sent);
    }

    // --- private messages -------------------------------------------------------------------

    [Fact]
    public async Task A_message_reaches_its_recipient_and_notifies_them()
    {
        var sender = await UserAsync("sender");
        var recipient = await UserAsync("recipient");

        using (var scope = Scope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<MessageService>()
                .SendAsync(sender, recipient, "Shall we work on the traffic topic together?");

            Assert.True(result.Succeeded, result.Error);
        }

        using var readScope = Scope();
        var messages = readScope.ServiceProvider.GetRequiredService<MessageService>();

        Assert.Equal(1, await messages.UnreadCountAsync(recipient));
        Assert.Equal(0, await messages.UnreadCountAsync(sender));

        var conversation = Assert.Single(await messages.ConversationsAsync(recipient));
        Assert.Equal("sender", conversation.WithDisplayName);
        Assert.Equal(1, conversation.Unread);

        await using var context = database.CreateContext();
        var notification = await context.Notifications.AsNoTracking().SingleAsync();

        Assert.Equal(recipient, notification.UserId);
        Assert.Equal(NotificationKind.PrivateMessage, notification.Kind);
    }

    [Fact]
    public async Task Reading_a_conversation_marks_only_what_was_addressed_to_the_reader()
    {
        // Opening your own sent message does not mean the other person has read it.
        var sender = await UserAsync("sender");
        var recipient = await UserAsync("recipient");

        using (var scope = Scope())
        {
            var messages = scope.ServiceProvider.GetRequiredService<MessageService>();
            await messages.SendAsync(sender, recipient, "From me to you");
            await messages.SendAsync(recipient, sender, "And back again");
        }

        using (var scope = Scope())
        {
            var thread = await scope.ServiceProvider.GetRequiredService<MessageService>()
                .ConversationAsync(sender, recipient);

            // Oldest first, and each one knows whose side it is on.
            Assert.Equal(["From me to you", "And back again"], thread.Select(m => m.Body));
            Assert.Equal([true, false], thread.Select(m => m.Mine));
        }

        await using var context = database.CreateContext();
        var mine = await context.PrivateMessages.AsNoTracking().SingleAsync(m => m.FromUserId == sender);
        var theirs = await context.PrivateMessages.AsNoTracking().SingleAsync(m => m.FromUserId == recipient);

        Assert.Null(mine.ReadAtUtc);
        Assert.NotNull(theirs.ReadAtUtc);
    }

    [Fact]
    public async Task A_message_cannot_be_sent_to_yourself_or_to_nobody()
    {
        var sender = await UserAsync("sender");

        using var scope = Scope();
        var messages = scope.ServiceProvider.GetRequiredService<MessageService>();

        Assert.False((await messages.SendAsync(sender, sender, "Talking to myself")).Succeeded);
        Assert.False((await messages.SendAsync(sender, Guid.NewGuid(), "Anyone there?")).Succeeded);

        await using var context = database.CreateContext();
        Assert.Equal(0, await context.PrivateMessages.CountAsync());
    }

    // --- attachments ------------------------------------------------------------------------

    private static Stream Content(string text) => new MemoryStream(Encoding.UTF8.GetBytes(text));

    [Fact]
    public async Task An_attachment_is_stored_under_a_generated_name()
    {
        // Nothing the uploader typed reaches the filesystem; the original name only labels the
        // download.
        var owner = await UserAsync("owner");
        var (topicId, proposalId) = await ProposalAsync(owner);

        using var scope = Scope();
        var attachments = scope.ServiceProvider.GetRequiredService<AttachmentService>();

        var result = await attachments.StoreAsync(
            topicId, proposalId, owner, "2024 traffic study.pdf", "application/pdf",
            Content("pretend pdf"), 11);

        Assert.True(result.Succeeded, result.Error);

        await using var context = database.CreateContext();
        var stored = await context.Attachments.AsNoTracking().SingleAsync();

        Assert.Equal("2024 traffic study.pdf", stored.FileName);
        Assert.DoesNotContain(' ', stored.StoredName);
        Assert.EndsWith(".pdf", stored.StoredName);

        var onDisk = Assert.Single(Directory.GetFiles(_uploads));
        Assert.Equal(stored.StoredName, Path.GetFileName(onDisk));

        var listed = Assert.Single(await attachments.ForProposalAsync(proposalId));
        Assert.Equal("2024 traffic study.pdf", listed.FileName);
        Assert.Equal("owner", listed.UploadedByDisplayName);
    }

    [Theory]
    [InlineData("payload.exe")]
    [InlineData("page.html")]
    [InlineData("noextension")]
    [InlineData("../../appsettings.json")]
    [InlineData("..\\..\\appsettings.json")]
    public async Task A_file_the_platform_will_not_accept_is_refused(string fileName)
    {
        var owner = await UserAsync("owner");
        var (topicId, proposalId) = await ProposalAsync(owner);

        using var scope = Scope();
        var result = await scope.ServiceProvider.GetRequiredService<AttachmentService>()
            .StoreAsync(topicId, proposalId, owner, fileName, "application/octet-stream", Content("x"), 1);

        Assert.False(result.Succeeded);

        await using var context = database.CreateContext();
        Assert.Equal(0, await context.Attachments.CountAsync());

        // Refused before anything was written, so there is nothing to clean up either.
        Assert.False(Directory.Exists(_uploads));
    }

    [Fact]
    public async Task A_file_over_the_cap_is_refused()
    {
        var owner = await UserAsync("owner");
        var (topicId, proposalId) = await ProposalAsync(owner);

        using var scope = Scope();
        var result = await scope.ServiceProvider.GetRequiredService<AttachmentService>()
            .StoreAsync(topicId, proposalId, owner, "huge.pdf", "application/pdf",
                Content("x"), Attachment.MaxSizeBytes + 1);

        Assert.False(result.Succeeded);
        Assert.Contains("MB", result.Error!);
    }

    [Fact]
    public async Task An_attachment_cannot_be_opened_through_another_topic()
    {
        // The download route checks access to the topic named in the URL, so the file must
        // belong to that topic — otherwise an id from a private discussion could be fetched by
        // quoting it against a public one.
        var owner = await UserAsync("owner");
        var (privateTopic, privateProposal) = await ProposalAsync(owner);
        var (otherTopic, _) = await ProposalAsync(owner);

        using var scope = Scope();
        var attachments = scope.ServiceProvider.GetRequiredService<AttachmentService>();

        var stored = await attachments.StoreAsync(
            privateTopic, privateProposal, owner, "confidential.pdf", "application/pdf",
            Content("secret"), 6);

        Assert.True(stored.Succeeded, stored.Error);

        Assert.Null(await attachments.OpenAsync(otherTopic, stored.Id));

        var opened = await attachments.OpenAsync(privateTopic, stored.Id);
        Assert.NotNull(opened);

        using var reader = new StreamReader(opened.Content);
        Assert.Equal("secret", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task A_proposal_in_another_topic_cannot_be_given_an_attachment()
    {
        var owner = await UserAsync("owner");
        var (_, victimProposal) = await ProposalAsync(owner);
        var (ownTopic, _) = await ProposalAsync(owner);

        using var scope = Scope();
        var result = await scope.ServiceProvider.GetRequiredService<AttachmentService>()
            .StoreAsync(ownTopic, victimProposal, owner, "notes.txt", "text/plain", Content("x"), 1);

        Assert.False(result.Succeeded);

        await using var context = database.CreateContext();
        Assert.Equal(0, await context.Attachments.CountAsync());
    }
}
