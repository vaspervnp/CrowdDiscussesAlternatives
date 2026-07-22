using CDA.Application.Abstractions;
using CDA.Domain.Attachments;
using CDA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CDA.Infrastructure.Attachments;

/// <summary>Where uploaded files are kept.</summary>
public sealed class AttachmentOptions
{
    public const string SectionName = "Attachments";

    /// <summary>
    /// A directory the web server does not publish.
    /// </summary>
    /// <remarks>
    /// Pointing this inside <c>wwwroot</c> would undo the whole design: files would be reachable
    /// by URL without any access check, and a private topic's attachments would be one guess
    /// away from anyone.
    /// </remarks>
    public string RootPath { get; set; } = "App_Data/attachments";
}

public sealed record AttachmentView(
    Guid Id, string FileName, long SizeBytes, string UploadedByDisplayName, DateTime UploadedAtUtc);

/// <summary>A file ready to be streamed back, with the name to label it.</summary>
public sealed record AttachmentDownload(Stream Content, string FileName, string ContentType);

public sealed record AttachmentResult(bool Succeeded, Guid Id = default, string? Error = null)
{
    public static AttachmentResult Ok(Guid id) => new(true, id);

    public static AttachmentResult Refused(string reason) => new(false, Error: reason);
}

public sealed class AttachmentService(
    CdaDbContext database,
    AttachmentOptions options,
    IClock clock,
    ILogger<AttachmentService> logger)
{
    public async Task<AttachmentResult> StoreAsync(
        Guid topicId,
        Guid proposalId,
        Guid userId,
        string fileName,
        string contentType,
        Stream content,
        long sizeBytes,
        CancellationToken cancellationToken = default)
    {
        if (sizeBytes <= 0)
        {
            return AttachmentResult.Refused("That file is empty.");
        }

        if (sizeBytes > Attachment.MaxSizeBytes)
        {
            return AttachmentResult.Refused(
                $"Files are limited to {Attachment.MaxSizeBytes / (1024 * 1024)} MB. " +
                "If it is on the web already, cite it as a source instead.");
        }

        if (!Attachment.IsAllowed(fileName))
        {
            return AttachmentResult.Refused(
                "That kind of file is not accepted. Documents, spreadsheets, images, PDFs and " +
                "plain text are.");
        }

        // Scoped to the topic the caller was authorised against.
        var proposal = await database.Proposals
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == proposalId && p.TopicId == topicId, cancellationToken);

        if (proposal is null)
        {
            return AttachmentResult.Refused("No such proposal.");
        }

        var topic = await database.Topics
            .AsNoTracking()
            .SingleAsync(t => t.Id == topicId, cancellationToken);

        if (topic.IsClosedAt(clock.UtcNow))
        {
            return AttachmentResult.Refused("This topic has closed.");
        }

        // The name on disk is generated. Nothing the uploader typed reaches the filesystem —
        // "../../appsettings.json" is a path traversal and "index.html" invites the file to be
        // served as a page. The original name is kept in the database to label the download.
        var storedName = $"{Guid.NewGuid():N}{Path.GetExtension(fileName).ToLowerInvariant()}";
        var directory = Path.GetFullPath(options.RootPath);
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, storedName);

        await using (var file = File.Create(path))
        {
            await content.CopyToAsync(file, cancellationToken);
        }

        var attachment = new Attachment(
            Guid.NewGuid(), topicId, proposalId, userId, fileName, contentType,
            storedName, sizeBytes, clock.UtcNow);

        database.Attachments.Add(attachment);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Do not leave an orphan on disk that nothing points at.
            TryDelete(path);
            throw;
        }

        return AttachmentResult.Ok(attachment.Id);
    }

    public async Task<List<AttachmentView>> ForProposalAsync(
        Guid proposalId,
        CancellationToken cancellationToken = default)
    {
        var rows = await database.Attachments
            .AsNoTracking()
            .Where(a => a.ProposalId == proposalId)
            .OrderBy(a => a.UploadedAtUtc)
            .Join(database.UserProfiles.AsNoTracking(),
                a => a.UploadedByUserId, profile => profile.Id,
                (a, profile) => new { a.Id, a.FileName, a.SizeBytes, profile.DisplayName, a.UploadedAtUtc })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new AttachmentView(
            r.Id, r.FileName, r.SizeBytes, r.DisplayName, r.UploadedAtUtc))];
    }

    /// <summary>
    /// Opens an attachment for reading, if it belongs to the topic the caller was cleared for.
    /// </summary>
    /// <remarks>
    /// The caller checks topic access; this checks that the file actually belongs to that topic,
    /// so an attachment id from a private discussion cannot be fetched by quoting it against a
    /// public one.
    /// </remarks>
    public async Task<AttachmentDownload?> OpenAsync(
        Guid topicId,
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        var attachment = await database.Attachments
            .AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == attachmentId && a.TopicId == topicId, cancellationToken);

        if (attachment is null)
        {
            return null;
        }

        // Combined with the generated name, never with anything from the request.
        var path = Path.Combine(Path.GetFullPath(options.RootPath), attachment.StoredName);

        if (!File.Exists(path))
        {
            logger.LogError(
                "Attachment {Id} is recorded but its file is missing at {Path}", attachmentId, path);
            return null;
        }

        return new AttachmentDownload(
            File.OpenRead(path), attachment.FileName, attachment.ContentType);
    }

    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception error)
        {
            logger.LogError(error, "Could not remove the orphaned upload at {Path}", path);
        }
    }
}
