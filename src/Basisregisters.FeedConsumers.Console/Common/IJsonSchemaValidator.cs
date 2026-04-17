namespace Basisregisters.FeedConsumers.Console.Common;

using System.Threading;
using System.Threading.Tasks;
using CloudNative.CloudEvents;

public interface IJsonSchemaValidator
{
    Task ValidateAsync(CloudEvent cloudEvent, CancellationToken cancellationToken);
}
