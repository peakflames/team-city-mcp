namespace TeamCityRemoteMcpServer.Tests;

public class TtlCacheTests
{
    [Fact]
    public async Task GetOrAddAsync_SecondCallWithinTtl_DoesNotInvokeFactoryAgain()
    {
        var time = new FakeTimeProvider();
        var cache = new TtlCache<string, int>(capacity: 10, time, NullLogger.Instance, "test-cache");
        var calls = 0;

        Task<int> Factory(CancellationToken _)
        {
            calls++;
            return Task.FromResult(42);
        }

        var first = await cache.GetOrAddAsync("k", TimeSpan.FromSeconds(60), Factory, CancellationToken.None);
        var second = await cache.GetOrAddAsync("k", TimeSpan.FromSeconds(60), Factory, CancellationToken.None);

        Assert.Equal(42, first);
        Assert.Equal(42, second);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task GetOrAddAsync_AfterTtlExpires_InvokesFactoryAgain()
    {
        var time = new FakeTimeProvider();
        var cache = new TtlCache<string, int>(capacity: 10, time, NullLogger.Instance, "test-cache");
        var calls = 0;

        Task<int> Factory(CancellationToken _)
        {
            calls++;
            return Task.FromResult(calls);
        }

        var first = await cache.GetOrAddAsync("k", TimeSpan.FromSeconds(60), Factory, CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(61));
        var second = await cache.GetOrAddAsync("k", TimeSpan.FromSeconds(60), Factory, CancellationToken.None);

        Assert.Equal(1, first);
        Assert.Equal(2, second);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task GetOrAddAsync_ConcurrentIdenticalRequests_ShareOneUpstreamCall()
    {
        var time = new FakeTimeProvider();
        var cache = new TtlCache<string, int>(capacity: 10, time, NullLogger.Instance, "test-cache");
        var calls = 0;
        var gate = new TaskCompletionSource();

        async Task<int> Factory(CancellationToken _)
        {
            Interlocked.Increment(ref calls);
            await gate.Task;
            return 99;
        }

        var task1 = cache.GetOrAddAsync("k", TimeSpan.FromSeconds(60), Factory, CancellationToken.None).AsTask();
        var task2 = cache.GetOrAddAsync("k", TimeSpan.FromSeconds(60), Factory, CancellationToken.None).AsTask();
        var task3 = cache.GetOrAddAsync("k", TimeSpan.FromSeconds(60), Factory, CancellationToken.None).AsTask();

        gate.SetResult();
        var results = await Task.WhenAll(task1, task2, task3);

        Assert.Equal(1, calls);
        Assert.All(results, r => Assert.Equal(99, r));
    }

    [Fact]
    public async Task GetOrAddAsync_FactoryThrows_IsNeverCached_AndEachRetryInvokesTheFactoryAgain()
    {
        var time = new FakeTimeProvider();
        var cache = new TtlCache<string, int>(capacity: 10, time, NullLogger.Instance, "test-cache");
        var calls = 0;

        Task<int> Factory(CancellationToken _)
        {
            calls++;
            if (calls == 1)
                throw new InvalidOperationException("boom");
            return Task.FromResult(7);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => cache.GetOrAddAsync("k", TimeSpan.FromSeconds(60), Factory, CancellationToken.None).AsTask());

        var second = await cache.GetOrAddAsync("k", TimeSpan.FromSeconds(60), Factory, CancellationToken.None);

        Assert.Equal(7, second);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task GetOrAddAsync_WaiterCancelled_DoesNotCancelTheSharedUpstreamCall()
    {
        var time = new FakeTimeProvider();
        var cache = new TtlCache<string, int>(capacity: 10, time, NullLogger.Instance, "test-cache");
        var gate = new TaskCompletionSource();
        var calls = 0;

        async Task<int> Factory(CancellationToken ct)
        {
            Interlocked.Increment(ref calls);
            await gate.Task;
            return 5;
        }

        using var cts = new CancellationTokenSource();
        var cancelledWaiter = cache.GetOrAddAsync("k", TimeSpan.FromSeconds(60), Factory, cts.Token).AsTask();
        var otherWaiter = cache.GetOrAddAsync("k", TimeSpan.FromSeconds(60), Factory, CancellationToken.None).AsTask();

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelledWaiter);

        gate.SetResult();
        var result = await otherWaiter;

        Assert.Equal(5, result);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task GetOrAddAsync_AtCapacity_ServesUncached_WithoutEvictingExistingEntries()
    {
        var time = new FakeTimeProvider();
        var cache = new TtlCache<string, int>(capacity: 1, time, NullLogger.Instance, "test-cache");
        var callsA = 0;
        var callsB = 0;

        var a1 = await cache.GetOrAddAsync("a", TimeSpan.FromSeconds(60), _ => { callsA++; return Task.FromResult(1); }, CancellationToken.None);
        var b1 = await cache.GetOrAddAsync("b", TimeSpan.FromSeconds(60), _ => { callsB++; return Task.FromResult(2); }, CancellationToken.None);
        var b2 = await cache.GetOrAddAsync("b", TimeSpan.FromSeconds(60), _ => { callsB++; return Task.FromResult(2); }, CancellationToken.None);
        var a2 = await cache.GetOrAddAsync("a", TimeSpan.FromSeconds(60), _ => { callsA++; return Task.FromResult(1); }, CancellationToken.None);

        Assert.Equal(1, a1);
        Assert.Equal(2, b1);
        Assert.Equal(2, b2);
        Assert.Equal(1, a2);
        Assert.Equal(1, callsA); // "a" stayed cached (it got the one slot first).
        Assert.Equal(2, callsB); // "b" was refused an insert every time — served uncached, not denied.
    }

    [Fact]
    public async Task EvictExpiredAsync_RemovesOnlyExpiredEntries()
    {
        var time = new FakeTimeProvider();
        var cache = new TtlCache<string, int>(capacity: 10, time, NullLogger.Instance, "test-cache");

        await cache.GetOrAddAsync("short", TimeSpan.FromSeconds(10), _ => Task.FromResult(1), CancellationToken.None);
        await cache.GetOrAddAsync("long", TimeSpan.FromSeconds(1000), _ => Task.FromResult(2), CancellationToken.None);

        time.Advance(TimeSpan.FromSeconds(11));
        var evicted = await cache.EvictExpiredAsync(CancellationToken.None);

        Assert.Equal(1, evicted);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public async Task GetOrAddAsync_PerEntryTtlOverload_UsesTheTtlTheFactoryReturns()
    {
        var time = new FakeTimeProvider();
        var cache = new TtlCache<string, string>(capacity: 10, time, NullLogger.Instance, "test-cache");
        var calls = 0;

        Task<(string Value, TimeSpan Ttl)> Factory(CancellationToken _)
        {
            calls++;
            return Task.FromResult(calls == 1 ? ("negative", TimeSpan.FromSeconds(5)) : ("positive", TimeSpan.FromSeconds(500)));
        }

        var first = await cache.GetOrAddAsync("k", Factory, CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(6));
        var second = await cache.GetOrAddAsync("k", Factory, CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(6));
        var third = await cache.GetOrAddAsync("k", Factory, CancellationToken.None);

        Assert.Equal("negative", first);
        Assert.Equal("positive", second);
        Assert.Equal("positive", third); // still within the 500s positive TTL
        Assert.Equal(2, calls);
    }
}
