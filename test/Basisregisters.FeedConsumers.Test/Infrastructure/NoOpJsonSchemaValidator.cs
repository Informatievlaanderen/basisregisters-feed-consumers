namespace Basisregisters.FeedConsumers.Test.Infrastructure;

using System.Threading;
using System.Threading.Tasks;
using CloudNative.CloudEvents;
using FeedConsumers.Console.Common;

public class NoOpJsonSchemaValidator : IJsonSchemaValidator
{
    public Task ValidateAsync(CloudEvent cloudEvent, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
