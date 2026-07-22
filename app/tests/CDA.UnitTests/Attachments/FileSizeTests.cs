using System.Globalization;
using CDA.Web.Models;

namespace CDA.UnitTests.Attachments;

/// <summary>
/// The culture is pinned in each test rather than left to the machine.
/// </summary>
/// <remarks>
/// The decimal separator is a cultural choice, and this developer's machine happens to be Greek
/// — an assertion against "1.5 KB" would pass in London and fail here, which says nothing about
/// the code. The formatter deliberately follows the reader's culture; what is asserted is that
/// it picks a sensible unit.
/// </remarks>
public class FileSizeTests
{
    private static string Describe(long bytes, string culture)
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);

        try
        {
            return FileSize.Describe(bytes);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData(0, "0 bytes")]
    [InlineData(96, "96 bytes")]
    [InlineData(1023, "1023 bytes")]
    public void A_small_file_is_reported_in_bytes(long bytes, string expected)
    {
        // "0 KB" reads as empty rather than small, which is what dividing straight to
        // kilobytes produces for anything under a kilobyte.
        Assert.Equal(expected, Describe(bytes, "en-GB"));
    }

    [Theory]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(20 * 1024, "20 KB")]
    public void A_middling_file_is_reported_in_kilobytes(long bytes, string expected)
    {
        Assert.Equal(expected, Describe(bytes, "en-GB"));
    }

    [Theory]
    [InlineData(1024 * 1024, "1 MB")]
    [InlineData(10 * 1024 * 1024, "10 MB")]
    public void A_large_file_is_reported_in_megabytes(long bytes, string expected)
    {
        Assert.Equal(expected, Describe(bytes, "en-GB"));
    }

    [Fact]
    public void The_number_follows_the_readers_culture()
    {
        // Greek writes the decimal separator as a comma. Phase 13 makes this visible; the
        // formatter is ready for it because it never hard-codes the invariant culture.
        Assert.Equal("1,5 KB", Describe(1536, "el-GR"));
    }
}
