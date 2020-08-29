//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using Microsoft.Azure.Cosmos.Core.Trace;
#if COSMOSCLIENT
    using Microsoft.Azure.Cosmos.Rntbd;
#endif
    using Microsoft.Azure.Documents.Collections;
    using RequestPool = RntbdConstants.RntbdEntityPool<RntbdConstants.Request, RntbdConstants.RequestIdentifiers>;

    internal static class TransportSerialization
    {
        // Path format
        internal static readonly char[] UrlTrim = { '/' };

        internal class RntbdHeader
        {
            public RntbdHeader(StatusCodes status, Guid activityId)
            {
                this.Status = status;
                this.ActivityId = activityId;
            }

            public StatusCodes Status { get; private set; }
            public Guid ActivityId { get; private set; }
        }

        internal static byte[] BuildRequest(DocumentServiceRequest request, string replicaPath,
            ResourceOperation resourceOperation, Guid activityId, out int headerSize,
            out int bodySize)
        {
            RntbdConstants.RntbdOperationType operationType = GetRntbdOperationType(resourceOperation.operationType);
            RntbdConstants.RntbdResourceType resourceType = GetRntbdResourceType(resourceOperation.resourceType);

            using RequestPool.EntityOwner owner = RequestPool.Instance.Get();
            RntbdConstants.Request rntbdRequest = owner.Entity;

            rntbdRequest.replicaPath.value.valueBytes = BytesSerializer.GetBytesForString(replicaPath, rntbdRequest);
            rntbdRequest.replicaPath.isPresent = true;

            // special-case headers (ones that don't come from request.headers, or ones that are a merge of
            // merging multiple request.headers, or ones that are parsed from a string to an enum).
            TransportSerialization.AddResourceIdOrPathHeaders(request, rntbdRequest);
            TransportSerialization.AddDateHeader(request, rntbdRequest);
            TransportSerialization.AddContinuation(request, rntbdRequest);
            TransportSerialization.AddMatchHeader(request, operationType, rntbdRequest);
            TransportSerialization.AddIfModifiedSinceHeader(request, rntbdRequest);
            TransportSerialization.AddA_IMHeader(request, rntbdRequest);
            TransportSerialization.AddIndexingDirectiveHeader(request, rntbdRequest);
            TransportSerialization.AddMigrateCollectionDirectiveHeader(request, rntbdRequest);
            TransportSerialization.AddConsistencyLevelHeader(request, rntbdRequest);
            TransportSerialization.AddIsFanout(request, rntbdRequest);
            TransportSerialization.AddEntityId(request, rntbdRequest);
            TransportSerialization.AddAllowScanOnQuery(request, rntbdRequest);
            TransportSerialization.AddEmitVerboseTracesInQuery(request, rntbdRequest);
            TransportSerialization.AddCanCharge(request, rntbdRequest);
            TransportSerialization.AddCanThrottle(request, rntbdRequest);
            TransportSerialization.AddProfileRequest(request, rntbdRequest);
            TransportSerialization.AddEnableLowPrecisionOrderBy(request, rntbdRequest);
            TransportSerialization.AddPageSize(request, rntbdRequest);
            TransportSerialization.AddSupportSpatialLegacyCoordinates(request, rntbdRequest);
            TransportSerialization.AddUsePolygonsSmallerThanAHemisphere(request, rntbdRequest);
            TransportSerialization.AddEnableLogging(request, rntbdRequest);
            TransportSerialization.AddPopulateQuotaInfo(request, rntbdRequest);
            TransportSerialization.AddPopulateResourceCount(request, rntbdRequest);
            TransportSerialization.AddDisableRUPerMinuteUsage(request, rntbdRequest);
            TransportSerialization.AddPopulateQueryMetrics(request, rntbdRequest);
            TransportSerialization.AddQueryForceScan(request, rntbdRequest);
            TransportSerialization.AddResponseContinuationTokenLimitInKb(request, rntbdRequest);
            TransportSerialization.AddPopulatePartitionStatistics(request, rntbdRequest);
            TransportSerialization.AddRemoteStorageType(request, rntbdRequest);
            TransportSerialization.AddCollectionRemoteStorageSecurityIdentifier(request, rntbdRequest);
            TransportSerialization.AddCollectionChildResourceNameLimitInBytes(request, rntbdRequest);
            TransportSerialization.AddCollectionChildResourceContentLengthLimitInKB(request, rntbdRequest);
            TransportSerialization.AddUniqueIndexNameEncodingMode(request, rntbdRequest);
            TransportSerialization.AddUniqueIndexReIndexingState(request, rntbdRequest);
            TransportSerialization.AddPopulateCollectionThroughputInfo(request, rntbdRequest);
            TransportSerialization.AddShareThroughput(request, rntbdRequest);
            TransportSerialization.AddIsReadOnlyScript(request, rntbdRequest);
#if !COSMOSCLIENT
            TransportSerialization.AddIsAutoScaleRequest(request, rntbdRequest);
#endif
            TransportSerialization.AddCanOfferReplaceComplete(request, rntbdRequest);
            TransportSerialization.AddIgnoreSystemLoweringMaxThroughput(request, rntbdRequest);
            TransportSerialization.AddExcludeSystemProperties(request, rntbdRequest);
            TransportSerialization.AddEnumerationDirection(request, rntbdRequest);
            TransportSerialization.AddFanoutOperationStateHeader(request, rntbdRequest);
            TransportSerialization.AddStartAndEndKeys(request, rntbdRequest);
            TransportSerialization.AddContentSerializationFormat(request, rntbdRequest);
            TransportSerialization.AddIsUserRequest(request, rntbdRequest);
            TransportSerialization.AddPreserveFullContent(request, rntbdRequest);
            TransportSerialization.AddIsRUPerGBEnforcementRequest(request, rntbdRequest);
            TransportSerialization.AddIsOfferStorageRefreshRequest(request, rntbdRequest);
            TransportSerialization.AddGetAllPartitionKeyStatistics(request, rntbdRequest);
            TransportSerialization.AddForceSideBySideIndexMigration(request, rntbdRequest);
            TransportSerialization.AddIsMigrateOfferToManualThroughputRequest(request, rntbdRequest);
            TransportSerialization.AddIsMigrateOfferToAutopilotRequest(request, rntbdRequest);
            TransportSerialization.AddSystemDocumentTypeHeader(request, rntbdRequest);
            TransportSerialization.AddTransactionMetaData(request, rntbdRequest);
            TransportSerialization.AddTransactionCompletionFlag(request, rntbdRequest);
            TransportSerialization.AddResourceTypes(request, rntbdRequest);
            TransportSerialization.AddUpdateMaxthroughputEverProvisioned(request, rntbdRequest);
            TransportSerialization.AddUseSystemBudget(request, rntbdRequest);

            // "normal" headers (strings, ULongs, etc.,)
            TransportSerialization.FillTokenFromHeader(request, HttpConstants.HttpHeaders.Authorization, rntbdRequest.authorizationToken, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, HttpConstants.HttpHeaders.SessionToken, rntbdRequest.sessionToken, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, HttpConstants.HttpHeaders.PreTriggerInclude, rntbdRequest.preTriggerInclude, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, HttpConstants.HttpHeaders.PreTriggerExclude, rntbdRequest.preTriggerExclude, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, HttpConstants.HttpHeaders.PostTriggerInclude, rntbdRequest.postTriggerInclude, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, HttpConstants.HttpHeaders.PostTriggerExclude, rntbdRequest.postTriggerExclude, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, HttpConstants.HttpHeaders.PartitionKey, rntbdRequest.partitionKey, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, HttpConstants.HttpHeaders.PartitionKeyRangeId, rntbdRequest.partitionKeyRangeId, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, HttpConstants.HttpHeaders.ResourceTokenExpiry, rntbdRequest.resourceTokenExpiry, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, HttpConstants.HttpHeaders.FilterBySchemaResourceId, rntbdRequest.filterBySchemaRid, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, HttpConstants.HttpHeaders.ShouldBatchContinueOnError, rntbdRequest.shouldBatchContinueOnError, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, HttpConstants.HttpHeaders.IsBatchOrdered, rntbdRequest.isBatchOrdered, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, HttpConstants.HttpHeaders.IsBatchAtomic, rntbdRequest.isBatchAtomic, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, WFConstants.BackendHeaders.CollectionPartitionIndex, rntbdRequest.collectionPartitionIndex, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, WFConstants.BackendHeaders.CollectionServiceIndex, rntbdRequest.collectionServiceIndex, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, WFConstants.BackendHeaders.ResourceSchemaName, rntbdRequest.resourceSchemaName, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, WFConstants.BackendHeaders.BindReplicaDirective, rntbdRequest.bindReplicaDirective, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, WFConstants.BackendHeaders.PrimaryMasterKey, rntbdRequest.primaryMasterKey, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, WFConstants.BackendHeaders.SecondaryMasterKey, rntbdRequest.secondaryMasterKey, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, WFConstants.BackendHeaders.PrimaryReadonlyKey, rntbdRequest.primaryReadonlyKey, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, WFConstants.BackendHeaders.SecondaryReadonlyKey, rntbdRequest.secondaryReadonlyKey, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, WFConstants.BackendHeaders.PartitionCount, rntbdRequest.partitionCount, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, WFConstants.BackendHeaders.CollectionRid, rntbdRequest.collectionRid, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, HttpConstants.HttpHeaders.GatewaySignature, rntbdRequest.gatewaySignature, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, HttpConstants.HttpHeaders.RemainingTimeInMsOnClientRequest, rntbdRequest.remainingTimeInMsOnClientRequest, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, HttpConstants.HttpHeaders.ClientRetryAttemptCount, rntbdRequest.clientRetryAttemptCount, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, HttpConstants.HttpHeaders.TargetLsn, rntbdRequest.targetLsn, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, HttpConstants.HttpHeaders.TargetGlobalCommittedLsn, rntbdRequest.targetGlobalCommittedLsn, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, HttpConstants.HttpHeaders.TransportRequestID, rntbdRequest.transportRequestID, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, HttpConstants.HttpHeaders.RestoreMetadataFilter, rntbdRequest.restoreMetadataFilter, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, WFConstants.BackendHeaders.RestoreParams, rntbdRequest.restoreParams, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, WFConstants.BackendHeaders.PartitionResourceFilter, rntbdRequest.partitionResourceFilter, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, WFConstants.BackendHeaders.EnableDynamicRidRangeAllocation, rntbdRequest.enableDynamicRidRangeAllocation, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, WFConstants.BackendHeaders.SchemaOwnerRid, rntbdRequest.schemaOwnerRid, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, WFConstants.BackendHeaders.SchemaHash, rntbdRequest.schemaHash, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, HttpConstants.HttpHeaders.IsClientEncrypted, rntbdRequest.isClientEncrypted, rntbdRequest);

            TransportSerialization.AddReturnPreferenceIfPresent(request, rntbdRequest);
            TransportSerialization.AddBinaryIdIfPresent(request, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, WFConstants.BackendHeaders.TimeToLiveInSeconds, rntbdRequest.timeToLiveInSeconds, rntbdRequest);
            TransportSerialization.AddEffectivePartitionKeyIfPresent(request, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, WFConstants.BackendHeaders.BinaryPassthroughRequest, rntbdRequest.binaryPassthroughRequest, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, WFConstants.BackendHeaders.AllowTentativeWrites, rntbdRequest.allowTentativeWrites, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, HttpConstants.HttpHeaders.IncludeTentativeWrites, rntbdRequest.includeTentativeWrites, rntbdRequest);
            TransportSerialization.AddMergeStaticIdIfPresent(request, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, HttpConstants.HttpHeaders.MaxPollingIntervalMilliseconds, rntbdRequest.maxPollingIntervalMilliseconds, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, WFConstants.BackendHeaders.PopulateLogStoreInfo, rntbdRequest.populateLogStoreInfo, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, WFConstants.BackendHeaders.MergeCheckPointGLSN, rntbdRequest.mergeCheckpointGlsnKeyName, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, WFConstants.BackendHeaders.PopulateUnflushedMergeEntryCount, rntbdRequest.populateUnflushedMergeEntryCount, rntbdRequest);

            // will be null in case of direct, which is fine - BE will use the value from the connection context message.
            // When this is used in Gateway, the header value will be populated with the proxied HTTP request's header, and
            // BE will respect the per-request value.
            TransportSerialization.FillTokenFromHeader(request, HttpConstants.HttpHeaders.Version, rntbdRequest.clientVersion, rntbdRequest);

            int metadataLength = (sizeof(uint) + sizeof(ushort) + sizeof(ushort) + BytesSerializer.GetSizeOfGuid());
            int headerAndMetadataLength = metadataLength;

            int allocationLength = 0;

            int bodyLength = 0;
            CloneableStream clonedStream = null;
            if (request.CloneableBody != null)
            {
                clonedStream = request.CloneableBody.Clone();
                bodyLength = (int)clonedStream.Length;
            }

            byte[] contextMessage;
            using (clonedStream)
            {
                if (bodyLength > 0)
                {
                    allocationLength += sizeof(uint);
                    allocationLength += (int)bodyLength;

                    rntbdRequest.payloadPresent.value.valueByte = 0x01;
                    rntbdRequest.payloadPresent.isPresent = true;
                }
                else
                {
                    rntbdRequest.payloadPresent.value.valueByte = 0x00;
                    rntbdRequest.payloadPresent.isPresent = true;
                }

                // Once all metadata tokens are set, we can calculate the length.
                headerAndMetadataLength += rntbdRequest.CalculateLength(); // metadata tokens
                allocationLength += headerAndMetadataLength;

                contextMessage = new byte[allocationLength];

                BytesSerializer writer = new BytesSerializer(contextMessage);

                // header
                writer.Write((uint)headerAndMetadataLength);
                writer.Write((ushort)resourceType);
                writer.Write((ushort)operationType);
                writer.Write(activityId);
                int actualWritten = metadataLength;

                // metadata
                rntbdRequest.SerializeToBinaryWriter(ref writer, out int tokensLength);
                actualWritten += tokensLength;

                if (actualWritten != headerAndMetadataLength)
                {
                    DefaultTrace.TraceCritical(
                        "Bug in RNTBD token serialization. Calculated header size: {0}. Actual header size: {1}",
                        headerAndMetadataLength, actualWritten);
                    throw new InternalServerErrorException();
                }

                if (bodyLength > 0)
                {
                    ArraySegment<byte> buffer = clonedStream.GetBuffer();
                    writer.Write((UInt32)bodyLength);
                    writer.Write(buffer);
                }
            }

            headerSize = headerAndMetadataLength;
            bodySize = sizeof(UInt32) + bodyLength;

            const int HeaderSizeWarningThreshold = 128 * 1024;
            const int BodySizeWarningThreshold = 2 * 1024 * 1024;
            if (headerSize > HeaderSizeWarningThreshold)
            {
                DefaultTrace.TraceWarning(
                    "The request header is large. Header size: {0}. Warning threshold: {1}. " +
                    "RID: {2}. Resource type: {3}. Operation: {4}. Address: {5}",
                    headerSize, HeaderSizeWarningThreshold, request.ResourceAddress,
                    request.ResourceType, resourceOperation, replicaPath);
            }
            if (bodySize > BodySizeWarningThreshold)
            {
                DefaultTrace.TraceWarning(
                    "The request body is large. Body size: {0}. Warning threshold: {1}. " +
                    "RID: {2}. Resource type: {3}. Operation: {4}. Address: {5}",
                    bodySize, BodySizeWarningThreshold, request.ResourceAddress,
                    request.ResourceType, resourceOperation, replicaPath);
            }

            return contextMessage;
        }

        internal static byte[] BuildContextRequest(Guid activityId, UserAgentContainer userAgent, RntbdConstants.CallerId callerId)
        {
            byte[] activityIdBytes = activityId.ToByteArray();

            RntbdConstants.ConnectionContextRequest request = new RntbdConstants.ConnectionContextRequest();
            request.protocolVersion.value.valueULong = RntbdConstants.CurrentProtocolVersion;
            request.protocolVersion.isPresent = true;

            request.clientVersion.value.valueBytes = HttpConstants.Versions.CurrentVersionUTF8;
            request.clientVersion.isPresent = true;

            request.userAgent.value.valueBytes = userAgent.UserAgentUTF8;
            request.userAgent.isPresent = true;

            request.callerId.isPresent = false;
            if(callerId == RntbdConstants.CallerId.Gateway)
            {
                request.callerId.value.valueByte = (byte)callerId;
                request.callerId.isPresent = true;
            }

            int length = (sizeof(UInt32) + sizeof(UInt16) + sizeof(UInt16) + activityIdBytes.Length); // header
            length += request.CalculateLength(); // tokens

            byte[] contextMessage = new byte[length];

            BytesSerializer writer = new BytesSerializer(contextMessage);

            // header
            writer.Write(length);
            writer.Write((ushort)RntbdConstants.RntbdResourceType.Connection);
            writer.Write((ushort)RntbdConstants.RntbdOperationType.Connection);
            writer.Write(activityIdBytes);

            // metadata
            request.SerializeToBinaryWriter(ref writer, out _);

            return contextMessage;
        }

        internal static StoreResponse MakeStoreResponse(
            StatusCodes status,
            Guid activityId,
            RntbdConstants.Response response,
            Stream body,
            string serverVersion)
        {
            StoreResponse storeResponse = new StoreResponse()
            {
                Headers = new StoreResponseNameValueCollection()
            };

            TransportSerialization.AddResponseStringHeaderIfPresent(response.lastStateChangeDateTime, HttpConstants.HttpHeaders.LastStateChangeUtc, storeResponse.Headers);
            TransportSerialization.AddResponseStringHeaderIfPresent(response.continuationToken, HttpConstants.HttpHeaders.Continuation, storeResponse.Headers);
            TransportSerialization.AddResponseStringHeaderIfPresent(response.eTag, HttpConstants.HttpHeaders.ETag, storeResponse.Headers);
            TransportSerialization.AddResponseULongHeaderIfPresent(response.retryAfterMilliseconds, HttpConstants.HttpHeaders.RetryAfterInMilliseconds, storeResponse.Headers);
            TransportSerialization.AddResponseStringHeaderIfPresent(response.storageMaxResoureQuota, HttpConstants.HttpHeaders.MaxResourceQuota, storeResponse.Headers);
            TransportSerialization.AddResponseStringHeaderIfPresent(response.storageResourceQuotaUsage, HttpConstants.HttpHeaders.CurrentResourceQuotaUsage, storeResponse.Headers);
            TransportSerialization.AddResponseULongHeaderIfPresent(response.collectionPartitionIndex, WFConstants.BackendHeaders.CollectionPartitionIndex, storeResponse.Headers);
            TransportSerialization.AddResponseULongHeaderIfPresent(response.collectionServiceIndex, WFConstants.BackendHeaders.CollectionServiceIndex, storeResponse.Headers);
            TransportSerialization.AddResponseLongLongHeaderIfPresent(response.LSN, WFConstants.BackendHeaders.LSN, storeResponse.Headers);
            TransportSerialization.AddResponseULongHeaderIfPresent(response.itemCount, HttpConstants.HttpHeaders.ItemCount, storeResponse.Headers);
            TransportSerialization.AddResponseStringHeaderIfPresent(response.schemaVersion, HttpConstants.HttpHeaders.SchemaVersion, storeResponse.Headers);
            TransportSerialization.AddResponseStringHeaderIfPresent(response.ownerFullName, HttpConstants.HttpHeaders.OwnerFullName, storeResponse.Headers);
            TransportSerialization.AddResponseStringHeaderIfPresent(response.ownerId, HttpConstants.HttpHeaders.OwnerId, storeResponse.Headers);
            TransportSerialization.AddResponseStringHeaderIfPresent(response.databaseAccountId, WFConstants.BackendHeaders.DatabaseAccountId, storeResponse.Headers);
            TransportSerialization.AddResponseLongLongHeaderIfPresent(response.quorumAckedLSN, WFConstants.BackendHeaders.QuorumAckedLSN, storeResponse.Headers);
            TransportSerialization.AddResponseByteHeaderIfPresent(response.requestValidationFailure, WFConstants.BackendHeaders.RequestValidationFailure, storeResponse.Headers);
            TransportSerialization.AddResponseULongHeaderIfPresent(response.subStatus, WFConstants.BackendHeaders.SubStatus, storeResponse.Headers);
            TransportSerialization.AddResponseULongHeaderIfPresent(response.collectionUpdateProgress, HttpConstants.HttpHeaders.CollectionIndexTransformationProgress, storeResponse.Headers);
            TransportSerialization.AddResponseULongHeaderIfPresent(response.currentWriteQuorum, WFConstants.BackendHeaders.CurrentWriteQuorum, storeResponse.Headers);
            TransportSerialization.AddResponseULongHeaderIfPresent(response.currentReplicaSetSize, WFConstants.BackendHeaders.CurrentReplicaSetSize, storeResponse.Headers);
            TransportSerialization.AddResponseULongHeaderIfPresent(response.collectionLazyIndexProgress, HttpConstants.HttpHeaders.CollectionLazyIndexingProgress, storeResponse.Headers);
            TransportSerialization.AddResponseStringHeaderIfPresent(response.partitionKeyRangeId, WFConstants.BackendHeaders.PartitionKeyRangeId, storeResponse.Headers);
            TransportSerialization.AddResponseStringHeaderIfPresent(response.logResults, HttpConstants.HttpHeaders.LogResults, storeResponse.Headers);
            TransportSerialization.AddResponseULongHeaderIfPresent(response.xpRole, WFConstants.BackendHeaders.XPRole, storeResponse.Headers);
            TransportSerialization.AddResponseByteHeaderIfPresent(response.isRUPerMinuteUsed, WFConstants.BackendHeaders.IsRUPerMinuteUsed, storeResponse.Headers);
            TransportSerialization.AddResponseStringHeaderIfPresent(response.queryMetrics, WFConstants.BackendHeaders.QueryMetrics, storeResponse.Headers);
            TransportSerialization.AddResponseStringHeaderIfPresent(response.queryExecutionInfo, WFConstants.BackendHeaders.QueryExecutionInfo, storeResponse.Headers);
            TransportSerialization.AddResponseStringHeaderIfPresent(response.indexUtilization, WFConstants.BackendHeaders.IndexUtilization, storeResponse.Headers);
            TransportSerialization.AddResponseLongLongHeaderIfPresent(response.globalCommittedLSN, WFConstants.BackendHeaders.GlobalCommittedLSN, storeResponse.Headers);
            TransportSerialization.AddResponseULongHeaderIfPresent(response.numberOfReadRegions, WFConstants.BackendHeaders.NumberOfReadRegions, storeResponse.Headers);
            TransportSerialization.AddResponseBoolHeaderIfPresent(response.offerReplacePending, WFConstants.BackendHeaders.OfferReplacePending, storeResponse.Headers);
            TransportSerialization.AddResponseLongLongHeaderIfPresent(response.itemLSN, WFConstants.BackendHeaders.ItemLSN, storeResponse.Headers);
            TransportSerialization.AddResponseStringHeaderIfPresent(response.restoreState, WFConstants.BackendHeaders.RestoreState, storeResponse.Headers);
            TransportSerialization.AddResponseStringHeaderIfPresent(response.collectionSecurityIdentifier, WFConstants.BackendHeaders.CollectionSecurityIdentifier, storeResponse.Headers);
            TransportSerialization.AddResponseULongHeaderIfPresent(response.transportRequestID, HttpConstants.HttpHeaders.TransportRequestID, storeResponse.Headers);
            TransportSerialization.AddResponseBoolHeaderIfPresent(response.shareThroughput, WFConstants.BackendHeaders.ShareThroughput, storeResponse.Headers);
            TransportSerialization.AddResponseBoolHeaderIfPresent(response.disableRntbdChannel, HttpConstants.HttpHeaders.DisableRntbdChannel, storeResponse.Headers);
            TransportSerialization.AddResponseStringHeaderIfPresent(response.serverDateTimeUtc, HttpConstants.HttpHeaders.XDate, storeResponse.Headers);
            TransportSerialization.AddResponseLongLongHeaderIfPresent(response.localLSN, WFConstants.BackendHeaders.LocalLSN, storeResponse.Headers);
            TransportSerialization.AddResponseLongLongHeaderIfPresent(response.quorumAckedLocalLSN, WFConstants.BackendHeaders.QuorumAckedLocalLSN, storeResponse.Headers);
            TransportSerialization.AddResponseLongLongHeaderIfPresent(response.itemLocalLSN, WFConstants.BackendHeaders.ItemLocalLSN, storeResponse.Headers);
            TransportSerialization.AddResponseBoolHeaderIfPresent(response.hasTentativeWrites, WFConstants.BackendHeaders.HasTentativeWrites, storeResponse.Headers);
            TransportSerialization.AddResponseStringHeaderIfPresent(response.sessionToken, HttpConstants.HttpHeaders.SessionToken, storeResponse.Headers);
            TransportSerialization.AddResponseLongLongHeaderIfPresent(response.replicatorLSNToGLSNDelta, WFConstants.BackendHeaders.ReplicatorLSNToGLSNDelta, storeResponse.Headers);
            TransportSerialization.AddResponseLongLongHeaderIfPresent(response.replicatorLSNToLLSNDelta, WFConstants.BackendHeaders.ReplicatorLSNToLLSNDelta, storeResponse.Headers);
            TransportSerialization.AddResponseLongLongHeaderIfPresent(response.vectorClockLocalProgress, WFConstants.BackendHeaders.VectorClockLocalProgress, storeResponse.Headers);
            TransportSerialization.AddResponseULongHeaderIfPresent(response.minimumRUsForOffer, WFConstants.BackendHeaders.MinimumRUsForOffer, storeResponse.Headers);
            TransportSerialization.AddResponseULongHeaderIfPresent(response.xpConfigurationSesssionsCount, WFConstants.BackendHeaders.XPConfigurationSessionsCount, storeResponse.Headers);
            TransportSerialization.AddResponseULongHeaderIfPresent(response.unflushedMergeLogEntryCount, WFConstants.BackendHeaders.UnflushedMergLogEntryCount, storeResponse.Headers);
            TransportSerialization.AddResponseStringHeaderIfPresent(response.resourceName, WFConstants.BackendHeaders.ResourceId, storeResponse.Headers);
            TransportSerialization.AddResponseLongLongHeaderIfPresent(response.timeToLiveInSeconds, WFConstants.BackendHeaders.TimeToLiveInSeconds, storeResponse.Headers);
            TransportSerialization.AddResponseBoolHeaderIfPresent(response.replicaStatusRevoked, WFConstants.BackendHeaders.ReplicaStatusRevoked, storeResponse.Headers);
            TransportSerialization.AddResponseULongHeaderIfPresent(response.softMaxAllowedThroughput, WFConstants.BackendHeaders.SoftMaxAllowedThroughput, storeResponse.Headers);

            if (response.requestCharge.isPresent)
            {
                storeResponse.Headers[HttpConstants.HttpHeaders.RequestCharge] = string.Format(CultureInfo.InvariantCulture, "{0:0.##}", response.requestCharge.value.valueDouble);
            }

            if (response.indexingDirective.isPresent)
            {
                string indexingDirective;
                switch (response.indexingDirective.value.valueByte)
                {
                case (byte) RntbdConstants.RntbdIndexingDirective.Default:
                    indexingDirective = IndexingDirectiveStrings.Default;
                    break;
                case (byte) RntbdConstants.RntbdIndexingDirective.Exclude:
                    indexingDirective = IndexingDirectiveStrings.Exclude;
                    break;
                case (byte) RntbdConstants.RntbdIndexingDirective.Include:
                    indexingDirective = IndexingDirectiveStrings.Include;
                    break;
                default:
                    throw new Exception();
                }

                storeResponse.Headers[HttpConstants.HttpHeaders.IndexingDirective] = indexingDirective;
            }

            storeResponse.Headers[HttpConstants.HttpHeaders.ServerVersion] = serverVersion;

            storeResponse.Headers[HttpConstants.HttpHeaders.ActivityId] = activityId.ToString();

            storeResponse.ResponseBody = body;
            storeResponse.Status = (int)status;

            return storeResponse;
        }

        internal static RntbdHeader DecodeRntbdHeader(byte[] header)
        {
            StatusCodes status = (StatusCodes) BitConverter.ToUInt32(header, 4);
            Guid activityId = BytesSerializer.ReadGuidFromBytes(new ArraySegment<byte>(header, 8, 16));
            return new RntbdHeader(status, activityId);
        }

        private static void AddResponseByteHeaderIfPresent(RntbdToken token, string header,
            INameValueCollection headers)
        {
            if (token.isPresent)
            {
                headers[header] = token.value.valueByte.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static void AddResponseBoolHeaderIfPresent(RntbdToken token, string header,
            INameValueCollection headers)
        {
            if (token.isPresent)
            {
                headers[header] = (token.value.valueByte != 0).ToString().ToLowerInvariant();
            }
        }

        private static unsafe void AddResponseStringHeaderIfPresent(RntbdToken token, string header,
            INameValueCollection headers)
        {
            if (token.isPresent)
            {
                headers[header] = BytesSerializer.GetStringFromBytes(token.value.valueBytes);
            }
        }

        private static void AddResponseULongHeaderIfPresent(RntbdToken token, string header,
            INameValueCollection headers)
        {
            if (token.isPresent)
            {
                headers[header] = token.value.valueULong.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static void AddResponseDoubleHeaderIfPresent(RntbdToken token, string header,
            INameValueCollection headers)
        {
            if (token.isPresent)
            {
                headers[header] = token.value.valueDouble.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static void AddResponseFloatHeaderIfPresent(RntbdToken token, string header,
            INameValueCollection headers)
        {
            if (token.isPresent)
            {
                headers[header] = token.value.valueFloat.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static void AddResponseLongLongHeaderIfPresent(RntbdToken token, string header,
            INameValueCollection headers)
        {
            if (token.isPresent)
            {
                headers[header] = token.value.valueLongLong.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static RntbdConstants.RntbdOperationType GetRntbdOperationType(OperationType operationType)
        {
            switch (operationType)
            {
            case OperationType.Create:
                return RntbdConstants.RntbdOperationType.Create;
            case OperationType.Delete:
                return RntbdConstants.RntbdOperationType.Delete;
            case OperationType.ExecuteJavaScript:
                return RntbdConstants.RntbdOperationType.ExecuteJavaScript;
            case OperationType.Query:
                return RntbdConstants.RntbdOperationType.Query;
            case OperationType.Read:
                return RntbdConstants.RntbdOperationType.Read;
            case OperationType.ReadFeed:
                return RntbdConstants.RntbdOperationType.ReadFeed;
            case OperationType.Replace:
                return RntbdConstants.RntbdOperationType.Replace;
            case OperationType.SqlQuery:
                return RntbdConstants.RntbdOperationType.SQLQuery;
            case OperationType.Patch:
                return RntbdConstants.RntbdOperationType.Patch;
            case OperationType.Head:
                return RntbdConstants.RntbdOperationType.Head;
            case OperationType.HeadFeed:
                return RntbdConstants.RntbdOperationType.HeadFeed;
            case OperationType.Upsert:
                return RntbdConstants.RntbdOperationType.Upsert;
            case OperationType.BatchApply:
                return RntbdConstants.RntbdOperationType.BatchApply;
            case OperationType.Batch:
                return RntbdConstants.RntbdOperationType.Batch;
            case OperationType.CompleteUserTransaction:
                return RntbdConstants.RntbdOperationType.CompleteUserTransaction;
#if !COSMOSCLIENT
            case OperationType.Crash:
                return RntbdConstants.RntbdOperationType.Crash;
            case OperationType.Pause:
                return RntbdConstants.RntbdOperationType.Pause;
            case OperationType.Recreate:
                return RntbdConstants.RntbdOperationType.Recreate;
            case OperationType.Recycle:
                return RntbdConstants.RntbdOperationType.Recycle;
            case OperationType.Resume:
                return RntbdConstants.RntbdOperationType.Resume;
            case OperationType.Stop:
                return RntbdConstants.RntbdOperationType.Stop;
            case OperationType.ForceConfigRefresh:
                return RntbdConstants.RntbdOperationType.ForceConfigRefresh;
            case OperationType.Throttle:
                return RntbdConstants.RntbdOperationType.Throttle;
            case OperationType.PreCreateValidation:
                return RntbdConstants.RntbdOperationType.PreCreateValidation;
            case OperationType.GetSplitPoint:
                return RntbdConstants.RntbdOperationType.GetSplitPoint;
            case OperationType.AbortSplit:
                return RntbdConstants.RntbdOperationType.AbortSplit;
            case OperationType.CompleteSplit:
                return RntbdConstants.RntbdOperationType.CompleteSplit;
            case OperationType.CompleteMergeOnMaster:
                return RntbdConstants.RntbdOperationType.CompleteMergeOnMaster;
            case OperationType.CompleteMergeOnTarget:
                 return RntbdConstants.RntbdOperationType.CompleteMergeOnTarget;
            case OperationType.OfferUpdateOperation:
                return RntbdConstants.RntbdOperationType.OfferUpdateOperation;
            case OperationType.OfferPreGrowValidation:
                return RntbdConstants.RntbdOperationType.OfferPreGrowValidation;
            case OperationType.BatchReportThroughputUtilization:
                return RntbdConstants.RntbdOperationType.BatchReportThroughputUtilization;
            case OperationType.AbortPartitionMigration:
                return RntbdConstants.RntbdOperationType.AbortPartitionMigration;
            case OperationType.CompletePartitionMigration:
                return RntbdConstants.RntbdOperationType.CompletePartitionMigration;
            case OperationType.PreReplaceValidation:
                return RntbdConstants.RntbdOperationType.PreReplaceValidation;
            case OperationType.MigratePartition:
                return RntbdConstants.RntbdOperationType.MigratePartition;
            case OperationType.MasterReplaceOfferOperation:
                return RntbdConstants.RntbdOperationType.MasterReplaceOfferOperation;
            case OperationType.ProvisionedCollectionOfferUpdateOperation:
                return RntbdConstants.RntbdOperationType.ProvisionedCollectionOfferUpdateOperation;
            case OperationType.InitiateDatabaseOfferPartitionShrink:
                return RntbdConstants.RntbdOperationType.InitiateDatabaseOfferPartitionShrink;
            case OperationType.CompleteDatabaseOfferPartitionShrink:
                return RntbdConstants.RntbdOperationType.CompleteDatabaseOfferPartitionShrink;
            case OperationType.EnsureSnapshotOperation:
                return RntbdConstants.RntbdOperationType.EnsureSnapshotOperation;
            case OperationType.GetSplitPoints:
                return RntbdConstants.RntbdOperationType.GetSplitPoints;
            case OperationType.ForcePartitionBackup:
                return RntbdConstants.RntbdOperationType.ForcePartitionBackup;
            case OperationType.MasterInitiatedProgressCoordination:
                return RntbdConstants.RntbdOperationType.MasterInitiatedProgressCoordination;
#endif
                case OperationType.AddComputeGatewayRequestCharges:
                return RntbdConstants.RntbdOperationType.AddComputeGatewayRequestCharges;
            default:
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture, "Invalid operation type: {0}", operationType),
                    "operationType");
            }
        }

        private static RntbdConstants.RntbdResourceType GetRntbdResourceType(ResourceType resourceType)
        {
            switch (resourceType)
            {
            case ResourceType.Attachment:
                return RntbdConstants.RntbdResourceType.Attachment;
            case ResourceType.Collection:
                return RntbdConstants.RntbdResourceType.Collection;
            case ResourceType.Conflict:
                return RntbdConstants.RntbdResourceType.Conflict;
            case ResourceType.Database:
                return RntbdConstants.RntbdResourceType.Database;
            case ResourceType.Document:
                return RntbdConstants.RntbdResourceType.Document;
            case ResourceType.Record:
                return RntbdConstants.RntbdResourceType.Record;
            case ResourceType.Permission:
                return RntbdConstants.RntbdResourceType.Permission;
            case ResourceType.StoredProcedure:
                return RntbdConstants.RntbdResourceType.StoredProcedure;
            case ResourceType.Trigger:
                return RntbdConstants.RntbdResourceType.Trigger;
            case ResourceType.User:
                return RntbdConstants.RntbdResourceType.User;
            case ResourceType.ClientEncryptionKey:
                return RntbdConstants.RntbdResourceType.ClientEncryptionKey;
            case ResourceType.UserDefinedType:
                return RntbdConstants.RntbdResourceType.UserDefinedType;
            case ResourceType.UserDefinedFunction:
                return RntbdConstants.RntbdResourceType.UserDefinedFunction;
            case ResourceType.Offer:
                return RntbdConstants.RntbdResourceType.Offer;
            case ResourceType.DatabaseAccount:
                return RntbdConstants.RntbdResourceType.DatabaseAccount;
            case ResourceType.PartitionKeyRange:
                return RntbdConstants.RntbdResourceType.PartitionKeyRange;
            case ResourceType.Schema:
                return RntbdConstants.RntbdResourceType.Schema;
            case ResourceType.BatchApply:
                return RntbdConstants.RntbdResourceType.BatchApply;
            case ResourceType.ComputeGatewayCharges:
                return RntbdConstants.RntbdResourceType.ComputeGatewayCharges;
            case ResourceType.PartitionKey:
                return RntbdConstants.RntbdResourceType.PartitionKey;
            case ResourceType.PartitionedSystemDocument:
                return RntbdConstants.RntbdResourceType.PartitionedSystemDocument;
            case ResourceType.RoleDefinition:
                return RntbdConstants.RntbdResourceType.RoleDefinition;
            case ResourceType.RoleAssignment:
                return RntbdConstants.RntbdResourceType.RoleAssignment;
            case ResourceType.Transaction:
                    return RntbdConstants.RntbdResourceType.Transaction;
#if !COSMOSCLIENT
                case ResourceType.Module:
                return RntbdConstants.RntbdResourceType.Module;
            case ResourceType.ModuleCommand:
                return RntbdConstants.RntbdResourceType.ModuleCommand;
            case ResourceType.Replica:
                return RntbdConstants.RntbdResourceType.Replica;
            case ResourceType.PartitionSetInformation:
                return RntbdConstants.RntbdResourceType.PartitionSetInformation;
            case ResourceType.XPReplicatorAddress:
                return RntbdConstants.RntbdResourceType.XPReplicatorAddress;
            case ResourceType.MasterPartition:
                return RntbdConstants.RntbdResourceType.MasterPartition;
            case ResourceType.ServerPartition:
                return RntbdConstants.RntbdResourceType.ServerPartition;
            case ResourceType.Topology:
                return RntbdConstants.RntbdResourceType.Topology;
            case ResourceType.RestoreMetadata:
                return RntbdConstants.RntbdResourceType.RestoreMetadata;
            case ResourceType.RidRange:
                return RntbdConstants.RntbdResourceType.RidRange;
            case ResourceType.VectorClock:
                return RntbdConstants.RntbdResourceType.VectorClock;
            case ResourceType.Snapshot:
                    return RntbdConstants.RntbdResourceType.Snapshot;
#endif
                default:
                throw new ArgumentException(
                    string.Format(CultureInfo.InvariantCulture, "Invalid resource type: {0}", resourceType),
                    "resourceType");
            }
        }

        private static void AddMatchHeader(DocumentServiceRequest request, RntbdConstants.RntbdOperationType operationType, RntbdConstants.Request rntbdRequest)
        {
            string match = null;
            switch (operationType)
            {
            case RntbdConstants.RntbdOperationType.Read:
            case RntbdConstants.RntbdOperationType.ReadFeed:
                match = request.Headers[HttpConstants.HttpHeaders.IfNoneMatch];
                break;
            default:
                match = request.Headers[HttpConstants.HttpHeaders.IfMatch];
                break;
            }

            if (!string.IsNullOrEmpty(match))
            {
                rntbdRequest.match.value.valueBytes = BytesSerializer.GetBytesForString(match, rntbdRequest);
                rntbdRequest.match.isPresent = true;
            }
        }

        private static void AddIfModifiedSinceHeader(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[HttpConstants.HttpHeaders.IfModifiedSince];
            if (!string.IsNullOrEmpty(headerValue))
            {
                rntbdRequest.ifModifiedSince.value.valueBytes = BytesSerializer.GetBytesForString(headerValue, rntbdRequest);
                rntbdRequest.ifModifiedSince.isPresent = true;
            }
        }

        private static void AddA_IMHeader(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[HttpConstants.HttpHeaders.A_IM];
            if (!string.IsNullOrEmpty(headerValue))
            {
                rntbdRequest.a_IM.value.valueBytes = BytesSerializer.GetBytesForString(headerValue, rntbdRequest);
                rntbdRequest.a_IM.isPresent = true;
            }
        }

        private static void AddDateHeader(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string dateHeader = Helpers.GetDateHeader(request.Headers);
            if (!string.IsNullOrEmpty(dateHeader))
            {
                rntbdRequest.date.value.valueBytes = BytesSerializer.GetBytesForString(dateHeader, rntbdRequest);
                rntbdRequest.date.isPresent = true;
            }
        }

        private static void AddContinuation(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Continuation))
            {
                rntbdRequest.continuationToken.value.valueBytes = BytesSerializer.GetBytesForString(request.Continuation, rntbdRequest);
                rntbdRequest.continuationToken.isPresent = true;
            }
        }

        private static void AddResourceIdOrPathHeaders(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.ResourceId))
            {
                // name based can also have ResourceId because gateway might generate it.
                rntbdRequest.resourceId.value.valueBytes = ResourceId.Parse(request.ResourceType, request.ResourceId);
                rntbdRequest.resourceId.isPresent = true;
            }

            if (request.IsNameBased)
            {
                // Assumption: format is like "dbs/dbName/colls/collName/docs/docName" or "/dbs/dbName/colls/collName",
                // not "apps/appName/partitions/partitionKey/replicas/replicaId/dbs/dbName"
                string[] fragments = request.ResourceAddress.Split(
                    TransportSerialization.UrlTrim, StringSplitOptions.RemoveEmptyEntries);

                if (fragments.Length >= 2)
                {
                    switch (fragments[0])
                    {
                    case Paths.DatabasesPathSegment:
                        rntbdRequest.databaseName.value.valueBytes = BytesSerializer.GetBytesForString(fragments[1], rntbdRequest);
                        rntbdRequest.databaseName.isPresent = true;
                        break;
                    case Paths.SnapshotsPathSegment:
                        rntbdRequest.snapshotName.value.valueBytes = BytesSerializer.GetBytesForString(fragments[1], rntbdRequest);
                        rntbdRequest.snapshotName.isPresent = true;
                        break;
                    case Paths.RoleDefinitionsPathSegment:
                        rntbdRequest.roleDefinitionName.value.valueBytes = BytesSerializer.GetBytesForString(fragments[1], rntbdRequest);
                        rntbdRequest.roleDefinitionName.isPresent = true;
                        break;
                    case Paths.RoleAssignmentsPathSegment:
                        rntbdRequest.roleAssignmentName.value.valueBytes = BytesSerializer.GetBytesForString(fragments[1], rntbdRequest);
                        rntbdRequest.roleAssignmentName.isPresent = true;
                        break;
                    default:
                        throw new BadRequestException();
                    }
                }

                if (fragments.Length >= 4)
                {
                    switch (fragments[2])
                    {
                    case Paths.CollectionsPathSegment:
                        rntbdRequest.collectionName.value.valueBytes = BytesSerializer.GetBytesForString(fragments[3], rntbdRequest);
                        rntbdRequest.collectionName.isPresent = true;
                        break;
                    case Paths.ClientEncryptionKeysPathSegment:
                        rntbdRequest.clientEncryptionKeyName.value.valueBytes = BytesSerializer.GetBytesForString(fragments[3], rntbdRequest);
                        rntbdRequest.clientEncryptionKeyName.isPresent = true;
                        break;
                    case Paths.UsersPathSegment:
                        rntbdRequest.userName.value.valueBytes = BytesSerializer.GetBytesForString(fragments[3], rntbdRequest);
                        rntbdRequest.userName.isPresent = true;
                        break;
                    case Paths.UserDefinedTypesPathSegment:
                        rntbdRequest.userDefinedTypeName.value.valueBytes = BytesSerializer.GetBytesForString(fragments[3], rntbdRequest);
                        rntbdRequest.userDefinedTypeName.isPresent = true;
                        break;
                    }
                }

                if (fragments.Length >= 6)
                {
                    switch (fragments[4])
                    {
                    case Paths.DocumentsPathSegment:
                        rntbdRequest.documentName.value.valueBytes = BytesSerializer.GetBytesForString(fragments[5], rntbdRequest);
                        rntbdRequest.documentName.isPresent = true;
                        break;
                    case Paths.StoredProceduresPathSegment:
                        rntbdRequest.storedProcedureName.value.valueBytes = BytesSerializer.GetBytesForString(fragments[5], rntbdRequest);
                        rntbdRequest.storedProcedureName.isPresent = true;
                        break;
                    case Paths.PermissionsPathSegment:
                        rntbdRequest.permissionName.value.valueBytes = BytesSerializer.GetBytesForString(fragments[5], rntbdRequest);
                        rntbdRequest.permissionName.isPresent = true;
                        break;
                    case Paths.UserDefinedFunctionsPathSegment:
                        rntbdRequest.userDefinedFunctionName.value.valueBytes = BytesSerializer.GetBytesForString(fragments[5], rntbdRequest);
                        rntbdRequest.userDefinedFunctionName.isPresent = true;
                        break;
                    case Paths.TriggersPathSegment:
                        rntbdRequest.triggerName.value.valueBytes = BytesSerializer.GetBytesForString(fragments[5], rntbdRequest);
                        rntbdRequest.triggerName.isPresent = true;
                        break;
                    case Paths.ConflictsPathSegment:
                        rntbdRequest.conflictName.value.valueBytes = BytesSerializer.GetBytesForString(fragments[5], rntbdRequest);
                        rntbdRequest.conflictName.isPresent = true;
                        break;
                    case Paths.PartitionKeyRangesPathSegment:
                        rntbdRequest.partitionKeyRangeName.value.valueBytes = BytesSerializer.GetBytesForString(fragments[5], rntbdRequest);
                        rntbdRequest.partitionKeyRangeName.isPresent = true;
                        break;
                    case Paths.SchemasPathSegment:
                        rntbdRequest.schemaName.value.valueBytes = BytesSerializer.GetBytesForString(fragments[5], rntbdRequest);
                        rntbdRequest.schemaName.isPresent = true;
                        break;
                    case Paths.PartitionedSystemDocumentsPathSegment:
                        rntbdRequest.systemDocumentName.value.valueBytes = BytesSerializer.GetBytesForString(fragments[5], rntbdRequest);
                        rntbdRequest.systemDocumentName.isPresent = true;
                        break;
                    }
                }

                if (fragments.Length >= 8)
                {
                    switch (fragments[6])
                    {
                    case Paths.AttachmentsPathSegment:
                        rntbdRequest.attachmentName.value.valueBytes = BytesSerializer.GetBytesForString(fragments[7], rntbdRequest);
                        rntbdRequest.attachmentName.isPresent = true;
                        break;
                    }
                }
            }
        }

        private static void AddBinaryIdIfPresent(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            object binaryPayload;
            if (request.Properties != null && request.Properties.TryGetValue(WFConstants.BackendHeaders.BinaryId, out binaryPayload))
            {
                byte[] binaryData = binaryPayload as byte[];
                if (binaryData == null)
                {
                    throw new ArgumentOutOfRangeException(WFConstants.BackendHeaders.BinaryId);
                }

                rntbdRequest.binaryId.value.valueBytes = binaryData;
                rntbdRequest.binaryId.isPresent = true;
            }
        }

        private static void AddReturnPreferenceIfPresent(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string value = request.Headers[HttpConstants.HttpHeaders.Prefer];

            if (!string.IsNullOrEmpty(value))
            {
                if (string.Equals(value, HttpConstants.HttpHeaderValues.PreferReturnMinimal, StringComparison.OrdinalIgnoreCase))
                {
                    rntbdRequest.returnPreference.value.valueByte = (byte)0x01;
                    rntbdRequest.returnPreference.isPresent = true;
                }
                else if (string.Equals(value, HttpConstants.HttpHeaderValues.PreferReturnRepresentation, StringComparison.OrdinalIgnoreCase))
                {
                    rntbdRequest.returnPreference.value.valueByte = (byte)0x00;
                    rntbdRequest.returnPreference.isPresent = true;
                }
            }
        }

        private static void AddEffectivePartitionKeyIfPresent(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (request.Properties == null)
            {
                return;
            }

            object binaryPayload;
            if (request.Properties.TryGetValue(WFConstants.BackendHeaders.EffectivePartitionKey, out binaryPayload))
            {
                byte[] binaryData = binaryPayload as byte[];
                if (binaryData == null)
                {
                    throw new ArgumentOutOfRangeException(WFConstants.BackendHeaders.EffectivePartitionKey);
                }

                rntbdRequest.effectivePartitionKey.value.valueBytes = binaryData;
                rntbdRequest.effectivePartitionKey.isPresent = true;
            }
        }

        private static void AddMergeStaticIdIfPresent(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (request.Properties == null)
            {
                return;
            }

            object binaryPayload;
            if (request.Properties.TryGetValue(WFConstants.BackendHeaders.MergeStaticId, out binaryPayload))
            {
                byte[] binaryData = binaryPayload as byte[];
                if (binaryData == null)
                {
                    throw new ArgumentOutOfRangeException(WFConstants.BackendHeaders.MergeStaticId);
                }

                rntbdRequest.mergeStaticId.value.valueBytes = binaryData;
                rntbdRequest.mergeStaticId.isPresent = true;
            }
        }

        private static void AddEntityId(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.EntityId))
            {
                rntbdRequest.entityId.value.valueBytes = BytesSerializer.GetBytesForString(request.EntityId, rntbdRequest);
                rntbdRequest.entityId.isPresent = true;
            }
        }

        private static void AddIndexingDirectiveHeader(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.IndexingDirective]))
            {
                RntbdConstants.RntbdIndexingDirective rntbdDirective = RntbdConstants.RntbdIndexingDirective.Invalid;
                IndexingDirective directive;
                if (!Enum.TryParse(request.Headers[HttpConstants.HttpHeaders.IndexingDirective], true, out directive))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                        request.Headers[HttpConstants.HttpHeaders.IndexingDirective], typeof(IndexingDirective).Name));
                }

                switch (directive)
                {
                case IndexingDirective.Default:
                    rntbdDirective = RntbdConstants.RntbdIndexingDirective.Default;
                    break;
                case IndexingDirective.Exclude:
                    rntbdDirective = RntbdConstants.RntbdIndexingDirective.Exclude;
                    break;
                case IndexingDirective.Include:
                    rntbdDirective = RntbdConstants.RntbdIndexingDirective.Include;
                    break;
                default:
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                        request.Headers[HttpConstants.HttpHeaders.IndexingDirective], typeof(IndexingDirective).Name));
                }

                rntbdRequest.indexingDirective.value.valueByte = (byte) rntbdDirective;
                rntbdRequest.indexingDirective.isPresent = true;
            }
        }

        private static void AddMigrateCollectionDirectiveHeader(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.MigrateCollectionDirective]))
            {
                RntbdConstants.RntbdMigrateCollectionDirective rntbdDirective = RntbdConstants.RntbdMigrateCollectionDirective.Invalid;
                MigrateCollectionDirective directive;
                if (!Enum.TryParse(request.Headers[HttpConstants.HttpHeaders.MigrateCollectionDirective], true, out directive))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                        request.Headers[HttpConstants.HttpHeaders.MigrateCollectionDirective], typeof(MigrateCollectionDirective).Name));
                }

                switch (directive)
                {
                case MigrateCollectionDirective.Freeze:
                    rntbdDirective = RntbdConstants.RntbdMigrateCollectionDirective.Freeze;
                    break;
                case MigrateCollectionDirective.Thaw:
                    rntbdDirective = RntbdConstants.RntbdMigrateCollectionDirective.Thaw;
                    break;
                default:
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                        request.Headers[HttpConstants.HttpHeaders.MigrateCollectionDirective], typeof(MigrateCollectionDirective).Name));
                }

                rntbdRequest.migrateCollectionDirective.value.valueByte = (byte) rntbdDirective;
                rntbdRequest.migrateCollectionDirective.isPresent = true;
            }
        }

        private static void AddConsistencyLevelHeader(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.ConsistencyLevel]))
            {
                RntbdConstants.RntbdConsistencyLevel rntbdConsistencyLevel = RntbdConstants.RntbdConsistencyLevel.Invalid;
                ConsistencyLevel consistencyLevel;
                if (!Enum.TryParse<ConsistencyLevel>(request.Headers[HttpConstants.HttpHeaders.ConsistencyLevel], true, out consistencyLevel))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                        request.Headers[HttpConstants.HttpHeaders.ConsistencyLevel], typeof(ConsistencyLevel).Name));
                }

                switch (consistencyLevel)
                {
                case ConsistencyLevel.Strong:
                    rntbdConsistencyLevel = RntbdConstants.RntbdConsistencyLevel.Strong;
                    break;
                case ConsistencyLevel.BoundedStaleness:
                    rntbdConsistencyLevel = RntbdConstants.RntbdConsistencyLevel.BoundedStaleness;
                    break;
                case ConsistencyLevel.Session:
                    rntbdConsistencyLevel = RntbdConstants.RntbdConsistencyLevel.Session;
                    break;
                case ConsistencyLevel.Eventual:
                    rntbdConsistencyLevel = RntbdConstants.RntbdConsistencyLevel.Eventual;
                    break;
                case ConsistencyLevel.ConsistentPrefix:
                    rntbdConsistencyLevel = RntbdConstants.RntbdConsistencyLevel.ConsistentPrefix;
                    break;
                default:
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                        request.Headers[HttpConstants.HttpHeaders.ConsistencyLevel], typeof(ConsistencyLevel).Name));
                }

                rntbdRequest.consistencyLevel.value.valueByte = (byte) rntbdConsistencyLevel;
                rntbdRequest.consistencyLevel.isPresent = true;
            }
        }

        private static void AddIsFanout(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[WFConstants.BackendHeaders.IsFanoutRequest]))
            {
                rntbdRequest.isFanout.value.valueByte = (request.Headers[WFConstants.BackendHeaders.IsFanoutRequest].Equals(bool.TrueString)) ? (byte) 0x01 : (byte) 0x00;
                rntbdRequest.isFanout.isPresent = true;
            }
        }

        private static void AddAllowScanOnQuery(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.EnableScanInQuery]))
            {
                rntbdRequest.enableScanInQuery.value.valueByte = (request.Headers[HttpConstants.HttpHeaders.EnableScanInQuery].
                    Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
                    ? (byte) 0x01
                    : (byte) 0x00;
                rntbdRequest.enableScanInQuery.isPresent = true;
            }
        }

        private static void AddEnableLowPrecisionOrderBy(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.EnableLowPrecisionOrderBy]))
            {
                rntbdRequest.enableLowPrecisionOrderBy.value.valueByte = (request.Headers[HttpConstants.HttpHeaders.EnableLowPrecisionOrderBy].
                    Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
                    ? (byte) 0x01
                    : (byte) 0x00;
                rntbdRequest.enableLowPrecisionOrderBy.isPresent = true;
            }
        }

        private static void AddEmitVerboseTracesInQuery(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.EmitVerboseTracesInQuery]))
            {
                rntbdRequest.emitVerboseTracesInQuery.value.valueByte = (request.Headers[HttpConstants.HttpHeaders.EmitVerboseTracesInQuery].
                    Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
                    ? (byte) 0x01
                    : (byte) 0x00;
                rntbdRequest.emitVerboseTracesInQuery.isPresent = true;
            }
        }

        private static void AddCanCharge(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.CanCharge]))
            {
                rntbdRequest.canCharge.value.valueByte = (request.Headers[HttpConstants.HttpHeaders.CanCharge].
                    Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
                    ? (byte) 0x01
                    : (byte) 0x00;
                rntbdRequest.canCharge.isPresent = true;
            }
        }

        private static void AddCanThrottle(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.CanThrottle]))
            {
                rntbdRequest.canThrottle.value.valueByte = (request.Headers[HttpConstants.HttpHeaders.CanThrottle].
                    Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
                    ? (byte) 0x01
                    : (byte) 0x00;
                rntbdRequest.canThrottle.isPresent = true;
            }
        }

        private static void AddProfileRequest(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.ProfileRequest]))
            {
                rntbdRequest.profileRequest.value.valueByte = (request.Headers[HttpConstants.HttpHeaders.ProfileRequest].
                    Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
                    ? (byte) 0x01
                    : (byte) 0x00;
                rntbdRequest.profileRequest.isPresent = true;
            }
        }

        private static void AddPageSize(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string value = request.Headers[HttpConstants.HttpHeaders.PageSize];

            if (!string.IsNullOrEmpty(value))
            {
                int valueInt;
                if (!Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out valueInt))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidPageSize, value));
                }

                if (valueInt == -1)
                {
                    rntbdRequest.pageSize.value.valueULong = UInt32.MaxValue;
                }
                else if (valueInt >= 0)
                {
                    rntbdRequest.pageSize.value.valueULong = (UInt32) valueInt;
                }
                else
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidPageSize, value));
                }

                rntbdRequest.pageSize.isPresent = true;
            }
        }

        private static void AddEnableLogging(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.EnableLogging]))
            {
                rntbdRequest.enableLogging.value.valueByte = (request.Headers[HttpConstants.HttpHeaders.EnableLogging].
                    Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
                    ? (byte) 0x01
                    : (byte) 0x00;
                rntbdRequest.enableLogging.isPresent = true;
            }
        }

        private static void AddSupportSpatialLegacyCoordinates(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.SupportSpatialLegacyCoordinates]))
            {
                rntbdRequest.supportSpatialLegacyCoordinates.value.valueByte = (request.Headers[HttpConstants.HttpHeaders.SupportSpatialLegacyCoordinates].
                    Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
                    ? (byte) 0x01
                    : (byte) 0x00;
                rntbdRequest.supportSpatialLegacyCoordinates.isPresent = true;
            }
        }

        private static void AddUsePolygonsSmallerThanAHemisphere(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.UsePolygonsSmallerThanAHemisphere]))
            {
                rntbdRequest.usePolygonsSmallerThanAHemisphere.value.valueByte = (request.Headers[HttpConstants.HttpHeaders.UsePolygonsSmallerThanAHemisphere].
                    Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
                    ? (byte) 0x01
                    : (byte) 0x00;
                rntbdRequest.usePolygonsSmallerThanAHemisphere.isPresent = true;
            }
        }

        private static void AddPopulateQuotaInfo(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.PopulateQuotaInfo]))
            {
                rntbdRequest.populateQuotaInfo.value.valueByte = (request.Headers[HttpConstants.HttpHeaders.PopulateQuotaInfo].
                    Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
                    ? (byte) 0x01
                    : (byte) 0x00;
                rntbdRequest.populateQuotaInfo.isPresent = true;
            }
        }

        private static void AddPopulateResourceCount(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.PopulateResourceCount]))
            {
                rntbdRequest.populateResourceCount.value.valueByte = (request.Headers[HttpConstants.HttpHeaders.PopulateResourceCount].
                    Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
                    ? (byte) 0x01
                    : (byte) 0x00;
                rntbdRequest.populateResourceCount.isPresent = true;
            }
        }

        private static void AddPopulatePartitionStatistics(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.PopulatePartitionStatistics]))
            {
                rntbdRequest.populatePartitionStatistics.value.valueByte = (request.Headers[HttpConstants.HttpHeaders.PopulatePartitionStatistics].
                    Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
                    ? (byte) 0x01
                    : (byte) 0x00;
                rntbdRequest.populatePartitionStatistics.isPresent = true;
            }
        }

        private static void AddDisableRUPerMinuteUsage(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.DisableRUPerMinuteUsage]))
            {
                rntbdRequest.disableRUPerMinuteUsage.value.valueByte = (request.Headers[HttpConstants.HttpHeaders.DisableRUPerMinuteUsage].
                    Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
                    ? (byte) 0x01
                    : (byte) 0x00;
                rntbdRequest.disableRUPerMinuteUsage.isPresent = true;
            }
        }

        private static void AddPopulateQueryMetrics(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.PopulateQueryMetrics]))
            {
                rntbdRequest.populateQueryMetrics.value.valueByte = (request.Headers[HttpConstants.HttpHeaders.PopulateQueryMetrics].
                    Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
                    ? (byte) 0x01
                    : (byte) 0x00;
                rntbdRequest.populateQueryMetrics.isPresent = true;
            }
        }

        private static void AddQueryForceScan(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.ForceQueryScan]))
            {
                rntbdRequest.forceQueryScan.value.valueByte = (request.Headers[HttpConstants.HttpHeaders.ForceQueryScan].
                    Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
                    ? (byte)0x01
                    : (byte)0x00;
                rntbdRequest.forceQueryScan.isPresent = true;
            }
        }

        private static void AddPopulateCollectionThroughputInfo(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.PopulateCollectionThroughputInfo]))
            {
                rntbdRequest.populateCollectionThroughputInfo.value.valueByte = (request.Headers[HttpConstants.HttpHeaders.PopulateCollectionThroughputInfo].
                    Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
                    ? (byte) 0x01
                    : (byte) 0x00;
                rntbdRequest.populateCollectionThroughputInfo.isPresent = true;
            }
        }

        private static void AddShareThroughput(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[WFConstants.BackendHeaders.ShareThroughput]))
            {
                rntbdRequest.shareThroughput.value.valueByte = (request.Headers[WFConstants.BackendHeaders.ShareThroughput].
                    Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
                    ? (byte) 0x01
                    : (byte) 0x00;
                rntbdRequest.shareThroughput.isPresent = true;
            }
        }

        private static void AddIsReadOnlyScript(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.IsReadOnlyScript]))
            {
                rntbdRequest.isReadOnlyScript.value.valueByte = (request.Headers[HttpConstants.HttpHeaders.IsReadOnlyScript].
                    Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
                    ? (byte)0x01
                    : (byte)0x00;
                rntbdRequest.isReadOnlyScript.isPresent = true;
            }
        }

#if !COSMOSCLIENT
        private static void AddIsAutoScaleRequest(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.IsAutoScaleRequest]))
            {
                rntbdRequest.isAutoScaleRequest.value.valueByte = (request.Headers[HttpConstants.HttpHeaders.IsAutoScaleRequest].
                    Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
                    ? (byte)0x01
                    : (byte)0x00;
                rntbdRequest.isAutoScaleRequest.isPresent = true;
            }
        }
#endif

        private static void AddCanOfferReplaceComplete(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.CanOfferReplaceComplete]))
            {
                rntbdRequest.canOfferReplaceComplete.value.valueByte = (request.Headers[HttpConstants.HttpHeaders.CanOfferReplaceComplete].
                    Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
                    ? (byte)0x01
                    : (byte)0x00;
                rntbdRequest.canOfferReplaceComplete.isPresent = true;
            }
        }

        
        private static void AddIgnoreSystemLoweringMaxThroughput(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.IgnoreSystemLoweringMaxThroughput]))
            {
                rntbdRequest.ignoreSystemLoweringMaxThroughput.value.valueByte = (request.Headers[HttpConstants.HttpHeaders.IgnoreSystemLoweringMaxThroughput].
                    Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
                    ? (byte)0x01
                    : (byte)0x00;
                rntbdRequest.ignoreSystemLoweringMaxThroughput.isPresent = true;
            }
        }

        private static void AddUpdateMaxthroughputEverProvisioned(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.UpdateMaxThroughputEverProvisioned]))
            {
                string value = request.Headers[HttpConstants.HttpHeaders.UpdateMaxThroughputEverProvisioned];
                int valueInt;
                if (!Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out valueInt))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidUpdateMaxthroughputEverProvisioned, value));
                }

                if (valueInt >= 0)
                {
                    rntbdRequest.updateMaxThroughputEverProvisioned.value.valueULong = (UInt32)valueInt;
                }
                else
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidUpdateMaxthroughputEverProvisioned, value));
                }

                rntbdRequest.updateMaxThroughputEverProvisioned.isPresent = true;
            }
        }

        private static void AddGetAllPartitionKeyStatistics(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.GetAllPartitionKeyStatistics]))
            {
                rntbdRequest.getAllPartitionKeyStatistics.value.valueByte = (request.Headers[HttpConstants.HttpHeaders.GetAllPartitionKeyStatistics].
                    Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
                    ? (byte)0x01
                    : (byte)0x00;
                rntbdRequest.getAllPartitionKeyStatistics.isPresent = true;
            }
        }

        private static void AddResponseContinuationTokenLimitInKb(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.ResponseContinuationTokenLimitInKB]))
            {
                string value = request.Headers[HttpConstants.HttpHeaders.ResponseContinuationTokenLimitInKB];
                if (!Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int valueInt))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidPageSize, value));
                }

                if (valueInt >= 0)
                {
                    rntbdRequest.responseContinuationTokenLimitInKb.value.valueULong = (UInt32) valueInt;
                }
                else
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidResponseContinuationTokenLimit, value));
                }

                rntbdRequest.responseContinuationTokenLimitInKb.isPresent = true;
            }
        }

        private static void AddRemoteStorageType(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[WFConstants.BackendHeaders.RemoteStorageType]))
            {
                RntbdConstants.RntbdRemoteStorageType rntbdRemoteStorageType = RntbdConstants.RntbdRemoteStorageType.Invalid;
                if (!Enum.TryParse(request.Headers[WFConstants.BackendHeaders.RemoteStorageType], true, out RemoteStorageType remoteStorageType))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                        request.Headers[WFConstants.BackendHeaders.RemoteStorageType], typeof(RemoteStorageType).Name));
                }

                switch (remoteStorageType)
                {
                case RemoteStorageType.Standard:
                    rntbdRemoteStorageType = RntbdConstants.RntbdRemoteStorageType.Standard;
                    break;
                case RemoteStorageType.Premium:
                    rntbdRemoteStorageType = RntbdConstants.RntbdRemoteStorageType.Premium;
                    break;
                default:
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                        request.Headers[WFConstants.BackendHeaders.RemoteStorageType], typeof(RemoteStorageType).Name));
                }

                rntbdRequest.remoteStorageType.value.valueByte = (byte) rntbdRemoteStorageType;
                rntbdRequest.remoteStorageType.isPresent = true;
            }
        }

        private static void AddCollectionChildResourceNameLimitInBytes(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[WFConstants.BackendHeaders.CollectionChildResourceNameLimitInBytes];
            if (!string.IsNullOrEmpty(headerValue))
            {
                if (!Int32.TryParse(headerValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out rntbdRequest.collectionChildResourceNameLimitInBytes.value.valueLong))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture,
                        RMResources.InvalidHeaderValue,
                        headerValue,
                        WFConstants.BackendHeaders.CollectionChildResourceNameLimitInBytes));
                }

                rntbdRequest.collectionChildResourceNameLimitInBytes.isPresent = true;
            }
        }

        private static void AddCollectionChildResourceContentLengthLimitInKB(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[WFConstants.BackendHeaders.CollectionChildResourceContentLimitInKB];
            if (!string.IsNullOrEmpty(headerValue))
            {
                if (!Int32.TryParse(headerValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out rntbdRequest.collectionChildResourceContentLengthLimitInKB.value.valueLong))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture,
                        RMResources.InvalidHeaderValue,
                        headerValue,
                        WFConstants.BackendHeaders.CollectionChildResourceContentLimitInKB));
                }

                rntbdRequest.collectionChildResourceContentLengthLimitInKB.isPresent = true;
            }
        }

        private static void AddUniqueIndexNameEncodingMode(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[WFConstants.BackendHeaders.UniqueIndexNameEncodingMode];
            if (!string.IsNullOrEmpty(headerValue))
            {
                if (!Byte.TryParse(headerValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out rntbdRequest.uniqueIndexNameEncodingMode.value.valueByte))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture,
                        RMResources.InvalidHeaderValue,
                        headerValue,
                        WFConstants.BackendHeaders.UniqueIndexNameEncodingMode));
                }

                rntbdRequest.uniqueIndexNameEncodingMode.isPresent = true;
            }
        }

        private static void AddUniqueIndexReIndexingState(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[WFConstants.BackendHeaders.UniqueIndexReIndexingState];
            if (!string.IsNullOrEmpty(headerValue))
            {
                if (!Byte.TryParse(headerValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out rntbdRequest.uniqueIndexReIndexingState.value.valueByte))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture,
                        RMResources.InvalidHeaderValue,
                        headerValue,
                        WFConstants.BackendHeaders.UniqueIndexReIndexingState));
                }

                rntbdRequest.uniqueIndexReIndexingState.isPresent = true;
            }
        }

        private static void AddCollectionRemoteStorageSecurityIdentifier(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[HttpConstants.HttpHeaders.CollectionRemoteStorageSecurityIdentifier];
            if (!string.IsNullOrEmpty(headerValue))
            {
                rntbdRequest.collectionRemoteStorageSecurityIdentifier.value.valueBytes = BytesSerializer.GetBytesForString(headerValue, rntbdRequest);
                rntbdRequest.collectionRemoteStorageSecurityIdentifier.isPresent = true;
            }
        }

        private static void AddIsUserRequest(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[WFConstants.BackendHeaders.IsUserRequest]))
            {
                rntbdRequest.isUserRequest.value.valueByte = (request.Headers[WFConstants.BackendHeaders.IsUserRequest].
                    Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
                    ? (byte)0x01
                    : (byte)0x00;
                rntbdRequest.isUserRequest.isPresent = true;
            }
        }

        private static void AddPreserveFullContent(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[WFConstants.BackendHeaders.PreserveFullContent]))
            {
                rntbdRequest.preserveFullContent.value.valueByte = (request.Headers[WFConstants.BackendHeaders.PreserveFullContent].
                    Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
                    ? (byte) 0x01
                    : (byte) 0x00;
                rntbdRequest.preserveFullContent.isPresent = true;
            }
        }

        private static void AddForceSideBySideIndexMigration(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[WFConstants.BackendHeaders.ForceSideBySideIndexMigration]))
            {
                rntbdRequest.forceSideBySideIndexMigration.value.valueByte = (request.Headers[WFConstants.BackendHeaders.ForceSideBySideIndexMigration].
                    Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
                    ? (byte)0x01
                    : (byte)0x00;
                rntbdRequest.forceSideBySideIndexMigration.isPresent = true;
            }
        }

        private static void AddIsRUPerGBEnforcementRequest(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.IsRUPerGBEnforcementRequest]))
            {
                rntbdRequest.isRUPerGBEnforcementRequest.value.valueByte = (request.Headers[HttpConstants.HttpHeaders.IsRUPerGBEnforcementRequest].
                    Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
                    ? (byte)0x01
                    : (byte)0x00;
                rntbdRequest.isRUPerGBEnforcementRequest.isPresent = true;
            }
        }

        private static void AddIsOfferStorageRefreshRequest(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.IsOfferStorageRefreshRequest]))
            {
                rntbdRequest.isofferStorageRefreshRequest.value.valueByte = (request.Headers[HttpConstants.HttpHeaders.IsOfferStorageRefreshRequest].
                    Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
                    ? (byte)0x01
                    : (byte)0x00;
                rntbdRequest.isofferStorageRefreshRequest.isPresent = true;
            }
        }

        private static void AddIsMigrateOfferToManualThroughputRequest(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.MigrateOfferToManualThroughput]))
            {
                rntbdRequest.migrateOfferToManualThroughput.value.valueByte = (request.Headers[HttpConstants.HttpHeaders.MigrateOfferToManualThroughput].
                    Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
                    ? (byte)0x01
                    : (byte)0x00;
                rntbdRequest.migrateOfferToManualThroughput.isPresent = true;
            }
        }

        private static void AddIsMigrateOfferToAutopilotRequest(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.MigrateOfferToAutopilot]))
            {
                rntbdRequest.migrateOfferToAutopilot.value.valueByte = (request.Headers[HttpConstants.HttpHeaders.MigrateOfferToAutopilot].
                    Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
                    ? (byte)0x01
                    : (byte)0x00;
                rntbdRequest.migrateOfferToAutopilot.isPresent = true;
            }
        }

        private static void AddEnumerationDirection(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (request.Properties != null && request.Properties.TryGetValue(
                    HttpConstants.HttpHeaders.EnumerationDirection,
                    out object enumerationDirectionObject))
            {
                byte? scanDirection = enumerationDirectionObject as byte?;
                if (scanDirection == null)
                {
                    throw new BadRequestException(
                        String.Format(
                            CultureInfo.CurrentUICulture,
                            RMResources.InvalidEnumValue,
                            HttpConstants.HttpHeaders.EnumerationDirection,
                            nameof(EnumerationDirection)));
                }
                else
                {
                    rntbdRequest.enumerationDirection.value.valueByte = scanDirection.Value;
                    rntbdRequest.enumerationDirection.isPresent = true;
                }
            }
            else if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.EnumerationDirection]))
            {
                RntbdConstants.RntdbEnumerationDirection rntdbEnumerationDirection = RntbdConstants.RntdbEnumerationDirection.Invalid;
                if (!Enum.TryParse(request.Headers[HttpConstants.HttpHeaders.EnumerationDirection], true, out EnumerationDirection enumerationDirection))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                        request.Headers[HttpConstants.HttpHeaders.EnumerationDirection], nameof(EnumerationDirection)));
                }

                switch (enumerationDirection)
                {
                    case EnumerationDirection.Forward:
                        rntdbEnumerationDirection = RntbdConstants.RntdbEnumerationDirection.Forward;
                        break;
                    case EnumerationDirection.Reverse:
                        rntdbEnumerationDirection = RntbdConstants.RntdbEnumerationDirection.Reverse;
                        break;
                    default:
                        throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                            request.Headers[HttpConstants.HttpHeaders.EnumerationDirection], typeof(EnumerationDirection).Name));
                }

                rntbdRequest.enumerationDirection.value.valueByte = (byte)rntdbEnumerationDirection;
                rntbdRequest.enumerationDirection.isPresent = true;
            }
        }

        private static void AddStartAndEndKeys(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (request.Properties == null)
            {
                TransportSerialization.AddStartAndEndKeysFromHeaders(request, rntbdRequest);
                return;
            }

            RntbdConstants.RntdbReadFeedKeyType? readFeedKeyType = null;
            if (request.Properties.TryGetValue(HttpConstants.HttpHeaders.ReadFeedKeyType, out object requestObject))
            {
                if (!(requestObject is byte))
                {
                    throw new ArgumentOutOfRangeException(HttpConstants.HttpHeaders.ReadFeedKeyType);
                }

                rntbdRequest.readFeedKeyType.value.valueByte = (byte)requestObject;
                rntbdRequest.readFeedKeyType.isPresent = true;
                readFeedKeyType = (RntbdConstants.RntdbReadFeedKeyType)requestObject;
            }

            if (readFeedKeyType == RntbdConstants.RntdbReadFeedKeyType.ResourceId)
            {
                TransportSerialization.SetBytesValue(request, HttpConstants.HttpHeaders.StartId, rntbdRequest.StartId);
                TransportSerialization.SetBytesValue(request, HttpConstants.HttpHeaders.EndId, rntbdRequest.EndId);
            }
            else if (readFeedKeyType == RntbdConstants.RntdbReadFeedKeyType.EffectivePartitionKey)
            {

                TransportSerialization.SetBytesValue(request, HttpConstants.HttpHeaders.StartEpk, rntbdRequest.StartEpk);
                TransportSerialization.SetBytesValue(request, HttpConstants.HttpHeaders.EndEpk, rntbdRequest.EndEpk);
            }
        }

        private static void AddStartAndEndKeysFromHeaders(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.ReadFeedKeyType]))
            {
                RntbdConstants.RntdbReadFeedKeyType rntdbReadFeedKeyType = RntbdConstants.RntdbReadFeedKeyType.Invalid;
                if (!Enum.TryParse(request.Headers[HttpConstants.HttpHeaders.ReadFeedKeyType], true, out ReadFeedKeyType readFeedKeyType))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                        request.Headers[HttpConstants.HttpHeaders.ReadFeedKeyType], nameof(ReadFeedKeyType)));
                }

                switch (readFeedKeyType)
                {
                    case ReadFeedKeyType.ResourceId:
                        rntdbReadFeedKeyType = RntbdConstants.RntdbReadFeedKeyType.ResourceId;
                        break;
                    case ReadFeedKeyType.EffectivePartitionKey:
                        rntdbReadFeedKeyType = RntbdConstants.RntdbReadFeedKeyType.EffectivePartitionKey;
                        break;
                    default:
                        throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                            request.Headers[HttpConstants.HttpHeaders.ReadFeedKeyType], typeof(ReadFeedKeyType).Name));
                }

                rntbdRequest.readFeedKeyType.value.valueByte = (byte)rntdbReadFeedKeyType;
                rntbdRequest.readFeedKeyType.isPresent = true;
            }

            string startId = request.Headers[HttpConstants.HttpHeaders.StartId];
            if (!string.IsNullOrEmpty(startId))
            {
                rntbdRequest.StartId.value.valueBytes = System.Convert.FromBase64String(startId);
                rntbdRequest.StartId.isPresent = true;
            }

            string endId = request.Headers[HttpConstants.HttpHeaders.EndId];
            if (!string.IsNullOrEmpty(endId))
            {
                rntbdRequest.EndId.value.valueBytes = System.Convert.FromBase64String(endId);
                rntbdRequest.EndId.isPresent = true;
            }

            string startEpk = request.Headers[HttpConstants.HttpHeaders.StartEpk];
            if (!string.IsNullOrEmpty(startEpk))
            {
                rntbdRequest.StartEpk.value.valueBytes = System.Convert.FromBase64String(startEpk);
                rntbdRequest.StartEpk.isPresent = true;
            }

            string endEpk = request.Headers[HttpConstants.HttpHeaders.EndEpk];
            if (!string.IsNullOrEmpty(endEpk))
            {
                rntbdRequest.EndEpk.value.valueBytes = System.Convert.FromBase64String(endEpk);
                rntbdRequest.EndEpk.isPresent = true;
            }
        }

        private static void SetBytesValue(DocumentServiceRequest request, string headerName, RntbdToken token)
        {
            if (request.Properties.TryGetValue(headerName, out object requestObject))
            {
                byte[] endEpk = requestObject as byte[];
                if (endEpk == null)
                {
                    throw new ArgumentOutOfRangeException(headerName);
                }

                token.value.valueBytes = endEpk;
                token.isPresent = true;
            }
        }

        private static void AddContentSerializationFormat(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.ContentSerializationFormat]))
            {
                RntbdConstants.RntbdContentSerializationFormat rntbdContentSerializationFormat = RntbdConstants.RntbdContentSerializationFormat.Invalid;

                if (!Enum.TryParse<ContentSerializationFormat>(request.Headers[HttpConstants.HttpHeaders.ContentSerializationFormat], true, out ContentSerializationFormat contentSerializationFormat))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                        request.Headers[HttpConstants.HttpHeaders.ContentSerializationFormat], nameof(ContentSerializationFormat)));
                }

                switch (contentSerializationFormat)
                {
                    case ContentSerializationFormat.JsonText:
                        rntbdContentSerializationFormat = RntbdConstants.RntbdContentSerializationFormat.JsonText;
                        break;
                    case ContentSerializationFormat.CosmosBinary:
                        rntbdContentSerializationFormat = RntbdConstants.RntbdContentSerializationFormat.CosmosBinary;
                        break;
                    case ContentSerializationFormat.HybridRow:
                        rntbdContentSerializationFormat = RntbdConstants.RntbdContentSerializationFormat.HybridRow;
                        break;
                    default:
                        throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                            request.Headers[HttpConstants.HttpHeaders.ContentSerializationFormat], nameof(ContentSerializationFormat)));
                }

                rntbdRequest.contentSerializationFormat.value.valueByte = (byte)rntbdContentSerializationFormat;
                rntbdRequest.contentSerializationFormat.isPresent = true;
            }
        }

        private static void FillTokenFromHeader(DocumentServiceRequest request, string headerName, RntbdToken token, RntbdConstants.Request rntbdRequest)
        {
            string value = request.Headers[headerName];
            if (string.IsNullOrEmpty(value) && request.Properties != null)
            {
                object propertyValue;
                if (request.Properties.TryGetValue(headerName, out propertyValue))
                {
                    value = (string)propertyValue;
                }
            }

            if (!string.IsNullOrEmpty(value))
            {
                switch (token.GetTokenType())
                {
                case RntbdTokenTypes.SmallString:
                case RntbdTokenTypes.String:
                case RntbdTokenTypes.ULongString:
                    token.value.valueBytes = BytesSerializer.GetBytesForString(value, rntbdRequest);
                    break;
                case RntbdTokenTypes.ULong:
                    if (!uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint valueULong))
                    {
                        throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidHeaderValue, value, headerName));
                    }

                    token.value.valueULong = valueULong;
                    break;
                case RntbdTokenTypes.Long:
                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int valueLong))
                    {
                        throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidHeaderValue, value, headerName));
                    }

                    token.value.valueLong = valueLong;
                    break;
                case RntbdTokenTypes.Double:
                    token.value.valueDouble = double.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case RntbdTokenTypes.LongLong:
                    if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long valueLongLong))
                    {
                        throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidHeaderValue, value, headerName));
                    }

                    token.value.valueLongLong = valueLongLong;
                    break;
                case RntbdTokenTypes.Byte:
                     token.value.valueByte = value.Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase) ? (byte)0x01 : (byte)0x00;
                     break;
                default:
                    Debug.Assert(false, "Recognized header has neither special-case nor default handling to convert"
                        + " from header string to RNTBD token.");
                    throw new BadRequestException();
                }

                token.isPresent = true;
            }
        }

        private static void AddExcludeSystemProperties(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[WFConstants.BackendHeaders.ExcludeSystemProperties]))
            {
                rntbdRequest.excludeSystemProperties.value.valueByte = (request.Headers[WFConstants.BackendHeaders.ExcludeSystemProperties].
                    Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
                    ? (byte)0x01
                    : (byte)0x00;
                rntbdRequest.excludeSystemProperties.isPresent = true;
            }
        }

        private static void AddFanoutOperationStateHeader(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string value = request.Headers[WFConstants.BackendHeaders.FanoutOperationState];
            if (!string.IsNullOrEmpty(value))
            {
                if (!Enum.TryParse(value, true, out FanoutOperationState state))
                {
                    throw new BadRequestException(
                        String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue, value, nameof(FanoutOperationState)));
                }

                RntbdConstants.RntbdFanoutOperationState rntbdState;
                switch (state)
                {
                    case FanoutOperationState.Started:
                        rntbdState = RntbdConstants.RntbdFanoutOperationState.Started;
                        break;

                    case FanoutOperationState.Completed:
                        rntbdState = RntbdConstants.RntbdFanoutOperationState.Completed;
                        break;

                    default:
                        throw new BadRequestException(
                            String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue, value, nameof(FanoutOperationState)));
                }

                rntbdRequest.FanoutOperationState.value.valueByte = (byte)rntbdState;
                rntbdRequest.FanoutOperationState.isPresent = true;
            }
        }

        private static void AddResourceTypes(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            string headerValue = request.Headers[WFConstants.BackendHeaders.ResourceTypes];
            if (!string.IsNullOrEmpty(headerValue))
            {
                rntbdRequest.resourceTypes.value.valueBytes = BytesSerializer.GetBytesForString(headerValue, rntbdRequest);
                rntbdRequest.resourceTypes.isPresent = true;
            }
        }

        private static void AddSystemDocumentTypeHeader(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.SystemDocumentType]))
            {
                RntbdConstants.RntbdSystemDocumentType rntbdSystemDocumentType = RntbdConstants.RntbdSystemDocumentType.Invalid;
                if (!Enum.TryParse(request.Headers[HttpConstants.HttpHeaders.SystemDocumentType], true, out SystemDocumentType systemDocumentType))
                {
                    throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                        request.Headers[HttpConstants.HttpHeaders.SystemDocumentType], nameof(SystemDocumentType)));
                }

                switch (systemDocumentType)
                {
                    case SystemDocumentType.PartitionKey:
                        rntbdSystemDocumentType = RntbdConstants.RntbdSystemDocumentType.PartitionKey;
                        break;
                    case SystemDocumentType.MaterializedViewLeaseDocument:
                        rntbdSystemDocumentType = RntbdConstants.RntbdSystemDocumentType.MaterializedViewLeaseDocument;
                        break;
                    default:
                        throw new BadRequestException(String.Format(CultureInfo.CurrentUICulture, RMResources.InvalidEnumValue,
                            request.Headers[HttpConstants.HttpHeaders.SystemDocumentType], typeof(SystemDocumentType).Name));
                }

                rntbdRequest.systemDocumentType.value.valueByte = (byte)rntbdSystemDocumentType;
                rntbdRequest.systemDocumentType.isPresent = true;
            }
        }

        private static void AddTransactionMetaData(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (request.Properties != null &&
                request.Properties.TryGetValue(WFConstants.BackendHeaders.TransactionId, out object transactionIdValue) &&
                request.Properties.TryGetValue(WFConstants.BackendHeaders.TransactionFirstRequest, out object isFirstRequestValue))
            {
                // read transaction id
                byte[] transactionId = transactionIdValue as byte[];
                if (transactionId == null)
                {
                    throw new ArgumentOutOfRangeException(WFConstants.BackendHeaders.TransactionId);
                }

                // read initial transactional request flag
                bool? isFirstRequest = isFirstRequestValue as bool?;
                if (!isFirstRequest.HasValue)
                {
                    throw new ArgumentOutOfRangeException(WFConstants.BackendHeaders.TransactionFirstRequest);
                }

                // set transaction id and initial request flag
                rntbdRequest.transactionId.value.valueBytes = transactionId;
                rntbdRequest.transactionId.isPresent = true;

                rntbdRequest.transactionFirstRequest.value.valueByte = ((bool)isFirstRequest) ? (byte)0x01 : (byte)0x00;
                rntbdRequest.transactionFirstRequest.isPresent = true;
            }
        }

        private static void AddTransactionCompletionFlag(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (request.Properties != null &&
                request.Properties.TryGetValue(WFConstants.BackendHeaders.TransactionCommit, out object isCommit))
            {
                bool? boolData = isCommit as bool?;
                if (!boolData.HasValue)
                {
                    throw new ArgumentOutOfRangeException(WFConstants.BackendHeaders.TransactionFirstRequest);
                }

                rntbdRequest.transactionCommit.value.valueByte = ((bool)boolData) ? (byte)0x01 : (byte)0x00;
                rntbdRequest.transactionCommit.isPresent = true;
            }
        }

        private static void AddUseSystemBudget(DocumentServiceRequest request, RntbdConstants.Request rntbdRequest)
        {
            if (!string.IsNullOrEmpty(request.Headers[WFConstants.BackendHeaders.UseSystemBudget]))
            {
                rntbdRequest.useSystemBudget.value.valueByte = (request.Headers[WFConstants.BackendHeaders.UseSystemBudget].
                    Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
                    ? (byte)0x01
                    : (byte)0x00;
                rntbdRequest.useSystemBudget.isPresent = true;
            }
        }
    }
}