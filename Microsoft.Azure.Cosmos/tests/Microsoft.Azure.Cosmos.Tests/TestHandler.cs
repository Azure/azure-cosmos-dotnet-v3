namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    internal class TestHandler : RequestHandler
    {
        private readonly Func<RequestMessage,
            CancellationToken, Task<ResponseMessage>> _handlerFunc;

        public TestHandler()
            : this((r, c) => ReturnSuccess())
        {
        }

        public TestHandler(Func<RequestMessage, CancellationToken, Task<ResponseMessage>> handlerFunc)
        {
            this._handlerFunc = handlerFunc;
        }

        public override Task<ResponseMessage> SendAsync(
            RequestMessage request, 
            CancellationToken cancellationToken)
        {
            return this._handlerFunc(request, cancellationToken);
        }

        public static Task<ResponseMessage> ReturnSuccess()
        {
            return Task.Factory.StartNew(
                () =>
                {
                    ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new MemoryStream(Encoding.UTF8.GetBytes(@"{ ""Documents"": [{ ""id"": ""Test""}]}"))
                    };
                    return responseMessage;
                });
        }

        public static Task<ResponseMessage> ReturnStatusCode(
            HttpStatusCode statusCode, 
            SubStatusCodes subStatusCode = SubStatusCodes.Unknown)
        {
            return Task.Factory.StartNew(
                () =>
                {
                    ResponseMessage httpResponse = new ResponseMessage(statusCode);
                    if (subStatusCode != SubStatusCodes.Unknown)
                    {
                        httpResponse.Headers.Add(
                            WFConstants.BackendHeaders.SubStatus, 
                            ((uint)subStatusCode).ToString(CultureInfo.InvariantCulture));
                    }

                    httpResponse.Content = new MemoryStream(Encoding.UTF8.GetBytes("{}"));

                    return httpResponse;
                });
        }
    }
}
