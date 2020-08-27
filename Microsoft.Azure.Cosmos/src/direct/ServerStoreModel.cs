//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class ServerStoreModel : IStoreModel
    {
        private readonly StoreClient storeClient;
        private EventHandler<SendingRequestEventArgs> sendingRequest;
        private readonly EventHandler<ReceivedResponseEventArgs> receivedResponse;

        public ServerStoreModel(StoreClient storeClient)
        {
            this.storeClient = storeClient;
        }

        public ServerStoreModel(StoreClient storeClient, EventHandler<SendingRequestEventArgs> sendingRequest, EventHandler<ReceivedResponseEventArgs> receivedResponse)
            : this(storeClient)
        {
            this.sendingRequest = sendingRequest;
            this.receivedResponse = receivedResponse;
        }

        #region Test hooks
        public uint? DefaultReplicaIndex
        {
            get;
            set;
        }

        public string LastReadAddress
        {
            get
            {
                return this.storeClient.LastReadAddress;
            }
            set
            {
                this.storeClient.LastReadAddress = value;
            }
        }

        public bool ForceAddressRefresh
        {
            get
            {
                return this.storeClient.ForceAddressRefresh;
            }
            set
            {
                this.storeClient.ForceAddressRefresh = value;
            }
        }
        #endregion

        public Task<DocumentServiceResponse> ProcessMessageAsync(DocumentServiceRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (this.DefaultReplicaIndex.HasValue)
            {
                request.DefaultReplicaIndex = this.DefaultReplicaIndex;
            }

            string requestConsistencyLevelHeaderValue = request.Headers[HttpConstants.HttpHeaders.ConsistencyLevel];

            request.RequestContext.OriginalRequestConsistencyLevel = null;

            if (!string.IsNullOrEmpty(requestConsistencyLevelHeaderValue))
            {
                ConsistencyLevel requestConsistencyLevel;

                if (!Enum.TryParse<ConsistencyLevel>(requestConsistencyLevelHeaderValue, out requestConsistencyLevel))
                {
                    throw new BadRequestException(
                        string.Format(CultureInfo.CurrentUICulture,
                        RMResources.InvalidHeaderValue,
                        requestConsistencyLevelHeaderValue,
                        HttpConstants.HttpHeaders.ConsistencyLevel));
                }

                request.RequestContext.OriginalRequestConsistencyLevel = requestConsistencyLevel;
            }

            if (ReplicatedResourceClient.IsMasterResource(request.ResourceType))
            {
                request.Headers[HttpConstants.HttpHeaders.ConsistencyLevel] = ConsistencyLevel.Strong.ToString();
            }

            this.sendingRequest?.Invoke(this, new SendingRequestEventArgs(request));

            if (this.receivedResponse != null)
            {
                return this.ProcessMessageWithReceivedResponseDelegateAsync(request, cancellationToken);
            }
            else
            {
                return this.storeClient.ProcessMessageAsync(request, cancellationToken);
            }

        }

        private async Task<DocumentServiceResponse> ProcessMessageWithReceivedResponseDelegateAsync(
            DocumentServiceRequest request,
            CancellationToken cancellationToken = default(CancellationToken))
        {

            DocumentServiceResponse response = await this.storeClient.ProcessMessageAsync(request, cancellationToken);
            this.receivedResponse?.Invoke(this, new ReceivedResponseEventArgs(request, response));
            return response;
        }

        public void Dispose()
        {
            
        }
    }
}