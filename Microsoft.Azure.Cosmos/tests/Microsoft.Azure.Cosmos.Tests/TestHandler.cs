namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Globalization;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;

    internal class TestHandler : CosmosRequestHandler
    {
        private readonly Func<CosmosRequestMessage,
            CancellationToken, Task<CosmosResponseMessage>> _handlerFunc;

        public TestHandler()
        {
            _handlerFunc = (r, c) => ReturnSuccess();
        }

        public TestHandler(Func<CosmosRequestMessage,
            CancellationToken, Task<CosmosResponseMessage>> handlerFunc)
        {
            _handlerFunc = handlerFunc;
        }

        public override Task<CosmosResponseMessage> SendAsync(
            CosmosRequestMessage request, CancellationToken cancellationToken)
        {
            return _handlerFunc(request, cancellationToken);
        }

        public static Task<CosmosResponseMessage> ReturnSuccess()
        {
            return Task.Factory.StartNew(
                () => new CosmosResponseMessage(HttpStatusCode.OK));
        }

        public static Task<CosmosResponseMessage> ReturnStatusCode(HttpStatusCode statusCode, SubStatusCodes subStatusCode = SubStatusCodes.Unknown)
        {
            return Task.Factory.StartNew(
                () =>
                {
                    CosmosResponseMessage httpResponse = new CosmosResponseMessage(statusCode);
                    if (subStatusCode != SubStatusCodes.Unknown)
                    {
                        httpResponse.Headers.Add(WFConstants.BackendHeaders.SubStatus,((uint)subStatusCode).ToString(CultureInfo.InvariantCulture));
                    }

                    return httpResponse;
                });
        }
    }
}
