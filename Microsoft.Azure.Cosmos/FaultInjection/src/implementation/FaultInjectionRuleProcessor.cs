//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    internal class FaultInjectionRuleProcessor
    {
        private readonly ConnectionMode connectionMode;
        private readonly CollectionCache collectionCache;
        private readonly GlobalEndpointManager globalEndpointManager;
        private readonly GlobalAddressResolver? addressResolver;
        private readonly Func<IRetryPolicy> retryPolicy;
        private readonly IRoutingMapProvider routingMapProvider;
        private readonly FaultInjectionApplicationContext applicationContext;

        private readonly RegionNameMapper regionNameMapper = new RegionNameMapper();

        /// <summary>
        /// Initializes a new instance of the <see cref="FaultInjectionRuleProcessor"/> class.
        /// </summary>
        /// <param name="connectionMode"></param>
        /// <param name="collectionCache"></param>
        /// <param name="globalEndpointManager"></param>
        /// <param name="addressResolver"></param>
        /// <param name="routingMapProvider"></param>
        /// <param name="applicationContext"></param>
        public FaultInjectionRuleProcessor(
            Func<IRetryPolicy> retryPolicy,
            ConnectionMode connectionMode,
            CollectionCache collectionCache,
            GlobalEndpointManager globalEndpointManager,
            IRoutingMapProvider routingMapProvider,
            FaultInjectionApplicationContext applicationContext,
            GlobalAddressResolver? addressResolver = null)
        {
            this.connectionMode = connectionMode;
            this.collectionCache = collectionCache ?? throw new ArgumentNullException(nameof(collectionCache));
            this.globalEndpointManager = globalEndpointManager ?? throw new ArgumentNullException(nameof(globalEndpointManager));
            this.retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
            this.routingMapProvider = routingMapProvider ?? throw new ArgumentNullException(nameof(routingMapProvider));
            this.applicationContext = applicationContext ?? throw new ArgumentNullException(nameof(applicationContext));
            if (connectionMode == ConnectionMode.Direct)
            {
                this.addressResolver = addressResolver ?? throw new ArgumentNullException(nameof(addressResolver));
            }
        }


        public async Task<IFaultInjectionRuleInternal> ProcessFaultInjectionRule(FaultInjectionRule rule)
        {
            _ = rule ?? throw new ArgumentNullException(nameof(rule));

            this.ValidateRule(rule);
            return await this.GetEffectiveRule(rule);

        }

        private void ValidateRule(FaultInjectionRule rule)
        {
            if (rule.GetCondition().GetConnectionType() == FaultInjectionConnectionType.Direct
                && this.connectionMode != ConnectionMode.Direct)
            {
                throw new ArgumentException("Direct connection mode is not supported when client is not in direct mode");
            }
        }

        private async Task<IFaultInjectionRuleInternal> GetEffectiveRule(FaultInjectionRule rule)
        {
            if (rule.GetResult().GetType() == typeof(FaultInjectionServerErrorResult))
            {
                return await this.GetEffectiveServerErrorRule(rule);
            }

            if (rule.GetResult().GetType() == typeof(FaultInjectionConnectionErrorResult))
            {
                return await this.GetEffectiveConnectionErrorRule(rule);
            }

            throw new Exception($"{rule.GetResult().GetType()} is not supported");
        }

        private async Task<IFaultInjectionRuleInternal> GetEffectiveServerErrorRule(FaultInjectionRule rule)
        {
            FaultInjectionServerErrorType errorType = ((FaultInjectionServerErrorResult)rule.GetResult()).GetServerErrorType();
            FaultInjectionConditionInternal effectiveCondition = new FaultInjectionConditionInternal();

            FaultInjectionOperationType operationType = rule.GetCondition().GetOperationType();
            if ((operationType != FaultInjectionOperationType.All) && this.CanErrorLimitToOperation(errorType))
            {
                OperationType effectiveOperationType = this.GetEffectiveOperationType(operationType);
                if (effectiveOperationType != OperationType.Invalid)
                {
                    effectiveCondition.SetOperationType(this.GetEffectiveOperationType(operationType));
                }
                effectiveCondition.SetResourceType(this.GetEffectiveResourceType(operationType));
            }

            List<Uri> regionEndpoints = this.GetRegionEndpoints(rule.GetCondition());
            if (!string.IsNullOrEmpty(rule.GetCondition().GetRegion()))
            {
                effectiveCondition.SetRegionEndpoints(regionEndpoints);
            }
            else
            {
                List<Uri> defaultRegion = new List<Uri>(regionEndpoints)
                {
                    this.globalEndpointManager.GetDefaultEndpoint()
                };
                effectiveCondition.SetRegionEndpoints(defaultRegion);
            }

            if (rule.GetCondition().GetConnectionType() == FaultInjectionConnectionType.Gateway)
            {
                if (rule.GetCondition().GetEndpoint() != FaultInjectionEndpoint.Empty 
                    && this.CanErrorLimitToOperation(errorType) 
                    && this.CanLimitToPartition(rule.GetCondition()))
                {
                    IEnumerable<string> effectivePKRangeId = 
                        await BackoffRetryUtility<IEnumerable<string>>.ExecuteAsync(
                            () => this.ResolvePartitionKeyRangeIds(
                                rule.GetCondition().GetEndpoint()),
                            this.retryPolicy());

                    if (!this.IsMetaData(rule.GetCondition().GetOperationType()))
                    {
                        effectiveCondition.SetPartitionKeyRangeIds(effectivePKRangeId, rule);
                    }
                }
            }
            else
            {
                if (rule.GetCondition().GetEndpoint() != FaultInjectionEndpoint.Empty)
                {
                    DocumentServiceRequest request = DocumentServiceRequest.CreateFromName(
                       operationType: OperationType.Read,
                       resourceFullName: rule.GetCondition().GetEndpoint().GetResoureName(),
                       resourceType: ResourceType.Document,
                       authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey);

                    ContainerProperties collection = await this.collectionCache.ResolveCollectionAsync(request, CancellationToken.None, NoOpTrace.Singleton);

                    effectiveCondition.SetContainerResourceId(collection.ResourceId);
                }

                List<Uri> effectiveAddresses = await BackoffRetryUtility<List<Uri>>.ExecuteAsync(
                        () => this.ResolvePhyicalAddresses(
                            regionEndpoints,
                            rule.GetCondition(),
                            this.IsWriteOnly(rule.GetCondition())),
                        this.retryPolicy());

                if (!this.CanErrorLimitToOperation(errorType))
                {
                    effectiveAddresses = effectiveAddresses.Select(address =>
                        new Uri(string.Format(
                            "{0}://{1}:{2}/",
                            address.Scheme.ToString(),
                            address.Host.ToString(),
                            address.Port.ToString()))).ToList();
                }

                effectiveCondition.SetAddresses(effectiveAddresses);
            }

            FaultInjectionServerErrorResult result = (FaultInjectionServerErrorResult)rule.GetResult();

            return new FaultInjectionServerErrorRule(
                id: rule.GetId(),
                enabled: rule.IsEnabled(),
                delay: rule.GetStartDelay(),
                duration: rule.GetDuration(),
                hitLimit: rule.GetHitLimit(),
                connectionType: rule.GetCondition().GetConnectionType(),
                condition: effectiveCondition,
                result: new FaultInjectionServerErrorResultInternal(
                    result.GetServerErrorType(),
                    result.GetTimes(),
                    result.GetDelay(),
                    result.GetSuppressServiceRequests(),
                    result.GetInjectionRate(),
                    this.applicationContext));
        }

        private async Task<IFaultInjectionRuleInternal> GetEffectiveConnectionErrorRule(FaultInjectionRule rule)
        {
            List<Uri> regionEndpoints = string.IsNullOrEmpty(rule.GetCondition().GetRegion())
                ? new List<Uri>() : this.GetRegionEndpoints(rule.GetCondition());

            List<Uri> resolvedPhysicalAdresses = await this.ResolvePhyicalAddresses(
                regionEndpoints,
                rule.GetCondition(),
                this.IsWriteOnly(rule.GetCondition()));

            resolvedPhysicalAdresses.ForEach(address => 
                new Uri(string.Format(
                "{0}://{1}:{2}/",
                address.Scheme.ToString(),
                address.Host.ToString(),
                address.Port.ToString())));

            FaultInjectionConnectionErrorResult result = (FaultInjectionConnectionErrorResult)rule.GetResult();
            return new FaultInjectionConnectionErrorRule(
               rule.GetId(),
               rule.IsEnabled(),
               rule.GetStartDelay(),
               rule.GetDuration(),
               regionEndpoints,
               resolvedPhysicalAdresses,
               rule.GetCondition().GetConnectionType(),
               result);
        }

        private bool CanErrorLimitToOperation(FaultInjectionServerErrorType errorType)
        {
            // Some errors should only be applied to specific operationTypes/ requests 
            // others can be applied to all operations
            return errorType != FaultInjectionServerErrorType.Gone
                && errorType != FaultInjectionServerErrorType.ConnectionDelay;
        }

        private bool CanLimitToPartition(FaultInjectionCondition faultInjectionCondition)
        {
            // Some operations can be targeted for a certain partition while some can not (for example metadata requests)
            //TODO: Implement metadata operations
            if (faultInjectionCondition == null)
            {
            }
            return true;
        }

        private OperationType GetEffectiveOperationType(FaultInjectionOperationType faultInjectionOperationType)
        {
            return faultInjectionOperationType switch
            {
                FaultInjectionOperationType.ReadItem => OperationType.Read,
                FaultInjectionOperationType.CreateItem => OperationType.Create,
                FaultInjectionOperationType.QueryItem => OperationType.Query,
                FaultInjectionOperationType.UpsertItem => OperationType.Upsert,
                FaultInjectionOperationType.ReplaceItem => OperationType.Replace,
                FaultInjectionOperationType.DeleteItem => OperationType.Delete,
                FaultInjectionOperationType.PatchItem => OperationType.Patch,
                FaultInjectionOperationType.Batch => OperationType.Batch,
                FaultInjectionOperationType.ReadFeed => OperationType.ReadFeed,
                FaultInjectionOperationType.MetadataContainer => OperationType.Read,
                FaultInjectionOperationType.MetadataDatabaseAccount => OperationType.Read,
                FaultInjectionOperationType.MetadataPartitionKeyRange => OperationType.ReadFeed,
                FaultInjectionOperationType.MetadataRefreshAddresses => OperationType.Invalid,
                FaultInjectionOperationType.MetadataQueryPlan => OperationType.QueryPlan,
                _ => throw new ArgumentException($"FaultInjectionOperationType: {faultInjectionOperationType} is not supported"),
            };
        }

        private ResourceType GetEffectiveResourceType(FaultInjectionOperationType faultInjectionOperationType)
        {
            return faultInjectionOperationType switch
            {
                FaultInjectionOperationType.ReadItem => ResourceType.Document,
                FaultInjectionOperationType.CreateItem => ResourceType.Document,
                FaultInjectionOperationType.QueryItem => ResourceType.Document,
                FaultInjectionOperationType.UpsertItem => ResourceType.Document,
                FaultInjectionOperationType.ReplaceItem => ResourceType.Document,
                FaultInjectionOperationType.DeleteItem => ResourceType.Document,
                FaultInjectionOperationType.PatchItem => ResourceType.Document,
                FaultInjectionOperationType.Batch => ResourceType.Document,
                FaultInjectionOperationType.ReadFeed => ResourceType.Document,
                FaultInjectionOperationType.MetadataContainer => ResourceType.Collection,
                FaultInjectionOperationType.MetadataDatabaseAccount => ResourceType.DatabaseAccount,
                FaultInjectionOperationType.MetadataPartitionKeyRange => ResourceType.PartitionKeyRange,
                FaultInjectionOperationType.MetadataRefreshAddresses => ResourceType.Address,
                FaultInjectionOperationType.MetadataQueryPlan => ResourceType.Document,
                _ => throw new ArgumentException($"FaultInjectionOperationType: {faultInjectionOperationType} is not supported"),
            };
        }

        private List<Uri> GetRegionEndpoints(FaultInjectionCondition condition)
        {
            bool isWriteOnlyEndpoints = this.IsWriteOnly(condition);

            if(!string.IsNullOrEmpty(condition.GetRegion()))
            {
                return new List<Uri> { this.ResolveFaultInjectionServiceEndpoint(condition.GetRegion(), isWriteOnlyEndpoints) };
            }
            else
            {
                return isWriteOnlyEndpoints 
                    ? this.globalEndpointManager.GetAvailableWriteEndpointsByLocation().Values.ToList() 
                    : this.globalEndpointManager.GetAvailableReadEndpointsByLocation().Values.ToList();
            }
        }

        private Uri ResolveFaultInjectionServiceEndpoint(string region, bool isWriteOnlyEndpoints)
        {
            if (isWriteOnlyEndpoints)
            {
                if (this.globalEndpointManager.GetAvailableWriteEndpointsByLocation().TryGetValue(
                    this.regionNameMapper.GetCosmosDBRegionName(region), 
                    out Uri? endpoint))
                {
                    return endpoint;
                }
            }
            else
            {
                if (this.globalEndpointManager.GetAvailableReadEndpointsByLocation().TryGetValue(
                    this.regionNameMapper.GetCosmosDBRegionName(region),
                    out Uri? endpoint))
                {
                    return endpoint;
                }
            }

            throw new ArgumentException($"Cannot find service endpoint for region: {region}");
        }

        private async Task<IEnumerable<string>> ResolvePartitionKeyRangeIds(
           FaultInjectionEndpoint addressEndpoints)
        {
            if (addressEndpoints == null)
            {
                return new List<string>();
            }

            FeedRangeInternal feedRangeInternal = (FeedRangeInternal)addressEndpoints.GetFeedRange();
            DocumentServiceRequest request = DocumentServiceRequest.CreateFromName(
                    operationType: OperationType.Read,
                    resourceFullName: addressEndpoints.GetResoureName(),
                    resourceType: ResourceType.Document,
                    authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey);

            ContainerProperties collection = await this.collectionCache.ResolveCollectionAsync(request, CancellationToken.None, NoOpTrace.Singleton);

            return  await feedRangeInternal.GetPartitionKeyRangesAsync(
                this.routingMapProvider,
                collection.ResourceId,
                collection.PartitionKey,
                new CancellationToken(),
                NoOpTrace.Singleton);
        }


        private bool IsWriteOnly(FaultInjectionCondition condition)
        {
            return condition.GetOperationType() != FaultInjectionOperationType.All 
                && this.GetEffectiveOperationType(condition.GetOperationType()).IsWriteOperation();
        }

        private bool IsMetaData(FaultInjectionOperationType operationType)
        {
            return operationType == FaultInjectionOperationType.MetadataContainer
                || operationType == FaultInjectionOperationType.MetadataDatabaseAccount
                || operationType == FaultInjectionOperationType.MetadataPartitionKeyRange
                || operationType == FaultInjectionOperationType.MetadataRefreshAddresses
                || operationType == FaultInjectionOperationType.MetadataQueryPlan;
        }

        private async Task<List<Uri>> ResolvePhyicalAddresses(
            List<Uri> regionEndpoints,
            FaultInjectionCondition condition,
            bool isWriteOnly)
        {           
            FaultInjectionEndpoint addressEndpoints = condition.GetEndpoint();
            if (addressEndpoints == null || addressEndpoints == FaultInjectionEndpoint.Empty)
            {
                return new List<Uri>{ };
            }

            List<Uri> resolvedPhysicalAddresses = new List<Uri>();

            FeedRangeInternal feedRangeInternal = (FeedRangeInternal)addressEndpoints.GetFeedRange();

            DocumentServiceRequest request = DocumentServiceRequest.CreateFromName(
                    operationType: OperationType.Read,
                    resourceFullName: condition.GetEndpoint().GetResoureName(),
                    resourceType: ResourceType.Document,
                    authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey);

            ContainerProperties collection = await this.collectionCache.ResolveCollectionAsync(request, CancellationToken.None, NoOpTrace.Singleton);

            foreach (Uri regionEndpoint in regionEndpoints)
            {
                //The feed range can be mapped to multiple physical partitions, get the feed range list and resolve addresses for each partition
                IEnumerable<string> pkRanges = await feedRangeInternal.GetPartitionKeyRangesAsync(
                    this.routingMapProvider,
                    collection.ResourceId,
                    collection.PartitionKey,
                    cancellationToken: new CancellationToken(),
                    trace: NoOpTrace.Singleton);

                foreach (string partitionKeyRange in pkRanges)
                {
                    DocumentServiceRequest fauntInjectionAddressRequest = DocumentServiceRequest.Create(
                               operationType: OperationType.Read,
                               resourceId: collection.ResourceId,
                               resourceType: ResourceType.Document,
                               authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey);

                    fauntInjectionAddressRequest.RequestContext.RouteToLocation(regionEndpoint);
                    fauntInjectionAddressRequest.RouteTo(new PartitionKeyRangeIdentity(partitionKeyRange));

                    if (isWriteOnly)
                    {
                        TransportAddressUri primary = await this.ResolvePrimaryTransportAddressUriAsync(fauntInjectionAddressRequest, true);
                        resolvedPhysicalAddresses.Add(primary.Uri);
                    }
                    else
                    {
                        // Make sure Primary URI is the first one in the list
                        IEnumerable<Uri> resolvedEndpoints = (await this.ResolveAllTransportAddressUriAsync(
                                fauntInjectionAddressRequest,
                                addressEndpoints.IsIncludePrimary(),
                                true))
                                .Take(addressEndpoints.GetReplicaCount())
                                .Select(address => address.Uri);
                        resolvedPhysicalAddresses.AddRange(resolvedEndpoints);
                    }
                }
            }

            return resolvedPhysicalAddresses;
        }

        private async Task<IReadOnlyList<TransportAddressUri>> ResolveAllTransportAddressUriAsync(
            DocumentServiceRequest request,
            bool includePrimary,
            bool forceAddressRefresh)
        {
            PerProtocolPartitionAddressInformation partitionPerProtocolAddress = await this.ResolveAddressesHelperAsync(request, forceAddressRefresh);

            if (includePrimary)
            {
                List<TransportAddressUri> allAddresses = new List<TransportAddressUri>();
                TransportAddressUri primary = partitionPerProtocolAddress.PrimaryReplicaTransportAddressUri;
                allAddresses.Add(primary);

                foreach (TransportAddressUri transportAddressUri in partitionPerProtocolAddress.ReplicaTransportAddressUris)
                {
                    if (transportAddressUri != primary)
                    {
                        allAddresses.Add(transportAddressUri);
                    }
                }
                return allAddresses;
            }

            return partitionPerProtocolAddress.NonPrimaryReplicaTransportAddressUris;
        }

        private async Task<TransportAddressUri> ResolvePrimaryTransportAddressUriAsync(
            DocumentServiceRequest request,
            bool forceAddressRefresh)
        {
            PerProtocolPartitionAddressInformation partitionPerProtocolAddress = await this.ResolveAddressesHelperAsync(request, forceAddressRefresh);

            return partitionPerProtocolAddress.GetPrimaryAddressUri(request);
        }

        private async Task<PerProtocolPartitionAddressInformation> ResolveAddressesHelperAsync(
            DocumentServiceRequest request,
            bool forceAddressRefresh)
        {
            if (this.addressResolver != null)
            {
                PartitionAddressInformation partitionAddressInformation =
               await this.addressResolver.ResolveAsync(request, forceAddressRefresh, CancellationToken.None);

                return partitionAddressInformation.Get(Documents.Client.Protocol.Tcp);
            }
            
            throw new ArgumentException("AddressResolver is Null");
        }

        internal GlobalEndpointManager GetGlobalEndpointManager()
        {
            return this.globalEndpointManager;
        }
    }   
}
