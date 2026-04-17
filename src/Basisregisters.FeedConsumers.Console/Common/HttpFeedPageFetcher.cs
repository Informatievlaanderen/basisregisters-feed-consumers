namespace Basisregisters.FeedConsumers.Console.Common;

using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using static FeedProjectorBase;

public class HttpFeedPageFetcher : IFeedPageFetcher
{
    private readonly HttpClient _httpClient;
    private readonly string _feedUrl;

    public HttpFeedPageFetcher(HttpClient httpClient, string feedUrl)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _feedUrl = feedUrl ?? throw new ArgumentNullException(nameof(feedUrl));
    }

    public async Task<CloudEventsResult> FetchAsync(int page, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(_feedUrl + $"?pagina={page}", cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var events = await CloudEventReader.ReadBatchAsync(contentStream, cancellationToken);

        return new CloudEventsResult(events, response.Headers.TryGetValues(PageCompleteHeader, out var values)
                                             && values.FirstOrDefault()?.Equals("true", StringComparison.InvariantCultureIgnoreCase) == true);
    }
}
