namespace ProofFit.Api.Domain;

public static class Timestamp
{
    /// <summary>
    /// The current UTC time at the precision PostgreSQL stores (microseconds). .NET ticks are
    /// finer, so without truncating, a value returned from a write would differ from the
    /// same row read back later.
    /// </summary>
    public static DateTimeOffset Now()
    {
        var now = DateTimeOffset.UtcNow;
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond));
    }
}
