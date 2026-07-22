using CDA.Domain.Attachments;

namespace CDA.UnitTests.Attachments;

/// <summary>
/// The allowlist is the difference between an upload feature and an arbitrary file host, so it
/// gets checked rather than assumed.
/// </summary>
public class AttachmentRulesTests
{
    [Theory]
    [InlineData("report.pdf")]
    [InlineData("figures.csv")]
    [InlineData("photo.JPG")]
    [InlineData("notes.md")]
    [InlineData("budget.xlsx")]
    public void Ordinary_documents_are_accepted(string fileName)
    {
        Assert.True(Attachment.IsAllowed(fileName));
    }

    [Theory]
    [InlineData("payload.exe")]
    [InlineData("script.js")]
    [InlineData("shell.sh")]
    [InlineData("page.html")]
    [InlineData("drawing.svg")]
    [InlineData("macro.docm")]
    [InlineData("library.dll")]
    [InlineData("config.aspx")]
    public void Anything_that_could_run_is_refused(string fileName)
    {
        // html and svg are on this list for the same reason as exe: served from this origin
        // they would run their own script against a signed-in reader's session.
        Assert.False(Attachment.IsAllowed(fileName));
    }

    [Theory]
    [InlineData("noextension")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    public void A_name_with_no_usable_extension_is_refused(string fileName)
    {
        Assert.False(Attachment.IsAllowed(fileName));
    }

    [Theory]
    [InlineData("report.pdf.exe")]
    [InlineData("innocent.png.js")]
    public void Only_the_final_extension_counts(string fileName)
    {
        // A double extension is the oldest trick there is; what matters is what the system
        // would actually run.
        Assert.False(Attachment.IsAllowed(fileName));
    }

    [Fact]
    public void The_allowlist_names_what_is_permitted_rather_than_what_is_not()
    {
        // A blocklist has to anticipate every dangerous extension and is wrong the moment a new
        // one appears; this is wrong only by being inconvenient.
        Assert.NotEmpty(Attachment.AllowedExtensions);
        Assert.All(Attachment.AllowedExtensions, extension => Assert.StartsWith(".", extension));
    }

    [Fact]
    public void The_size_cap_is_modest_on_purpose()
    {
        // A reference is a link; an attachment is for the thing that has no link.
        Assert.Equal(10 * 1024 * 1024, Attachment.MaxSizeBytes);
    }
}
