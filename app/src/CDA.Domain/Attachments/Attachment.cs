namespace CDA.Domain.Attachments;

/// <summary>
/// A file supporting a proposal, held on the filesystem behind the application.
/// </summary>
/// <remarks>
/// <para>
/// The file itself never sits in a folder the web server publishes. It is written under a
/// private root with a generated name, and reaching it goes through a controller that checks
/// the caller may read the topic first. Serving uploads as static files is how an upload
/// feature becomes an arbitrary-file-host, and a private topic's attachments would be one
/// guessed URL away from anyone.
/// </para>
/// <para>
/// The stored name is a generated identifier, never anything the uploader supplied. A name
/// like <c>../../appsettings.json</c> is a path traversal; one like <c>index.html</c> invites
/// the file to be served as a page. The original name is kept only to label the download.
/// </para>
/// </remarks>
public sealed class Attachment
{
    public const int FileNameMaxLength = 260;

    /// <summary>The largest upload accepted.</summary>
    /// <remarks>
    /// A reference is a link; an attachment is for the thing that has no link. Ten megabytes
    /// covers a scanned report and stops the disk being treated as storage.
    /// </remarks>
    public const long MaxSizeBytes = 10 * 1024 * 1024;

    /// <summary>
    /// Extensions accepted, as an allowlist.
    /// </summary>
    /// <remarks>
    /// An allowlist, not a blocklist: a blocklist has to anticipate every dangerous extension
    /// and is wrong the moment a new one appears, while this is wrong only by being
    /// inconvenient. Nothing executable, nothing that a browser will run as script.
    /// </remarks>
    public static readonly IReadOnlySet<string> AllowedExtensions = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".txt", ".csv", ".md",
        ".png", ".jpg", ".jpeg", ".gif", ".webp",
        ".odt", ".ods", ".odp", ".docx", ".xlsx", ".pptx",
    };

    private Attachment()
    {
        // EF Core.
        FileName = null!;
        ContentType = null!;
        StoredName = null!;
    }

    public Attachment(
        Guid id,
        Guid topicId,
        Guid proposalId,
        Guid uploadedByUserId,
        string fileName,
        string contentType,
        string storedName,
        long sizeBytes,
        DateTime uploadedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(storedName);

        Id = id;
        TopicId = topicId;
        ProposalId = proposalId;
        UploadedByUserId = uploadedByUserId;
        FileName = fileName.Trim();
        ContentType = contentType;
        StoredName = storedName;
        SizeBytes = sizeBytes;
        UploadedAtUtc = uploadedAtUtc;
    }

    public Guid Id { get; private set; }

    /// <summary>Carried so access can be checked without loading the proposal.</summary>
    public Guid TopicId { get; private set; }

    public Guid ProposalId { get; private set; }

    public Guid UploadedByUserId { get; private set; }

    /// <summary>What the uploader called it. Shown to people; never used as a path.</summary>
    public string FileName { get; private set; }

    public string ContentType { get; private set; }

    /// <summary>The generated name on disk.</summary>
    public string StoredName { get; private set; }

    public long SizeBytes { get; private set; }

    public DateTime UploadedAtUtc { get; private set; }

    /// <summary>Whether a name is one this platform will accept.</summary>
    public static bool IsAllowed(string fileName) =>
        !string.IsNullOrWhiteSpace(fileName)
        && AllowedExtensions.Contains(Path.GetExtension(fileName));
}
