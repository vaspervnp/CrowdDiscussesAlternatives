namespace CDA.Application.Abstractions;

/// <summary>
/// The application's source of time.
/// </summary>
/// <remarks>
/// The database server runs on a local (CEST) clock with <c>time_zone=SYSTEM</c>, so
/// server-side <c>NOW()</c> is not UTC and must never be used for stored timestamps.
/// Every timestamp the application persists comes from here, in UTC, which also keeps
/// time-dependent rules — proposal edit windows, topic closing dates — testable.
/// </remarks>
public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
