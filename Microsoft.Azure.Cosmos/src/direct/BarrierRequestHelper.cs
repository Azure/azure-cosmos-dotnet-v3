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
        public static async Task<DocumentServiceRequest> CreateAsync(
            DocumentServiceRequest request,
            IAuthorizationTokenProvider authorizationTokenProvider,
            long? targetLsn,
            long? targetGlobalCommittedLsn)
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
            else if (request.IsNameBased) // Name based server request
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
            else // RID based Server request 
            {
                barrierLsnRequest = DocumentServiceRequest.Create(
                    OperationType.Head,
                    ResourceId.Parse(request.ResourceId).DocumentCollectionId.ToString(),
                    ResourceType.Collection, null, originalRequestTokenType);
            }

            barrierLsnRequest.Headers[HttpConstants.HttpHeaders.XDate] = DateTime.UtcNow.ToString("r", CultureInfo.InvariantCulture);
            
            if (targetLsn.HasValue && targetLsn.Value > 0)
            {
                barrierLsnRequest.Headers[HttpConstants.HttpHeaders.TargetLsn] = targetLsn.Value.ToString(CultureInfo.InvariantCulture);
            }

            if (targetGlobalCommittedLsn.HasValue && targetGlobalCommittedLsn.Value > 0)
            {
                barrierLsnRequest.Headers[HttpConstants.HttpHeaders.TargetGlobalCommittedLsn] = targetGlobalCommittedLsn.Value.ToString(CultureInfo.InvariantCulture);
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

                case AuthorizationTokenType.ResourceToken:
                    barrierLsnRequest.Headers[HttpConstants.HttpHeaders.Authorization] = request.Headers[HttpConstants.HttpHeaders.Authorization];
                    break;

                default:
                    string unknownAuthToken = "Unknown authorization token kind for read request";
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
    }
}
