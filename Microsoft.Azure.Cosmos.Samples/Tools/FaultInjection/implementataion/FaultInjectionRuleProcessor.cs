//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.FaultInjection;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Common;
    using System.Runtime.ConstrainedExecution;
    using System.Threading;
    using System.Linq;

    public class FaultInjectionRuleProcessor
    {
        private readonly ConnectionMode connectionMode;
        private readonly CollectionCache collectionCache;
        private readonly GlobalEndpointManager globalEndpointManager;
        private readonly GlobalAddressResolver addressResolver;
        private readonly RetryOptions retryOptions;
        private readonly IRoutingMapProvider routingMapProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="FaultInjectionRuleProcessor"/> class.
        /// </summary>
        /// <param name="connectionMode"></param>
        /// <param name="collectionCache"></param>
        /// <param name="globalEndpointManager"></param>
        /// <param name="addressResolver"></param>
        /// <param name="retryOptions"></param>
        /// <param name="routingMapProvider"></param>
        public FaultInjectionRuleProcessor(
            ConnectionMode connectionMode,
            CollectionCache collectionCache,
            GlobalEndpointManager globalEndpointManager,
            GlobalAddressResolver addressResolver,
            RetryOptions retryOptions,
            IRoutingMapProvider routingMapProvider)
        {
            this.connectionMode = connectionMode;
            this.collectionCache = collectionCache ?? throw new ArgumentNullException(nameof(collectionCache));
            this.globalEndpointManager = globalEndpointManager ?? throw new ArgumentNullException(nameof(globalEndpointManager));
            this.addressResolver = addressResolver ?? throw new ArgumentNullException(nameof(addressResolver));
            this.retryOptions = retryOptions ?? throw new ArgumentNullException(nameof(retryOptions));
            this.routingMapProvider = routingMapProvider ?? throw new ArgumentNullException(nameof(routingMapProvider));
        }

        public IFaultInjectionRuleInternal ProcessFaultInjectionRule(
            FaultInjectionRule rule,
            string containerUri)
        {
            _ = rule ?? throw new ArgumentNullException(nameof(rule));
            _ = string.IsNullOrEmpty(containerUri) ? throw new ArgumentException("containerUri cannot be null or empty") : containerUri;

            ContainerProperties containerProperties = this.collectionCache.ResolveByNameAsync(
                HttpConstants.Versions.CurrentVersion,
                containerUri,
                forceRefesh: false,
                trace: NoOpTrace.Singleton,
                clientSideRequestStatistics: null,
                cancellationToken: default).Result 
                ?? throw new ArgumentException($"Cannot find container info");

            this.ValidateRule(rule);
            return this.GetEffectiveRule(rule, containerProperties);

        }

        private void ValidateRule(FaultInjectionRule rule)
        {
            if (rule.GetCondition().GetConnectionType() == FaultInjectionConnectionType.DIRECT_MODE
                && this.connectionMode != ConnectionMode.Direct)
            {
                throw new ArgumentException("Direct connection mode is not supported when client is not in direct mode");
            }
        }

        private IFaultInjectionRuleInternal GetEffectiveRule(
            FaultInjectionRule rule,
            ContainerProperties containerProperties)
        {
            if (rule.GetResult().GetType() == typeof(FaultInjectionServerErrorRule))
            {
                return this.GetEffectiveServerErrorRule(rule, containerProperties);
            }

            //if (rule.GetResult().GetType() == typeof(FaultInjectionConnectionErrorRule))
            //{
            //    return this.GetEffectiveConnectionErrorRule(rule, resourceAddress);
            //}

            throw new Exception($"{rule.GetResult().GetType()} is not supported");
        }

        private IFaultInjectionRuleInternal GetEffectiveServerErrorRule(FaultInjectionRule rule, ContainerProperties containerProperties)
        {
            FaultInjectionServerErrorType errorType = ((FaultInjectionServerErrorResult)rule.GetResult()).GetServerErrorType();
            FaultInjectionConditionInternal effectiveCondition = new FaultInjectionConditionInternal(containerProperties.ResourceId);
            if (this.CanErrorLimitToOperation(errorType))
            {
                effectiveCondition.SetOperationType(this.GetEffectiveOperationType(rule.GetCondition().GetOperationType()));
            }

            List<Uri> regionEndpoints = this.GetRegionEndpoints(rule.GetCondition());

            if (string.IsNullOrEmpty(rule.GetCondition().GetRegion()))
            {
                List<Uri> regionEndpointsWithDefault = new List<Uri>
                {
                    this.globalEndpointManager.GetDefaultEndpoint()
                };
                effectiveCondition.SetRegionEndpoints(regionEndpointsWithDefault);
            }
            else
            {
                effectiveCondition.SetRegionEndpoints(regionEndpoints);
            }

            List<Uri> effectiveAddresses = BackoffRetryUtility<List<Uri>>.ExecuteAsync(
                () => Task.FromResult(this.ResolvePhyicalAddresses(
                    regionEndpoints,
                    rule.GetCondition().GetEndpoint(),
                    this.IsWriteOnly(rule.GetCondition()),
                    containerProperties)),
                new FaultInjectionRuleProcessorRetryPolicy(this.retryOptions)).Result;

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
                    result.GetSuppressServiceRequests()));

        }

        private bool CanErrorLimitToOperation(FaultInjectionServerErrorType errorType)
        {
            // Some errors should only be applied to specific operationTypes/ requests 
            // others can be applied to all operations
            return errorType != FaultInjectionServerErrorType.GONE
                && errorType != FaultInjectionServerErrorType.CONNECTION_DELAY
                ;
        }

        private OperationType GetEffectiveOperationType(FaultInjectionOperationType faultInjectionOperationType)
        {
            return faultInjectionOperationType switch
            {
                FaultInjectionOperationType.READ_ITEM => OperationType.Read,
                FaultInjectionOperationType.CREATE_ITEM => OperationType.Create,
                FaultInjectionOperationType.QUERY_ITEM => OperationType.Query,
                FaultInjectionOperationType.UPSERT_ITEM => OperationType.Upsert,
                FaultInjectionOperationType.REPLACE_ITEM => OperationType.Replace,
                FaultInjectionOperationType.DELETE_ITEM => OperationType.Delete,
                FaultInjectionOperationType.PATCH_ITEM => OperationType.Patch,
                _ => throw new ArgumentException($"FaultInjectionOperationType: {faultInjectionOperationType} is not supported"),
            };
        }

        private List<Uri> GetRegionEndpoints(FaultInjectionCondition condition)
        {
            bool isWriteOnlyEndpoints = this.IsWriteOnly(condition);

            if(!string.IsNullOrEmpty(condition.GetRegion()))
            {
                return new List<Uri> { this.globalEndpointManager.ResolveFaultInjectionServiceEndpoint(condition.GetRegion(), isWriteOnlyEndpoints) };
            }
            else
            {
                return isWriteOnlyEndpoints ? this.globalEndpointManager.WriteEndpoints.ToList() : this.globalEndpointManager.ReadEndpoints.ToList();
            }
        }

        private bool IsWriteOnly(FaultInjectionCondition condition)
        {
            return this.GetEffectiveOperationType(condition.GetOperationType()).IsWriteOperation();
        }

        private List<Uri> ResolvePhyicalAddresses(
            List<Uri> regionEndpoints,
            FaultInjectionEndpoint addressEndpoints,
            bool isWriteOnly,
            ContainerProperties containerProperties)
        {
            if (addressEndpoints == null)
            {
                return new List<Uri>{ };
            }

            return (List<Uri>)regionEndpoints.Select(
                regionEndpoint =>
                {
                    FeedRangeInternal feedRangeInternal = (FeedRangeInternal)addressEndpoints.GetFeedRange();

                    DocumentServiceRequest request = DocumentServiceRequest.Create(
                        operationType: OperationType.Read,
                        resourceId: containerProperties.ResourceId,
                        resourceType: ResourceType.Document,
                        authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey);

                    return feedRangeInternal.GetPartitionKeyRangesAsync(
                        this.routingMapProvider,
                        containerProperties.ResourceId,
                        containerProperties.PartitionKey,
                        cancellationToken: new CancellationToken(),
                        trace: NoOpTrace.Singleton).Result.Select(
                            partitionKeyRange =>
                            {
                                DocumentServiceRequest fauntInjectionAddressRequest = DocumentServiceRequest.Create(
                                    operationType: OperationType.Read,
                                    resourceId: containerProperties.ResourceId,
                                    resourceType: ResourceType.Document,
                                    authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey);

                                fauntInjectionAddressRequest.RequestContext.RouteToLocation(regionEndpoint);
                                fauntInjectionAddressRequest.RouteTo(new PartitionKeyRangeIdentity(partitionKeyRange));

                                if (isWriteOnly)
                                {
                                    return new List<Uri> { this.ResolvePrimaryTransportAddressUriAsync(fauntInjectionAddressRequest, true).Result.Uri};
                                }

                                return this.ResolveAllTransportAddressUriAsync(
                                    fauntInjectionAddressRequest,
                                    addressEndpoints.IsIncludePrimary(),
                                    true)
                                .Result
                                .OrderBy(address => address.Uri.ToString())
                                .Take(addressEndpoints.GetReplicaCount())
                                .Select(address => address.Uri).ToList();
                                
                            }
                        );
                });
        }

        private async Task<IReadOnlyList<TransportAddressUri>> ResolveAllTransportAddressUriAsync(
            DocumentServiceRequest request,
            bool includePrimary,
            bool forceAddressRefresh)
        {
            PerProtocolPartitionAddressInformation partitionPerProtocolAddress = await this.ResolveAddressesHelperAsync(request, forceAddressRefresh);

            return includePrimary
                ? partitionPerProtocolAddress.ReplicaTransportAddressUris
                : partitionPerProtocolAddress.NonPrimaryReplicaTransportAddressUris;
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
            PartitionAddressInformation partitionAddressInformation =
               await this.addressResolver.ResolveAsync(request, forceAddressRefresh, CancellationToken.None);

            return partitionAddressInformation.Get(Documents.Client.Protocol.Tcp);
        }

        public class FaultInjectionRuleProcessorRetryPolicy : IRetryPolicy
        {
            private readonly ResourceThrottleRetryPolicy resourceThrottleRetryPolicy;
            private readonly WebExceptionRetryPolicy webExceptionRetryPolicy;

            FaultInjectionRuleProcessorRetryPolicy(RetryOptions retryOptions)
            {
                this.resourceThrottleRetryPolicy = new ResourceThrottleRetryPolicy(
                    maxAttemptCount: retryOptions.MaxRetryAttemptsOnThrottledRequests,
                    maxWaitTimeInSeconds: retryOptions.MaxRetryWaitTimeInSeconds);
                this.webExceptionRetryPolicy = new WebExceptionRetryPolicy();
            }

            public Task<ShouldRetryResult> ShouldRetryAsync(Exception e, CancellationToken cancellationToken)
            {
                ShouldRetryResult result = this.webExceptionRetryPolicy.ShouldRetryAsync(e, cancellationToken).Result;
                if (result.ShouldRetry)
                {
                    return Task.FromResult(result);
                }

                return this.resourceThrottleRetryPolicy.ShouldRetryAsync(e, cancellationToken);
            }
        }

    }   
}
