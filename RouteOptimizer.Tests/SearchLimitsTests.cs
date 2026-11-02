using RouteOptimizer.Application;

namespace RouteOptimizer.Tests;

public sealed class SearchLimitsTests
{
    private sealed class ManualClock : TimeProvider
    {
        public long Timestamp { get; set; }
        public long IncrementPerRead { get; set; }
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() { var value = Timestamp; Timestamp += IncrementPerRead; return value; }
    }

    [Fact]
    public void TimeBudget_CountsActiveCalculationButNotManualIdleTime()
    {
        var clock = new ManualClock();
        var solver = new LittleSolver(clock);
        var idle = solver.CreateSession(ProblemSamples.TeachingExample());
        clock.Timestamp += TimeSpan.FromHours(2).Ticks;
        idle.Advance();
        Assert.Equal(SearchStatus.Running, idle.Result.Status);
        Assert.Equal(TimeSpan.Zero, idle.Result.Statistics.Elapsed);

        clock.IncrementPerRead = TimeSpan.TicksPerSecond;
        var active = solver.CreateSession(ProblemSamples.TeachingExample(), new SearchOptions(TimeSpan.FromSeconds(2)));
        active.Advance();
        Assert.Equal(SearchStatus.Stopped, active.Result.Status);
        Assert.Equal(StopReason.TimeLimit, active.Result.StopReason);
        Assert.Equal(TimeSpan.FromSeconds(2), active.Result.Statistics.Elapsed);
        Assert.Equal(36, active.Result.LowerBound);
    }

    [Fact]
    public void OpenNodeLimit_RetainsParentAndValidGlobalBound()
    {
        var session = new LittleSolver().CreateSession(
            ProblemSamples.TeachingExample(), new SearchOptions(MaxOpenNodes: 1));
        session.Advance();
        Assert.Equal(SearchStatus.Stopped, session.Result.Status);
        Assert.Equal(StopReason.OpenNodeLimit, session.Result.StopReason);
        Assert.Equal(1, session.Result.Statistics.Open);
        Assert.Equal(1, session.Result.Statistics.MaxOpen);
        Assert.Equal(36, session.Result.LowerBound);
        Assert.Null(session.Result.BestRoute);
    }

    [Theory]
    [InlineData(0, 10, 10)]
    [InlineData(1000, 0, 10)]
    [InlineData(1000, 10, -1)]
    public void InvalidSearchLimits_AreRejected(int milliseconds, int nodes, int trace)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LittleSolver().CreateSession(
            ProblemSamples.TeachingExample(), new SearchOptions(TimeSpan.FromMilliseconds(milliseconds), nodes, trace)));
    }
}
