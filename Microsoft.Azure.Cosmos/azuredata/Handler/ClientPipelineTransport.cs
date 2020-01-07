// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Threading.Tasks;
    using Azure.Core;
    using Azure.Core.Pipeline;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Handlers;

    internal class ClientPipelineTransport : HttpPipelineTransport
    {
        private readonly RequestInvokerHandler requestInvokerHandler;

        public ClientPipelineTransport(RequestInvokerHandler requestInvokerHandler)
        {
            this.requestInvokerHandler = requestInvokerHandler;
        }

        public override Request CreateRequest()
        {
            throw new NotImplementedException();
        }

        public override void Process(HttpMessage message)
        {
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
            this.ProcessAsync(message).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits
        }

        public override async ValueTask ProcessAsync(HttpMessage message)
        {
            RequestMessage requestMessage = message.Request as RequestMessage;
            if (requestMessage == null)
            {
                throw new NotImplementedException();
            }

            // If the message previously has a response, it means the pipeline is retrying
            bool isComingFromPipelineRetry = message.HasResponse;
            message.Response = await this.requestInvokerHandler.SendAsync(requestMessage, isComingFromPipelineRetry, message.CancellationToken);
        }
    }
}
