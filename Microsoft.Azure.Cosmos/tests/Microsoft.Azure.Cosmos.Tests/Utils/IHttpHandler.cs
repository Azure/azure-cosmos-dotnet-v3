namespace Microsoft.Azure.Cosmos.Tests
{
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IHttpHandler
    {
        Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken);
    }
}