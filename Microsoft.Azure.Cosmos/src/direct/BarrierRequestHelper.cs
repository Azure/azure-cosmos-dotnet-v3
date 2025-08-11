//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;

    internal static class BarrierRequestHelper
    {
        private const bool OldBarrierRequestHandlingEnabledDefault = false;
        private const int extensiveLsnGapThreshold = 10_000;
        private const int highLsnGapThreshold = 1_000;
        private const int mediumLsnGapThreshold = 100;

        private static readonly TimeSpan extensiveLsnGapDelayBetweenWriteBarrierCalls = TimeSpan.FromMilliseconds(1000);
        private static readonly TimeSpan highLsnGapDelayBetweenWriteBarrierCalls = TimeSpan.FromMilliseconds(100);
        private static readonly TimeSpan mediumLsnGapDelayBetweenWriteBarrierCalls = TimeSpan.FromMilliseconds(10);
        private static readonly bool isOldBarrierRequestHandlingEnabled = Helpers.GetSafeEnvironmentVariable<bool>(
            Constants.EnvironmentVariables.OldBarrierRequestHandlingEnabled,
            OldBarrierRequestHandlingEnabledDefault);

        public static async Task<DocumentServiceRequest> CreateAsync(
            DocumentServiceRequest request,
            IAuthorizationTokenProvider authorizationTokenProvider,
            long? targetLsn,
            long? targetGlobalCommittedLsn,
            long? globalNRegionCommittedLsn,
            bool includeRegionContext = false)
        {
            bool isCollectionHeadRequest = BarrierRequestHelper.IsCollectionHeadBarrierRequest(request.ResourceType, request.OperationType);

            //serviceIdentity, if set to masterService, takes priority over above
            if (request.ServiceIdentity != null)
            {
                if (request.ServiceIdentity.IsMasterService)
                {
                    isCollectionHeadRequest = false;
                }
            }

            if (request.RequestAuthorizationTokenType == AuthorizationTokenType.Invalid)
            {
                string message = "AuthorizationTokenType not set for the read request";
                Debug.Assert(false, message);
                DefaultTrace.TraceCritical(message);
            }

            AuthorizationTokenType originalRequestTokenType = request.RequestAuthorizationTokenType;

            DocumentServiceRequest barrierLsnRequest = null;
            if (!isCollectionHeadRequest)
            {
                // DB Feed
                barrierLsnRequest = DocumentServiceRequest.Create(
                        operationType: OperationType.HeadFeed,
                        resourceId: null,
                        resourceType: ResourceType.Database,
                        headers: null,
                        authorizationTokenType: originalRequestTokenType);
            }
#pragma warning disable SA1108
            else if (request.IsNameBased) // Name based server request
#pragma warning restore SA1108
            {
                // get the collection full name
                // dbs/{id}/colls/{collid}/
                string collectionLink = PathsHelper.GetCollectionPath(request.ResourceAddress);
                barrierLsnRequest = DocumentServiceRequest.CreateFromName(
                    OperationType.Head,
                    collectionLink,
                    ResourceType.Collection,
                    originalRequestTokenType,
                    null);
            }
#pragma warning disable SA1108
            else // RID based Server request
#pragma warning restore SA1108
            {
                barrierLsnRequest = DocumentServiceRequest.Create(
                    OperationType.Head,
                    ResourceId.Parse(request.ResourceId).DocumentCollectionId.ToString(),
                    ResourceType.Collection, null, originalRequestTokenType);
            }

            barrierLsnRequest.Headers[HttpConstants.HttpHeaders.XDate] = Rfc1123DateTimeCache.UtcNow();

            if (targetLsn.HasValue && targetLsn.Value > 0)
            {
                barrierLsnRequest.Headers[HttpConstants.HttpHeaders.TargetLsn] = targetLsn.Value.ToString(CultureInfo.InvariantCulture);
            }

            if (targetGlobalCommittedLsn.HasValue && targetGlobalCommittedLsn.Value > 0)
            {
                barrierLsnRequest.Headers[HttpConstants.HttpHeaders.TargetGlobalCommittedLsn] = targetGlobalCommittedLsn.Value.ToString(CultureInfo.InvariantCulture);
            }

            if (globalNRegionCommittedLsn.HasValue && globalNRegionCommittedLsn.Value > 0)
            {
                barrierLsnRequest.Headers[HttpConstants.HttpHeaders.TargetGlobalNRegionCommittedLsn] = globalNRegionCommittedLsn.Value.ToString(CultureInfo.InvariantCulture);
            }

            switch (originalRequestTokenType)
            {
                case AuthorizationTokenType.PrimaryMasterKey:
                case AuthorizationTokenType.PrimaryReadonlyMasterKey:
                case AuthorizationTokenType.SecondaryMasterKey:
                case AuthorizationTokenType.SecondaryReadonlyMasterKey:
                    barrierLsnRequest.Headers[HttpConstants.HttpHeaders.Authorization] = (await authorizationTokenProvider.GetUserAuthorizationAsync(
                        barrierLsnRequest.ResourceAddress,
                        isCollectionHeadRequest ? PathsHelper.GetResourcePath(ResourceType.Collection) : PathsHelper.GetResourcePath(ResourceType.Database),
                        HttpConstants.HttpMethods.Head,
                        barrierLsnRequest.Headers,
                        originalRequestTokenType)).token;
                    break;

                case AuthorizationTokenType.SystemAll:
                case AuthorizationTokenType.SystemReadOnly:
                case AuthorizationTokenType.SystemReadWrite:
                    if (request.RequestContext.TargetIdentity == null)
                    {
                        DefaultTrace.TraceCritical("TargetIdentity is needed to create the ReadBarrier request");
                        throw new InternalServerErrorException(RMResources.InternalServerError);
                    }

                    await authorizationTokenProvider.AddSystemAuthorizationHeaderAsync(
                        barrierLsnRequest,
                        ((ServiceIdentity)request.RequestContext.TargetIdentity).FederationId,
                        HttpConstants.HttpMethods.Head,
                        resourceId: null);
                    break;

                case AuthorizationTokenType.AadToken:
                case AuthorizationTokenType.ResourceToken:
                    barrierLsnRequest.Headers[HttpConstants.HttpHeaders.Authorization] = request.Headers[HttpConstants.HttpHeaders.Authorization];
                    break;

                default:
                    string unknownAuthToken = $"Unknown authorization token kind [{originalRequestTokenType}] for read request";
                    Debug.Assert(false, unknownAuthToken);
                    DefaultTrace.TraceCritical(unknownAuthToken);
                    throw new InternalServerErrorException(RMResources.InternalServerError);
            }

            barrierLsnRequest.RequestContext = request.RequestContext.Clone();

            if (request.ServiceIdentity != null)
            {
                barrierLsnRequest.RouteTo(request.ServiceIdentity);
            }
            if (request.PartitionKeyRangeIdentity != null)
            {
                barrierLsnRequest.RouteTo(request.PartitionKeyRangeIdentity);
            }
            if (request.Headers[HttpConstants.HttpHeaders.PartitionKey] != null)
            {
                barrierLsnRequest.Headers[HttpConstants.HttpHeaders.PartitionKey] = request.Headers[HttpConstants.HttpHeaders.PartitionKey];
            }
            if (request.Headers[WFConstants.BackendHeaders.CollectionRid] != null)
            {
                barrierLsnRequest.Headers[WFConstants.BackendHeaders.CollectionRid] = request.Headers[WFConstants.BackendHeaders.CollectionRid];
            }

            if (includeRegionContext)
            {
                if (request.RequestContext.LocationEndpointToRoute != null)
                {
                    barrierLsnRequest.RequestContext.RouteToLocation(request.RequestContext.LocationEndpointToRoute);
                }
                else if (request.RequestContext.LocationIndexToRoute.HasValue)
                {
                    barrierLsnRequest.RequestContext.RouteToLocation(request.RequestContext.LocationIndexToRoute.Value, false);
                }
            }

            if (request.Properties != null && request.Properties.ContainsKey(WFConstants.BackendHeaders.EffectivePartitionKeyString))
            {
                if (barrierLsnRequest.Properties == null)
                {
                    barrierLsnRequest.Properties = new Dictionary<string, object>();
                }

                barrierLsnRequest.Properties[WFConstants.BackendHeaders.EffectivePartitionKeyString] = request.Properties[WFConstants.BackendHeaders.EffectivePartitionKeyString];
            }

            return barrierLsnRequest;
        }

        internal static bool IsOldBarrierRequestHandlingEnabled
        {
            get
            {
                return isOldBarrierRequestHandlingEnabled;
            }
        }

        internal static bool IsCollectionHeadBarrierRequest(ResourceType resourceType, OperationType operationType)
        {
            switch(resourceType)
            {
                case ResourceType.Attachment:
                case ResourceType.Document:
                case ResourceType.Conflict:
                case ResourceType.StoredProcedure:
                case ResourceType.UserDefinedFunction:
                case ResourceType.Trigger:
                case ResourceType.SystemDocument:
                case ResourceType.PartitionedSystemDocument:
                    return true;
                case ResourceType.Collection:
                    if (operationType != OperationType.ReadFeed && operationType != OperationType.Query && operationType != OperationType.SqlQuery)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                case ResourceType.PartitionKeyRange:
#if !COSMOSCLIENT
                    if (operationType == OperationType.GetSplitPoint || operationType == OperationType.AbortSplit || operationType == OperationType.GetSplitPoints)
                    {
                        return true;
                    }
#endif
                    return false;
                default:
                    return false;
            }
        }
#pragma warning disable SA1507 // Code should not contain multiple blank lines in a row

        
#pragma warning disable CS1570 // XML comment has badly formed XML
/// <summary>
        /// Used to determine the appropriate back-off time between barrier requests based
        /// on the responses to previous barrier requests. The substatus code of HEAD requests
        /// indicate the gap - like how far the targeted LSN/GCLSN was missed.
        /// As a very naive rule-of-thumb the assumpiton is that even for small documents < 1 KB
        /// only about 2000 write trasnactions can possibly be committed on a single phsyical
        /// partition (10,000 RU / 5 RU at least per write operation). The allowed
        /// throughput per physical partition could grow and the min. RU per write operations
        /// could be reduced. There are also scenarios like copying where replication progress
        /// is much faster. So, above is only a vague estimate and the injected delays are more
        /// reluctant (assuming up-to 10,000 transactions could be replicated within a second).
        /// The delay will also be reduced by the backend latency (in case backend also uses
        /// config to inject delay for HEAD requests) - but will at least be as high as before
        /// modifying the back-off based on the SubStatusCodes of the HEAD request.
        /// The motivation for this change was to reduce the number of barrier requests
        /// because they could cause high CPU usage on the backend when a global strong account
        /// has at least one partition where one of the read regions is not progressing.
        /// </summary>
        /// <param name="previousHeadRequestLatency">Latency for the last HEAD request</param>
        /// <param name="responses">Responses for the last barrier request</param>
        /// <param name="minDelay">The minimum delay that should at least be injected</param>
        /// <param name="delay">The max delay that should be injected</param>
        /// <returns>
        /// A flag indicating whether a delay before the next barrier request should be injected.
        /// </returns>
        internal static bool ShouldDelayBetweenHeadRequests(
#pragma warning restore SA1507 // Code should not contain multiple blank lines in a row
#pragma warning restore CS1570 // XML comment has badly formed XML
            TimeSpan previousHeadRequestLatency,
            IList<ReferenceCountedDisposable<StoreResult>> responses,
            TimeSpan minDelay,
            out TimeSpan delay)
        {
            int minLSNGap = Int32.MaxValue;
            foreach (ReferenceCountedDisposable<StoreResult> response in responses)
            {
                if (response.Target.StatusCode == StatusCodes.NoContent)
                {
                    switch (response.Target.SubStatusCode)
                    {
                        case SubStatusCodes.MissedTargetGlobalCommittedLsnOver100:
                            minLSNGap = Math.Min(100, minLSNGap);
                            break;
                        case SubStatusCodes.MissedTargetGlobalCommittedLsnOver1000:
                            minLSNGap = Math.Min(1000, minLSNGap);
                            break;
                        case SubStatusCodes.MissedTargetGlobalCommittedLsnOver10000:
                            minLSNGap = Math.Min(10000, minLSNGap);
                            break;
                        default:
                            minLSNGap = 1;
                            break;
                    }
                }
            }

            TimeSpan delayCandidate = GetDelayBetweenHeadRequests(minLSNGap) - previousHeadRequestLatency;
            if (delayCandidate > minDelay)
            {
                delay = delayCandidate;
                return true;
            }

            delay = minDelay;
            return minDelay > TimeSpan.Zero;
        }

        private static TimeSpan GetDelayBetweenHeadRequests(int minLSNGap)
        {
            if (minLSNGap == Int32.MaxValue)
            {
                return TimeSpan.Zero;
            }

            if (minLSNGap >= extensiveLsnGapThreshold)
            {
                return extensiveLsnGapDelayBetweenWriteBarrierCalls;
            }

            if (minLSNGap >= highLsnGapThreshold)
            {
                return highLsnGapDelayBetweenWriteBarrierCalls;
            }

            if (minLSNGap >= mediumLsnGapThreshold)
            {
                return mediumLsnGapDelayBetweenWriteBarrierCalls;
            }

            return TimeSpan.Zero;
        }
    }
}
