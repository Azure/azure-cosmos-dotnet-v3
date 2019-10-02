// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Azure.Data.Cosmos
{
    using System;
    using System.Threading.Tasks;
    using Azure.Core.Http;
    using Azure.Core.Pipeline;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Handlers;

    internal class ClientPipelineTransport : HttpPipelineTransport
    {
        private readonly RequestInvokerHandler requestInvokerHandler;

        public CosmosPipelineTransport(RequestInvokerHandler requestInvokerHandler)
        {
            this.requestInvokerHandler = requestInvokerHandler;
        }

        public override Request CreateRequest()
        {
            throw new System.NotImplementedException();
        }

        public override void Process(HttpPipelineMessage message)
        {
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
            this.ProcessAsync(message).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits
        }

        public override async Task ProcessAsync(HttpPipelineMessage message)
        {
            RequestMessage requestMessage = message.Request as RequestMessage;
            if (requestMessage == null)
            {
                throw new NotImplementedException();
            }

            message.Response = await this.requestInvokerHandler.SendAsync(requestMessage, message.CancellationToken);
        }
    }
}
