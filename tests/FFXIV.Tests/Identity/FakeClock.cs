namespace FFXIV.Tests.Identity;

/// <summary>A <see cref="TimeProvider"/> whose UTC time the test moves by hand.</summary>
internal sealed class FakeClock : TimeProvider
{
    public FakeClock(DateTimeOffset now)
    {
        Now = now;
    }

    public DateTimeOffset Now { get; set; }

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan by) => Now += by;
}
