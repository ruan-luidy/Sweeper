namespace Sweeper.Core.Tests;

// Relógio que só anda quando o teste manda.
public sealed class ManualTimeProvider : TimeProvider
{
  private long _ticks;

  public override long TimestampFrequency => TimeSpan.TicksPerSecond;

  public override long GetTimestamp() => _ticks;

  public void Advance(TimeSpan amount) => _ticks += amount.Ticks;
}
