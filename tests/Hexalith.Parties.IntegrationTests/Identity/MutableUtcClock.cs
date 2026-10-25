namespace Hexalith.Parties.IntegrationTests.Identity;

/// <summary>Test clock with a movable UTC instant and a real elapsed-time budget.</summary>
internal sealed class MutableUtcClock : TimeProvider
{
    /// <summary>Gets or sets the instant returned by <see cref="GetUtcNow"/>.</summary>
    public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => UtcNow;
}
