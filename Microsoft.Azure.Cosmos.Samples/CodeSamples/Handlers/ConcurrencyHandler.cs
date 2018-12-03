using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

namespace Cosmos.Samples.Handlers
{
    /// <summary>
    /// Handler that detects concurrency and etag issues
    /// </summary>
    class ConcurrencyHandler: CosmosRequestHandler
    {
        public override async Task<CosmosResponseMessage> SendAsync(
            CosmosRequestMessage request,
            CancellationToken cancellationToken)
        {

            var response = await base.SendAsync(request, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
            {
                response.Headers.Set("x-ms-substatus", "999");
            }

            return response;
        }
    }
}
