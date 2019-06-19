namespace Cosmos.Samples.Handlers
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Polly;

    /// <summary>
    /// Using Polly to retry on Throttles.
    /// </summary>
    class ThrottlingHandler : RequestHandler
    {
        public override Task<ResponseMessage> SendAsync(
            RequestMessage request,
            CancellationToken cancellationToken)
        {
            return Policy
                .HandleResult<ResponseMessage>(r => (int)r.StatusCode == 429)
                .RetryAsync(3)
                .ExecuteAsync(() => base.SendAsync(request, cancellationToken));
        }
    }
}
