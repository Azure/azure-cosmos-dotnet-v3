namespace Cosmos.Samples.Handlers
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Polly;

    /// <summary>
    /// Using Polly to retry on Throttles.
    /// </summary>
    class ThrottlingHandler : CosmosRequestHandler
    {
        public override Task<CosmosResponseMessage> SendAsync(
            CosmosRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Policy
                .HandleResult<CosmosResponseMessage>(r => (int)r.StatusCode == 429)
                .RetryAsync(3)
                .ExecuteAsync(() => base.SendAsync(request, cancellationToken));
        }
    }
}
