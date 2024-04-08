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
        public CosmosClient Client { get; private set; }
        public Container Container { get; private set; }

        public void AddClient(CosmosClient client)
        {
            this.Client = client;
        }

        public void AddContainer(Container container)
        {
            this.Container = container;
        }

        public override async Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
        {
            if (request.ResourceType == Documents.ResourceType.Document && request.OperationType == Documents.OperationType.ReadFeed)
            {
                Debug.WriteLine("*** hits custom handler ***");

                IReadOnlyList<FeedRange> partitionKeyRangeCache = await this.Container.GetFeedRangesAsync();

                ResponseMessage response = await base.SendAsync(request, cancellationToken);

                // the partition is gone, meaning a split.
                if (response.StatusCode == System.Net.HttpStatusCode.Gone)
                {
                }

                return await MockedFriendsProcessor.ProcessAllVersionsAndDeletesRequestAsync(
                    cosmosClient: this.Client,
                    container: this.Container,
                    enableFFCFPartitionSplitArchivalCaching: true,
                    lsn: @"""101""",
                    partitionKeyRangeId: "0",
                    cancellationToken: default);
            }

            return await base.SendAsync(request, cancellationToken);
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