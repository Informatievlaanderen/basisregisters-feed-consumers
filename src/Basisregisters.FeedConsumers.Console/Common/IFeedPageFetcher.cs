namespace Basisregisters.FeedConsumers.Console.Common;

using System.Threading;
using System.Threading.Tasks;
using static FeedProjectorBase;

public interface IFeedPageFetcher
{
    Task<CloudEventsResult> FetchAsync(int page, CancellationToken cancellationToken);
}
