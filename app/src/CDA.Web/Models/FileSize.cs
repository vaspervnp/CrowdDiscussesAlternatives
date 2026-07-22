namespace CDA.Web.Models;

/// <summary>How a file's size is written out for a reader.</summary>
public static class FileSize
{
    /// <summary>
    /// Renders a byte count in the largest unit that leaves a number worth reading.
    /// </summary>
    /// <remarks>
    /// Dividing straight to kilobytes reports a small text file as "0 KB", which reads as
    /// "empty" rather than "small".
    /// </remarks>
    public static string Describe(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} bytes",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes / (1024.0 * 1024.0):0.#} MB",
    };
}
