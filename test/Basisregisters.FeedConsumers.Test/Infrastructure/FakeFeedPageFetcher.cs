namespace Basisregisters.FeedConsumers.Test.Infrastructure;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Console.Common;
using static Console.Common.FeedProjectorBase;

public class FakeFeedPageFetcher : IFeedPageFetcher
{
    private readonly Dictionary<int, CloudEventsResult> _pages = new();
    private int _fetchCount;

    public int FetchCount => _fetchCount;

    public void SetupPage(int page, CloudEventsResult result)
    {
        _pages[page] = result;
    }

    public Task<CloudEventsResult> FetchAsync(int page, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _fetchCount);

        if (_pages.TryGetValue(page, out var result))
            return Task.FromResult(result);

        return Task.FromResult(new CloudEventsResult([], false));
    }
}
