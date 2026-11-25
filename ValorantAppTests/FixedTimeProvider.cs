using System;

namespace ValorantAppTests
{
    /// <summary>Test clock: reports a fixed instant until <see cref="Set"/> moves it.</summary>
    public sealed class FixedTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow() => _now;

        public void Set(DateTimeOffset now) => _now = now;
    }
}
