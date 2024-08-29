//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Routing;

    internal class PrimaryReplicaAddressFinder 
    {
        private readonly AsyncCacheNonBlocking<PartitionKeyRangeIdentity, (bool isValid, PartitionAddressInformation)> partitionFanOutLoop = new();
        private readonly IAuthorizationTokenProvider authorizationTokenProvider;
        private readonly TransportClient transportClient;

        public static PrimaryReplicaAddressFinder TryCreatePrimaryReplicaAddressFinder(
            IAuthorizationTokenProvider tokenProvider,
            TransportClient transportClient,
            Protocol protocol)
        {
            if(protocol != Protocol.Tcp)
            {
                return null;
            }

            return new PrimaryReplicaAddressFinder(tokenProvider, transportClient);
        }

        private PrimaryReplicaAddressFinder(
            IAuthorizationTokenProvider tokenProvider,
            TransportClient transportClient)
        {
            this.authorizationTokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
            this.transportClient = transportClient ?? throw new ArgumentNullException(nameof(transportClient));
        }

        public bool IsBackendApiFanoutSupported(
            PartitionKeyRangeIdentity pkId,
            DocumentServiceRequest dsr)
        {
            if (dsr.IsReadOnlyRequest)
            {
                return false;
            }

            return true;
        }

        public Task<(bool isValid, PartitionAddressInformation)> TryGetAddressesFromBackendAsync(
            PartitionKeyRangeIdentity pkId,
            PartitionAddressInformation cachedAddresses)
        {
            return this.partitionFanOutLoop.GetAsync(
                pkId,
                (_) => this.TryFanoutReplicaAsync(pkId, cachedAddresses),
                (stale) => stale.GetHashCode() == cachedAddresses.GetHashCode());
        }

        private async Task<(bool isValid, PartitionAddressInformation)> TryFanoutReplicaAsync(
            PartitionKeyRangeIdentity pkId,
            PartitionAddressInformation cachedAddresses)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            var info = cachedAddresses.Get(Protocol.Tcp);
            var addresses = info.ReplicaTransportAddressUris;
            var addressesTo = info.ReplicaTransportAddressUrisHealthState;

            TimeSpan delayTimeSpan = TimeSpan.FromMilliseconds(50);
            using (DocumentServiceRequest headRequest = DocumentServiceRequest.Create(
                      OperationType.Head,
                      pkId.CollectionRid,
                      ResourceType.Collection,
                      AuthorizationTokenType.PrimaryMasterKey))
            {
                while (!cts.IsCancellationRequested)
                {
                    foreach (var address in addresses)
                    {
                        if (address.IsUnhealthy())
                        {
                            continue;
                        }

                        using var barrierRequest = await BarrierRequestHelper.CreateAsync(
                            headRequest,
                            this.authorizationTokenProvider,
                            null,
                            null);

                        StoreResponse r = await this.transportClient.InvokeResourceOperationAsync(
                            address,
                            barrierRequest);

                        var replicaSize = r.Headers.GetValues(WFConstants.BackendHeaders.CurrentReplicaSetSize);
                        if (replicaSize == null || replicaSize.Length != 1)
                        {
                            continue;
                        }

                        int replicaSetSize = int.Parse(replicaSize.First(), CultureInfo.InvariantCulture);
                        // Found primary. Duplicate and set new primary address.
                        if (replicaSetSize > 0)
                        {
                            IReadOnlyList<AddressInformation> oldAddress = info.ReplicaAddresses;
                            List<AddressInformation> newAddresses = new List<AddressInformation>(addresses.Count);
                            foreach (var old in oldAddress)
                            {
                                bool isPrimary = old.PhysicalUri == address.ToString();
                                var newAddress = new AddressInformation(
                                    old.PhysicalUri,
                                    old.IsPublic,
                                    isPrimary,
                                    Protocol.Tcp);

                                newAddresses.Add(newAddress);
                            }

                            return (true, new PartitionAddressInformation(
                                newAddresses));
                        }
                    }
                }

                await Task.Delay(delayTimeSpan);
            }
        
            return (false, null);
        }

    }
}
