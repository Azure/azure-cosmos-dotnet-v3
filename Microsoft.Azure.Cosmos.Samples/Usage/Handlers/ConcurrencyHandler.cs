namespace Cosmos.Samples.Handlers
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// Handler that detects concurrency and etag issues
    /// </summary>
    class ConcurrencyHandler : RequestHandler
    {
        public override async Task<ResponseMessage> SendAsync(
            RequestMessage request,
            CancellationToken cancellationToken)
        {

            ResponseMessage response = await base.SendAsync(request, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
            {
                response.Headers.Set("x-ms-substatus", "999");
            }

            return response;
        }
    }
}
