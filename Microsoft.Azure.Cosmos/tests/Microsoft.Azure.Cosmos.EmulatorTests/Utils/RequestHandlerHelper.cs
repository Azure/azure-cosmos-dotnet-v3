//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using Microsoft.Azure.Cosmos;
using System.Threading;

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    public class RequestHandlerHelper : RequestHandler
    {
        public Action<RequestMessage> UpdateRequestMessage = null;
        public Func<RequestMessage, ResponseMessage, ResponseMessage> CallBackOnResponse = null;
        public override async Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
        {
            this.UpdateRequestMessage?.Invoke(request);
            ResponseMessage responseMessage = await base.SendAsync(request, cancellationToken);
            if (this.CallBackOnResponse != null)
            {
                responseMessage = this.CallBackOnResponse.Invoke(request, responseMessage);
            }

            return responseMessage;
        }
    }

    public class TestRequestHandler : RequestHandler
    {
        public Func<CosmosClient> GetMessage = null;
        public override Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
        {
            if (request.ResourceType == Documents.ResourceType.Document && request.OperationType == Documents.OperationType.ReadFeed)
            {
                CosmosClient client = this.GetMessage?.Invoke();

                return MockedFriendsProcessor.ProcessAllVersionsAndDeletesRequestAsync(
                    cosmosClient: client,
                    container: default,
                    enableFFCFPartitionSplitArchivalCaching: true,
                    lsn: default,
                    partitionKeyRangeId: default,
                    cancellationToken: default);
            }

            return base.SendAsync(request, cancellationToken);
        }

        public class MockedFriendsProcessor
        {
            public static async Task<ResponseMessage> ProcessAllVersionsAndDeletesRequestAsync(
                CosmosClient cosmosClient,
                Container container,
                bool enableFFCFPartitionSplitArchivalCaching,
                string lsn,
                string partitionKeyRangeId,
                CancellationToken cancellationToken = default)
            {
                if (cosmosClient is null)
                {
                    throw new ArgumentNullException(nameof(cosmosClient));
                }

                if (container is null)
                {
                    throw new ArgumentNullException(nameof(container));
                }

                if (string.IsNullOrEmpty(lsn))
                {
                    throw new ArgumentException($"'{nameof(lsn)}' cannot be null or empty.", nameof(lsn));
                }

                if (string.IsNullOrEmpty(partitionKeyRangeId))
                {
                    throw new ArgumentException($"'{nameof(partitionKeyRangeId)}' cannot be null or empty.", nameof(partitionKeyRangeId));
                }

                Debug.Assert(cancellationToken == default);

                if (enableFFCFPartitionSplitArchivalCaching)
                {

                }

                await Task.Delay(1000);

                return new ResponseMessage(statusCode: System.Net.HttpStatusCode.OK);
            }
        }
    }
}