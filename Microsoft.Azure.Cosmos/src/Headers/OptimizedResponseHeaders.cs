//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

// This is auto-generated code.

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    internal class OptimizedResponseHeaders : INameValueCollection
    {
        private readonly Lazy<Dictionary<string, string>> lazyNotCommonHeaders;

        public string A_IM { get; set; }
        public string AddResourcePropertiesToResponse { get; set; }
        public string AllowTentativeWrites { get; set; }
        public string Authorization { get; set; }
        public string BinaryId { get; set; }
        public string BinaryPassthroughRequest { get; set; }
        public string BindReplicaDirective { get; set; }
        public string CanCharge { get; set; }
        public string CanOfferReplaceComplete { get; set; }
        public string CanThrottle { get; set; }
        public string ChangeFeedStartFullFidelityIfNoneMatch { get; set; }
        public string ClientRetryAttemptCount { get; set; }
        public string CollectionChildResourceContentLimitInKB { get; set; }
        public string CollectionChildResourceNameLimitInBytes { get; set; }
        public string CollectionPartitionIndex { get; set; }
        public string CollectionRid { get; set; }
        public string CollectionSecurityIdentifier { get; set; }
        public string CollectionServiceIndex { get; set; }
        public string ConsistencyLevel { get; set; }
        public string ContentSerializationFormat { get; set; }
        public string Continuation { get; set; }
        public string DisableRUPerMinuteUsage { get; set; }
        public string EffectivePartitionKey { get; set; }
        public string EmitVerboseTracesInQuery { get; set; }
        public string EnableDynamicRidRangeAllocation { get; set; }
        public string EnableLogging { get; set; }
        public string EnableLowPrecisionOrderBy { get; set; }
        public string EnableScanInQuery { get; set; }
        public string EndEpk { get; set; }
        public string EndId { get; set; }
        public string EnumerationDirection { get; set; }
        public string ExcludeSystemProperties { get; set; }
        public string FanoutOperationState { get; set; }
        public string FilterBySchemaResourceId { get; set; }
        public string ForceQueryScan { get; set; }
        public string ForceSideBySideIndexMigration { get; set; }
        public string GatewaySignature { get; set; }
        public string GetAllPartitionKeyStatistics { get; set; }
        public string HttpDate { get; set; }
        public string IfMatch { get; set; }
        public string IfModifiedSince { get; set; }
        public string IfNoneMatch { get; set; }
        public string IgnoreSystemLoweringMaxThroughput { get; set; }
        public string IncludeTentativeWrites { get; set; }
        public string IndexingDirective { get; set; }
        public string IsBatchAtomic { get; set; }
        public string IsBatchOrdered { get; set; }
        public string IsClientEncrypted { get; set; }
        public string IsFanoutRequest { get; set; }
        public string IsOfferStorageRefreshRequest { get; set; }
        public string IsReadOnlyScript { get; set; }
        public string IsRetriedWriteRequest { get; set; }
        public string IsRUPerGBEnforcementRequest { get; set; }
        public string IsUserRequest { get; set; }
        public string MaxPollingIntervalMilliseconds { get; set; }
        public string MergeCheckPointGLSN { get; set; }
        public string MergeStaticId { get; set; }
        public string MigrateCollectionDirective { get; set; }
        public string MigrateOfferToAutopilot { get; set; }
        public string MigrateOfferToManualThroughput { get; set; }
        public string PageSize { get; set; }
        public string PartitionCount { get; set; }
        public string PartitionKey { get; set; }
        public string PartitionKeyRangeId { get; set; }
        public string PartitionResourceFilter { get; set; }
        public string PopulateCollectionThroughputInfo { get; set; }
        public string PopulateLogStoreInfo { get; set; }
        public string PopulatePartitionStatistics { get; set; }
        public string PopulateQueryMetrics { get; set; }
        public string PopulateQuotaInfo { get; set; }
        public string PopulateResourceCount { get; set; }
        public string PopulateUnflushedMergeEntryCount { get; set; }
        public string PostTriggerExclude { get; set; }
        public string PostTriggerInclude { get; set; }
        public string Prefer { get; set; }
        public string PreserveFullContent { get; set; }
        public string PreTriggerExclude { get; set; }
        public string PreTriggerInclude { get; set; }
        public string PrimaryMasterKey { get; set; }
        public string PrimaryReadonlyKey { get; set; }
        public string ProfileRequest { get; set; }
        public string ReadFeedKeyType { get; set; }
        public string RemainingTimeInMsOnClientRequest { get; set; }
        public string RemoteStorageType { get; set; }
        public string ResourceSchemaName { get; set; }
        public string ResourceTokenExpiry { get; set; }
        public string ResourceTypes { get; set; }
        public string ResponseContinuationTokenLimitInKB { get; set; }
        public string RestoreMetadataFilter { get; set; }
        public string RestoreParams { get; set; }
        public string RetriableWriteRequestId { get; set; }
        public string RetriableWriteRequestStartTimestamp { get; set; }
        public string SchemaHash { get; set; }
        public string SchemaOwnerRid { get; set; }
        public string SecondaryMasterKey { get; set; }
        public string SecondaryReadonlyKey { get; set; }
        public string SessionToken { get; set; }
        public string ShareThroughput { get; set; }
        public string ShouldBatchContinueOnError { get; set; }
        public string StartEpk { get; set; }
        public string StartId { get; set; }
        public string SupportSpatialLegacyCoordinates { get; set; }
        public string SystemDocumentType { get; set; }
        public string TargetGlobalCommittedLsn { get; set; }
        public string TargetLsn { get; set; }
        public string TimeToLiveInSeconds { get; set; }
        public string TransactionCommit { get; set; }
        public string TransactionId { get; set; }
        public string TransportRequestID { get; set; }
        public string TruncateMergeLogRequest { get; set; }
        public string UniqueIndexNameEncodingMode { get; set; }
        public string UniqueIndexReIndexingState { get; set; }
        public string UpdateMaxThroughputEverProvisioned { get; set; }
        public string UsePolygonsSmallerThanAHemisphere { get; set; }
        public string UseSystemBudget { get; set; }
        public string Version { get; set; }
        public string XDate { get; set; }

        public OptimizedResponseHeaders()
            : this(new Lazy<Dictionary<string, string>>(() => new Dictionary<string, string>()))
        {
        }

        private OptimizedResponseHeaders(Lazy<Dictionary<string, string>> notCommonHeaders)
        {
            this.lazyNotCommonHeaders = notCommonHeaders ?? throw new ArgumentNullException(nameof(notCommonHeaders));
        }

        public string this[string key] 
        { 
            get => this.Get(key); 
            set => this.Set(key, value);
        }

        public void Add(string key, string value)
        {
            this.Set(key, value);
        }

        public void Add(INameValueCollection collection)
        {
            foreach(string key in collection.Keys())
            {
                this.Set(key, collection[key]);
            }
        }

        public string[] AllKeys()
        {
            return this.Keys().ToArray();
        }

        public void Clear()
        {
            if (this.lazyNotCommonHeaders.IsValueCreated)
            {
                this.lazyNotCommonHeaders.Value.Clear();
            }

            this.A_IM = null;
            this.AddResourcePropertiesToResponse = null;
            this.AllowTentativeWrites = null;
            this.Authorization = null;
            this.BinaryId = null;
            this.BinaryPassthroughRequest = null;
            this.BindReplicaDirective = null;
            this.CanCharge = null;
            this.CanOfferReplaceComplete = null;
            this.CanThrottle = null;
            this.ChangeFeedStartFullFidelityIfNoneMatch = null;
            this.ClientRetryAttemptCount = null;
            this.CollectionChildResourceContentLimitInKB = null;
            this.CollectionChildResourceNameLimitInBytes = null;
            this.CollectionPartitionIndex = null;
            this.CollectionRid = null;
            this.CollectionSecurityIdentifier = null;
            this.CollectionServiceIndex = null;
            this.ConsistencyLevel = null;
            this.ContentSerializationFormat = null;
            this.Continuation = null;
            this.DisableRUPerMinuteUsage = null;
            this.EffectivePartitionKey = null;
            this.EmitVerboseTracesInQuery = null;
            this.EnableDynamicRidRangeAllocation = null;
            this.EnableLogging = null;
            this.EnableLowPrecisionOrderBy = null;
            this.EnableScanInQuery = null;
            this.EndEpk = null;
            this.EndId = null;
            this.EnumerationDirection = null;
            this.ExcludeSystemProperties = null;
            this.FanoutOperationState = null;
            this.FilterBySchemaResourceId = null;
            this.ForceQueryScan = null;
            this.ForceSideBySideIndexMigration = null;
            this.GatewaySignature = null;
            this.GetAllPartitionKeyStatistics = null;
            this.HttpDate = null;
            this.IfMatch = null;
            this.IfModifiedSince = null;
            this.IfNoneMatch = null;
            this.IgnoreSystemLoweringMaxThroughput = null;
            this.IncludeTentativeWrites = null;
            this.IndexingDirective = null;
            this.IsBatchAtomic = null;
            this.IsBatchOrdered = null;
            this.IsClientEncrypted = null;
            this.IsFanoutRequest = null;
            this.IsOfferStorageRefreshRequest = null;
            this.IsReadOnlyScript = null;
            this.IsRetriedWriteRequest = null;
            this.IsRUPerGBEnforcementRequest = null;
            this.IsUserRequest = null;
            this.MaxPollingIntervalMilliseconds = null;
            this.MergeCheckPointGLSN = null;
            this.MergeStaticId = null;
            this.MigrateCollectionDirective = null;
            this.MigrateOfferToAutopilot = null;
            this.MigrateOfferToManualThroughput = null;
            this.PageSize = null;
            this.PartitionCount = null;
            this.PartitionKey = null;
            this.PartitionKeyRangeId = null;
            this.PartitionResourceFilter = null;
            this.PopulateCollectionThroughputInfo = null;
            this.PopulateLogStoreInfo = null;
            this.PopulatePartitionStatistics = null;
            this.PopulateQueryMetrics = null;
            this.PopulateQuotaInfo = null;
            this.PopulateResourceCount = null;
            this.PopulateUnflushedMergeEntryCount = null;
            this.PostTriggerExclude = null;
            this.PostTriggerInclude = null;
            this.Prefer = null;
            this.PreserveFullContent = null;
            this.PreTriggerExclude = null;
            this.PreTriggerInclude = null;
            this.PrimaryMasterKey = null;
            this.PrimaryReadonlyKey = null;
            this.ProfileRequest = null;
            this.ReadFeedKeyType = null;
            this.RemainingTimeInMsOnClientRequest = null;
            this.RemoteStorageType = null;
            this.ResourceSchemaName = null;
            this.ResourceTokenExpiry = null;
            this.ResourceTypes = null;
            this.ResponseContinuationTokenLimitInKB = null;
            this.RestoreMetadataFilter = null;
            this.RestoreParams = null;
            this.RetriableWriteRequestId = null;
            this.RetriableWriteRequestStartTimestamp = null;
            this.SchemaHash = null;
            this.SchemaOwnerRid = null;
            this.SecondaryMasterKey = null;
            this.SecondaryReadonlyKey = null;
            this.SessionToken = null;
            this.ShareThroughput = null;
            this.ShouldBatchContinueOnError = null;
            this.StartEpk = null;
            this.StartId = null;
            this.SupportSpatialLegacyCoordinates = null;
            this.SystemDocumentType = null;
            this.TargetGlobalCommittedLsn = null;
            this.TargetLsn = null;
            this.TimeToLiveInSeconds = null;
            this.TransactionCommit = null;
            this.TransactionId = null;
            this.TransportRequestID = null;
            this.TruncateMergeLogRequest = null;
            this.UniqueIndexNameEncodingMode = null;
            this.UniqueIndexReIndexingState = null;
            this.UpdateMaxThroughputEverProvisioned = null;
            this.UsePolygonsSmallerThanAHemisphere = null;
            this.UseSystemBudget = null;
            this.Version = null;
            this.XDate = null;

        }

        public INameValueCollection Clone()
        {
            Lazy<Dictionary<string, string>> cloneNotCommonHeaders = new Lazy<Dictionary<string, string>>(() => new Dictionary<string, string>());
            if (this.lazyNotCommonHeaders.IsValueCreated)
            {
                foreach (KeyValuePair<string, string> notCommonHeader in this.lazyNotCommonHeaders.Value)
                {
                    cloneNotCommonHeaders.Value[notCommonHeader.Key] = notCommonHeader.Value;
                }
            }

            OptimizedResponseHeaders cloneHeaders = new OptimizedResponseHeaders(cloneNotCommonHeaders)
            {
                A_IM = this.A_IM,
                AddResourcePropertiesToResponse = this.AddResourcePropertiesToResponse,
                AllowTentativeWrites = this.AllowTentativeWrites,
                Authorization = this.Authorization,
                BinaryId = this.BinaryId,
                BinaryPassthroughRequest = this.BinaryPassthroughRequest,
                BindReplicaDirective = this.BindReplicaDirective,
                CanCharge = this.CanCharge,
                CanOfferReplaceComplete = this.CanOfferReplaceComplete,
                CanThrottle = this.CanThrottle,
                ChangeFeedStartFullFidelityIfNoneMatch = this.ChangeFeedStartFullFidelityIfNoneMatch,
                ClientRetryAttemptCount = this.ClientRetryAttemptCount,
                CollectionChildResourceContentLimitInKB = this.CollectionChildResourceContentLimitInKB,
                CollectionChildResourceNameLimitInBytes = this.CollectionChildResourceNameLimitInBytes,
                CollectionPartitionIndex = this.CollectionPartitionIndex,
                CollectionRid = this.CollectionRid,
                CollectionSecurityIdentifier = this.CollectionSecurityIdentifier,
                CollectionServiceIndex = this.CollectionServiceIndex,
                ConsistencyLevel = this.ConsistencyLevel,
                ContentSerializationFormat = this.ContentSerializationFormat,
                Continuation = this.Continuation,
                DisableRUPerMinuteUsage = this.DisableRUPerMinuteUsage,
                EffectivePartitionKey = this.EffectivePartitionKey,
                EmitVerboseTracesInQuery = this.EmitVerboseTracesInQuery,
                EnableDynamicRidRangeAllocation = this.EnableDynamicRidRangeAllocation,
                EnableLogging = this.EnableLogging,
                EnableLowPrecisionOrderBy = this.EnableLowPrecisionOrderBy,
                EnableScanInQuery = this.EnableScanInQuery,
                EndEpk = this.EndEpk,
                EndId = this.EndId,
                EnumerationDirection = this.EnumerationDirection,
                ExcludeSystemProperties = this.ExcludeSystemProperties,
                FanoutOperationState = this.FanoutOperationState,
                FilterBySchemaResourceId = this.FilterBySchemaResourceId,
                ForceQueryScan = this.ForceQueryScan,
                ForceSideBySideIndexMigration = this.ForceSideBySideIndexMigration,
                GatewaySignature = this.GatewaySignature,
                GetAllPartitionKeyStatistics = this.GetAllPartitionKeyStatistics,
                HttpDate = this.HttpDate,
                IfMatch = this.IfMatch,
                IfModifiedSince = this.IfModifiedSince,
                IfNoneMatch = this.IfNoneMatch,
                IgnoreSystemLoweringMaxThroughput = this.IgnoreSystemLoweringMaxThroughput,
                IncludeTentativeWrites = this.IncludeTentativeWrites,
                IndexingDirective = this.IndexingDirective,
                IsBatchAtomic = this.IsBatchAtomic,
                IsBatchOrdered = this.IsBatchOrdered,
                IsClientEncrypted = this.IsClientEncrypted,
                IsFanoutRequest = this.IsFanoutRequest,
                IsOfferStorageRefreshRequest = this.IsOfferStorageRefreshRequest,
                IsReadOnlyScript = this.IsReadOnlyScript,
                IsRetriedWriteRequest = this.IsRetriedWriteRequest,
                IsRUPerGBEnforcementRequest = this.IsRUPerGBEnforcementRequest,
                IsUserRequest = this.IsUserRequest,
                MaxPollingIntervalMilliseconds = this.MaxPollingIntervalMilliseconds,
                MergeCheckPointGLSN = this.MergeCheckPointGLSN,
                MergeStaticId = this.MergeStaticId,
                MigrateCollectionDirective = this.MigrateCollectionDirective,
                MigrateOfferToAutopilot = this.MigrateOfferToAutopilot,
                MigrateOfferToManualThroughput = this.MigrateOfferToManualThroughput,
                PageSize = this.PageSize,
                PartitionCount = this.PartitionCount,
                PartitionKey = this.PartitionKey,
                PartitionKeyRangeId = this.PartitionKeyRangeId,
                PartitionResourceFilter = this.PartitionResourceFilter,
                PopulateCollectionThroughputInfo = this.PopulateCollectionThroughputInfo,
                PopulateLogStoreInfo = this.PopulateLogStoreInfo,
                PopulatePartitionStatistics = this.PopulatePartitionStatistics,
                PopulateQueryMetrics = this.PopulateQueryMetrics,
                PopulateQuotaInfo = this.PopulateQuotaInfo,
                PopulateResourceCount = this.PopulateResourceCount,
                PopulateUnflushedMergeEntryCount = this.PopulateUnflushedMergeEntryCount,
                PostTriggerExclude = this.PostTriggerExclude,
                PostTriggerInclude = this.PostTriggerInclude,
                Prefer = this.Prefer,
                PreserveFullContent = this.PreserveFullContent,
                PreTriggerExclude = this.PreTriggerExclude,
                PreTriggerInclude = this.PreTriggerInclude,
                PrimaryMasterKey = this.PrimaryMasterKey,
                PrimaryReadonlyKey = this.PrimaryReadonlyKey,
                ProfileRequest = this.ProfileRequest,
                ReadFeedKeyType = this.ReadFeedKeyType,
                RemainingTimeInMsOnClientRequest = this.RemainingTimeInMsOnClientRequest,
                RemoteStorageType = this.RemoteStorageType,
                ResourceSchemaName = this.ResourceSchemaName,
                ResourceTokenExpiry = this.ResourceTokenExpiry,
                ResourceTypes = this.ResourceTypes,
                ResponseContinuationTokenLimitInKB = this.ResponseContinuationTokenLimitInKB,
                RestoreMetadataFilter = this.RestoreMetadataFilter,
                RestoreParams = this.RestoreParams,
                RetriableWriteRequestId = this.RetriableWriteRequestId,
                RetriableWriteRequestStartTimestamp = this.RetriableWriteRequestStartTimestamp,
                SchemaHash = this.SchemaHash,
                SchemaOwnerRid = this.SchemaOwnerRid,
                SecondaryMasterKey = this.SecondaryMasterKey,
                SecondaryReadonlyKey = this.SecondaryReadonlyKey,
                SessionToken = this.SessionToken,
                ShareThroughput = this.ShareThroughput,
                ShouldBatchContinueOnError = this.ShouldBatchContinueOnError,
                StartEpk = this.StartEpk,
                StartId = this.StartId,
                SupportSpatialLegacyCoordinates = this.SupportSpatialLegacyCoordinates,
                SystemDocumentType = this.SystemDocumentType,
                TargetGlobalCommittedLsn = this.TargetGlobalCommittedLsn,
                TargetLsn = this.TargetLsn,
                TimeToLiveInSeconds = this.TimeToLiveInSeconds,
                TransactionCommit = this.TransactionCommit,
                TransactionId = this.TransactionId,
                TransportRequestID = this.TransportRequestID,
                TruncateMergeLogRequest = this.TruncateMergeLogRequest,
                UniqueIndexNameEncodingMode = this.UniqueIndexNameEncodingMode,
                UniqueIndexReIndexingState = this.UniqueIndexReIndexingState,
                UpdateMaxThroughputEverProvisioned = this.UpdateMaxThroughputEverProvisioned,
                UsePolygonsSmallerThanAHemisphere = this.UsePolygonsSmallerThanAHemisphere,
                UseSystemBudget = this.UseSystemBudget,
                Version = this.Version,
                XDate = this.XDate,
            };

            return cloneHeaders;
        }

        public int Count()
        {
            return this.Keys().Count();
        }

        public IEnumerator GetEnumerator()
        {
            return this.Keys().GetEnumerator();
        }

        public string[] GetValues(string key)
        {
            string value = this.Get(key);
            if(value != null){
                return new string[] { value };
            }
            
            return null;
        }

        public IEnumerable<string> Keys()
        {
                if (this.A_IM != null)
                {
                    yield return this.A_IM;
                }
                if (this.AddResourcePropertiesToResponse != null)
                {
                    yield return this.AddResourcePropertiesToResponse;
                }
                if (this.AllowTentativeWrites != null)
                {
                    yield return this.AllowTentativeWrites;
                }
                if (this.Authorization != null)
                {
                    yield return this.Authorization;
                }
                if (this.BinaryId != null)
                {
                    yield return this.BinaryId;
                }
                if (this.BinaryPassthroughRequest != null)
                {
                    yield return this.BinaryPassthroughRequest;
                }
                if (this.BindReplicaDirective != null)
                {
                    yield return this.BindReplicaDirective;
                }
                if (this.CanCharge != null)
                {
                    yield return this.CanCharge;
                }
                if (this.CanOfferReplaceComplete != null)
                {
                    yield return this.CanOfferReplaceComplete;
                }
                if (this.CanThrottle != null)
                {
                    yield return this.CanThrottle;
                }
                if (this.ChangeFeedStartFullFidelityIfNoneMatch != null)
                {
                    yield return this.ChangeFeedStartFullFidelityIfNoneMatch;
                }
                if (this.ClientRetryAttemptCount != null)
                {
                    yield return this.ClientRetryAttemptCount;
                }
                if (this.CollectionChildResourceContentLimitInKB != null)
                {
                    yield return this.CollectionChildResourceContentLimitInKB;
                }
                if (this.CollectionChildResourceNameLimitInBytes != null)
                {
                    yield return this.CollectionChildResourceNameLimitInBytes;
                }
                if (this.CollectionPartitionIndex != null)
                {
                    yield return this.CollectionPartitionIndex;
                }
                if (this.CollectionRid != null)
                {
                    yield return this.CollectionRid;
                }
                if (this.CollectionSecurityIdentifier != null)
                {
                    yield return this.CollectionSecurityIdentifier;
                }
                if (this.CollectionServiceIndex != null)
                {
                    yield return this.CollectionServiceIndex;
                }
                if (this.ConsistencyLevel != null)
                {
                    yield return this.ConsistencyLevel;
                }
                if (this.ContentSerializationFormat != null)
                {
                    yield return this.ContentSerializationFormat;
                }
                if (this.Continuation != null)
                {
                    yield return this.Continuation;
                }
                if (this.DisableRUPerMinuteUsage != null)
                {
                    yield return this.DisableRUPerMinuteUsage;
                }
                if (this.EffectivePartitionKey != null)
                {
                    yield return this.EffectivePartitionKey;
                }
                if (this.EmitVerboseTracesInQuery != null)
                {
                    yield return this.EmitVerboseTracesInQuery;
                }
                if (this.EnableDynamicRidRangeAllocation != null)
                {
                    yield return this.EnableDynamicRidRangeAllocation;
                }
                if (this.EnableLogging != null)
                {
                    yield return this.EnableLogging;
                }
                if (this.EnableLowPrecisionOrderBy != null)
                {
                    yield return this.EnableLowPrecisionOrderBy;
                }
                if (this.EnableScanInQuery != null)
                {
                    yield return this.EnableScanInQuery;
                }
                if (this.EndEpk != null)
                {
                    yield return this.EndEpk;
                }
                if (this.EndId != null)
                {
                    yield return this.EndId;
                }
                if (this.EnumerationDirection != null)
                {
                    yield return this.EnumerationDirection;
                }
                if (this.ExcludeSystemProperties != null)
                {
                    yield return this.ExcludeSystemProperties;
                }
                if (this.FanoutOperationState != null)
                {
                    yield return this.FanoutOperationState;
                }
                if (this.FilterBySchemaResourceId != null)
                {
                    yield return this.FilterBySchemaResourceId;
                }
                if (this.ForceQueryScan != null)
                {
                    yield return this.ForceQueryScan;
                }
                if (this.ForceSideBySideIndexMigration != null)
                {
                    yield return this.ForceSideBySideIndexMigration;
                }
                if (this.GatewaySignature != null)
                {
                    yield return this.GatewaySignature;
                }
                if (this.GetAllPartitionKeyStatistics != null)
                {
                    yield return this.GetAllPartitionKeyStatistics;
                }
                if (this.HttpDate != null)
                {
                    yield return this.HttpDate;
                }
                if (this.IfMatch != null)
                {
                    yield return this.IfMatch;
                }
                if (this.IfModifiedSince != null)
                {
                    yield return this.IfModifiedSince;
                }
                if (this.IfNoneMatch != null)
                {
                    yield return this.IfNoneMatch;
                }
                if (this.IgnoreSystemLoweringMaxThroughput != null)
                {
                    yield return this.IgnoreSystemLoweringMaxThroughput;
                }
                if (this.IncludeTentativeWrites != null)
                {
                    yield return this.IncludeTentativeWrites;
                }
                if (this.IndexingDirective != null)
                {
                    yield return this.IndexingDirective;
                }
                if (this.IsBatchAtomic != null)
                {
                    yield return this.IsBatchAtomic;
                }
                if (this.IsBatchOrdered != null)
                {
                    yield return this.IsBatchOrdered;
                }
                if (this.IsClientEncrypted != null)
                {
                    yield return this.IsClientEncrypted;
                }
                if (this.IsFanoutRequest != null)
                {
                    yield return this.IsFanoutRequest;
                }
                if (this.IsOfferStorageRefreshRequest != null)
                {
                    yield return this.IsOfferStorageRefreshRequest;
                }
                if (this.IsReadOnlyScript != null)
                {
                    yield return this.IsReadOnlyScript;
                }
                if (this.IsRetriedWriteRequest != null)
                {
                    yield return this.IsRetriedWriteRequest;
                }
                if (this.IsRUPerGBEnforcementRequest != null)
                {
                    yield return this.IsRUPerGBEnforcementRequest;
                }
                if (this.IsUserRequest != null)
                {
                    yield return this.IsUserRequest;
                }
                if (this.MaxPollingIntervalMilliseconds != null)
                {
                    yield return this.MaxPollingIntervalMilliseconds;
                }
                if (this.MergeCheckPointGLSN != null)
                {
                    yield return this.MergeCheckPointGLSN;
                }
                if (this.MergeStaticId != null)
                {
                    yield return this.MergeStaticId;
                }
                if (this.MigrateCollectionDirective != null)
                {
                    yield return this.MigrateCollectionDirective;
                }
                if (this.MigrateOfferToAutopilot != null)
                {
                    yield return this.MigrateOfferToAutopilot;
                }
                if (this.MigrateOfferToManualThroughput != null)
                {
                    yield return this.MigrateOfferToManualThroughput;
                }
                if (this.PageSize != null)
                {
                    yield return this.PageSize;
                }
                if (this.PartitionCount != null)
                {
                    yield return this.PartitionCount;
                }
                if (this.PartitionKey != null)
                {
                    yield return this.PartitionKey;
                }
                if (this.PartitionKeyRangeId != null)
                {
                    yield return this.PartitionKeyRangeId;
                }
                if (this.PartitionResourceFilter != null)
                {
                    yield return this.PartitionResourceFilter;
                }
                if (this.PopulateCollectionThroughputInfo != null)
                {
                    yield return this.PopulateCollectionThroughputInfo;
                }
                if (this.PopulateLogStoreInfo != null)
                {
                    yield return this.PopulateLogStoreInfo;
                }
                if (this.PopulatePartitionStatistics != null)
                {
                    yield return this.PopulatePartitionStatistics;
                }
                if (this.PopulateQueryMetrics != null)
                {
                    yield return this.PopulateQueryMetrics;
                }
                if (this.PopulateQuotaInfo != null)
                {
                    yield return this.PopulateQuotaInfo;
                }
                if (this.PopulateResourceCount != null)
                {
                    yield return this.PopulateResourceCount;
                }
                if (this.PopulateUnflushedMergeEntryCount != null)
                {
                    yield return this.PopulateUnflushedMergeEntryCount;
                }
                if (this.PostTriggerExclude != null)
                {
                    yield return this.PostTriggerExclude;
                }
                if (this.PostTriggerInclude != null)
                {
                    yield return this.PostTriggerInclude;
                }
                if (this.Prefer != null)
                {
                    yield return this.Prefer;
                }
                if (this.PreserveFullContent != null)
                {
                    yield return this.PreserveFullContent;
                }
                if (this.PreTriggerExclude != null)
                {
                    yield return this.PreTriggerExclude;
                }
                if (this.PreTriggerInclude != null)
                {
                    yield return this.PreTriggerInclude;
                }
                if (this.PrimaryMasterKey != null)
                {
                    yield return this.PrimaryMasterKey;
                }
                if (this.PrimaryReadonlyKey != null)
                {
                    yield return this.PrimaryReadonlyKey;
                }
                if (this.ProfileRequest != null)
                {
                    yield return this.ProfileRequest;
                }
                if (this.ReadFeedKeyType != null)
                {
                    yield return this.ReadFeedKeyType;
                }
                if (this.RemainingTimeInMsOnClientRequest != null)
                {
                    yield return this.RemainingTimeInMsOnClientRequest;
                }
                if (this.RemoteStorageType != null)
                {
                    yield return this.RemoteStorageType;
                }
                if (this.ResourceSchemaName != null)
                {
                    yield return this.ResourceSchemaName;
                }
                if (this.ResourceTokenExpiry != null)
                {
                    yield return this.ResourceTokenExpiry;
                }
                if (this.ResourceTypes != null)
                {
                    yield return this.ResourceTypes;
                }
                if (this.ResponseContinuationTokenLimitInKB != null)
                {
                    yield return this.ResponseContinuationTokenLimitInKB;
                }
                if (this.RestoreMetadataFilter != null)
                {
                    yield return this.RestoreMetadataFilter;
                }
                if (this.RestoreParams != null)
                {
                    yield return this.RestoreParams;
                }
                if (this.RetriableWriteRequestId != null)
                {
                    yield return this.RetriableWriteRequestId;
                }
                if (this.RetriableWriteRequestStartTimestamp != null)
                {
                    yield return this.RetriableWriteRequestStartTimestamp;
                }
                if (this.SchemaHash != null)
                {
                    yield return this.SchemaHash;
                }
                if (this.SchemaOwnerRid != null)
                {
                    yield return this.SchemaOwnerRid;
                }
                if (this.SecondaryMasterKey != null)
                {
                    yield return this.SecondaryMasterKey;
                }
                if (this.SecondaryReadonlyKey != null)
                {
                    yield return this.SecondaryReadonlyKey;
                }
                if (this.SessionToken != null)
                {
                    yield return this.SessionToken;
                }
                if (this.ShareThroughput != null)
                {
                    yield return this.ShareThroughput;
                }
                if (this.ShouldBatchContinueOnError != null)
                {
                    yield return this.ShouldBatchContinueOnError;
                }
                if (this.StartEpk != null)
                {
                    yield return this.StartEpk;
                }
                if (this.StartId != null)
                {
                    yield return this.StartId;
                }
                if (this.SupportSpatialLegacyCoordinates != null)
                {
                    yield return this.SupportSpatialLegacyCoordinates;
                }
                if (this.SystemDocumentType != null)
                {
                    yield return this.SystemDocumentType;
                }
                if (this.TargetGlobalCommittedLsn != null)
                {
                    yield return this.TargetGlobalCommittedLsn;
                }
                if (this.TargetLsn != null)
                {
                    yield return this.TargetLsn;
                }
                if (this.TimeToLiveInSeconds != null)
                {
                    yield return this.TimeToLiveInSeconds;
                }
                if (this.TransactionCommit != null)
                {
                    yield return this.TransactionCommit;
                }
                if (this.TransactionId != null)
                {
                    yield return this.TransactionId;
                }
                if (this.TransportRequestID != null)
                {
                    yield return this.TransportRequestID;
                }
                if (this.TruncateMergeLogRequest != null)
                {
                    yield return this.TruncateMergeLogRequest;
                }
                if (this.UniqueIndexNameEncodingMode != null)
                {
                    yield return this.UniqueIndexNameEncodingMode;
                }
                if (this.UniqueIndexReIndexingState != null)
                {
                    yield return this.UniqueIndexReIndexingState;
                }
                if (this.UpdateMaxThroughputEverProvisioned != null)
                {
                    yield return this.UpdateMaxThroughputEverProvisioned;
                }
                if (this.UsePolygonsSmallerThanAHemisphere != null)
                {
                    yield return this.UsePolygonsSmallerThanAHemisphere;
                }
                if (this.UseSystemBudget != null)
                {
                    yield return this.UseSystemBudget;
                }
                if (this.Version != null)
                {
                    yield return this.Version;
                }
                if (this.XDate != null)
                {
                    yield return this.XDate;
                }
        }

        public NameValueCollection ToNameValueCollection()
        {
            throw new NotImplementedException();
        }

        public void Remove(string key)
        {
            this.Set(key, null);
        }

        public string Get(string key)
        {
            switch (key.Length)
            {
                case 4:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.HttpDate, key))
                    {
                        return this.HttpDate;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.A_IM, key))
                    {
                        return this.A_IM;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.HttpDate, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.HttpDate;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.A_IM, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.A_IM;
                    }
                
                    break;
                case 6:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.Prefer, key))
                    {
                        return this.Prefer;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.Prefer, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.Prefer;
                    }
                
                    break;
                case 8:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IfMatch, key))
                    {
                        return this.IfMatch;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.IfMatch, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.IfMatch;
                    }
                
                    break;
                case 9:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.XDate, key))
                    {
                        return this.XDate;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.XDate, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.XDate;
                    }
                
                    break;
                case 11:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.EndId, key))
                    {
                        return this.EndId;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.EndId, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.EndId;
                    }
                
                    break;
                case 12:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.Version, key))
                    {
                        return this.Version;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.EndEpk, key))
                    {
                        return this.EndEpk;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.Version, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.Version;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.EndEpk, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.EndEpk;
                    }
                
                    break;
                case 13:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.Authorization, key))
                    {
                        return this.Authorization;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IfNoneMatch, key))
                    {
                        return this.IfNoneMatch;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.StartId, key))
                    {
                        return this.StartId;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.Authorization, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.Authorization;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.IfNoneMatch, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.IfNoneMatch;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.StartId, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.StartId;
                    }
                
                    break;
                case 14:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.CanCharge, key))
                    {
                        return this.CanCharge;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.StartEpk, key))
                    {
                        return this.StartEpk;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.BinaryId, key))
                    {
                        return this.BinaryId;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.CanCharge, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.CanCharge;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.StartEpk, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.StartEpk;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.BinaryId, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.BinaryId;
                    }
                
                    break;
                case 15:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.TargetLsn, key))
                    {
                        return this.TargetLsn;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.TargetLsn, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.TargetLsn;
                    }
                
                    break;
                case 16:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.CanThrottle, key))
                    {
                        return this.CanThrottle;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.SchemaHash, key))
                    {
                        return this.SchemaHash;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.CanThrottle, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.CanThrottle;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.SchemaHash, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.SchemaHash;
                    }
                
                    break;
                case 17:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.Continuation, key))
                    {
                        return this.Continuation;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IfModifiedSince, key))
                    {
                        return this.IfModifiedSince;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.BindReplicaDirective, key))
                    {
                        return this.BindReplicaDirective;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.TransactionId, key))
                    {
                        return this.TransactionId;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.Continuation, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.Continuation;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.IfModifiedSince, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.IfModifiedSince;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.BindReplicaDirective, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.BindReplicaDirective;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.TransactionId, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.TransactionId;
                    }
                
                    break;
                case 18:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.ReadFeedKeyType, key))
                    {
                        return this.ReadFeedKeyType;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.SessionToken, key))
                    {
                        return this.SessionToken;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.ReadFeedKeyType, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.ReadFeedKeyType;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.SessionToken, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.SessionToken;
                    }
                
                    break;
                case 19:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.PageSize, key))
                    {
                        return this.PageSize;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.RestoreParams, key))
                    {
                        return this.RestoreParams;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.PageSize, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.PageSize;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.RestoreParams, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.RestoreParams;
                    }
                
                    break;
                case 20:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.ProfileRequest, key))
                    {
                        return this.ProfileRequest;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.ProfileRequest, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.ProfileRequest;
                    }
                
                    break;
                case 21:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.SchemaOwnerRid, key))
                    {
                        return this.SchemaOwnerRid;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ShareThroughput, key))
                    {
                        return this.ShareThroughput;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.TransactionCommit, key))
                    {
                        return this.TransactionCommit;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.SchemaOwnerRid, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.SchemaOwnerRid;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.ShareThroughput, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.ShareThroughput;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.TransactionCommit, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.TransactionCommit;
                    }
                
                    break;
                case 22:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.ConsistencyLevel, key))
                    {
                        return this.ConsistencyLevel;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.GatewaySignature, key))
                    {
                        return this.GatewaySignature;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.IsFanoutRequest, key))
                    {
                        return this.IsFanoutRequest;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.ConsistencyLevel, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.ConsistencyLevel;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.GatewaySignature, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.GatewaySignature;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.IsFanoutRequest, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.IsFanoutRequest;
                    }
                
                    break;
                case 23:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IndexingDirective, key))
                    {
                        return this.IndexingDirective;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IsReadOnlyScript, key))
                    {
                        return this.IsReadOnlyScript;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.PrimaryMasterKey, key))
                    {
                        return this.PrimaryMasterKey;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.IndexingDirective, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.IndexingDirective;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.IsReadOnlyScript, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.IsReadOnlyScript;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.PrimaryMasterKey, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.PrimaryMasterKey;
                    }
                
                    break;
                case 24:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IsBatchAtomic, key))
                    {
                        return this.IsBatchAtomic;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.CollectionServiceIndex, key))
                    {
                        return this.CollectionServiceIndex;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.RemoteStorageType, key))
                    {
                        return this.RemoteStorageType;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.IsBatchAtomic, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.IsBatchAtomic;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.CollectionServiceIndex, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.CollectionServiceIndex;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.RemoteStorageType, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.RemoteStorageType;
                    }
                
                    break;
                case 25:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IsBatchOrdered, key))
                    {
                        return this.IsBatchOrdered;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.TransportRequestID, key))
                    {
                        return this.TransportRequestID;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.PrimaryReadonlyKey, key))
                    {
                        return this.PrimaryReadonlyKey;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ResourceSchemaName, key))
                    {
                        return this.ResourceSchemaName;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ResourceTypes, key))
                    {
                        return this.ResourceTypes;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.SecondaryMasterKey, key))
                    {
                        return this.SecondaryMasterKey;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.IsBatchOrdered, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.IsBatchOrdered;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.TransportRequestID, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.TransportRequestID;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.PrimaryReadonlyKey, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.PrimaryReadonlyKey;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.ResourceSchemaName, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.ResourceSchemaName;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.ResourceTypes, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.ResourceTypes;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.SecondaryMasterKey, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.SecondaryMasterKey;
                    }
                
                    break;
                case 26:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.EnumerationDirection, key))
                    {
                        return this.EnumerationDirection;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.CollectionPartitionIndex, key))
                    {
                        return this.CollectionPartitionIndex;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.EnumerationDirection, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.EnumerationDirection;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.CollectionPartitionIndex, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.CollectionPartitionIndex;
                    }
                
                    break;
                case 27:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.FanoutOperationState, key))
                    {
                        return this.FanoutOperationState;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.MergeStaticId, key))
                    {
                        return this.MergeStaticId;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.SecondaryReadonlyKey, key))
                    {
                        return this.SecondaryReadonlyKey;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.FanoutOperationState, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.FanoutOperationState;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.MergeStaticId, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.MergeStaticId;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.SecondaryReadonlyKey, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.SecondaryReadonlyKey;
                    }
                
                    break;
                case 28:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.PartitionKey, key))
                    {
                        return this.PartitionKey;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.RestoreMetadataFilter, key))
                    {
                        return this.RestoreMetadataFilter;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.EffectivePartitionKey, key))
                    {
                        return this.EffectivePartitionKey;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.TimeToLiveInSeconds, key))
                    {
                        return this.TimeToLiveInSeconds;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.UseSystemBudget, key))
                    {
                        return this.UseSystemBudget;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.PartitionKey, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.PartitionKey;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.RestoreMetadataFilter, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.RestoreMetadataFilter;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.EffectivePartitionKey, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.EffectivePartitionKey;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.TimeToLiveInSeconds, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.TimeToLiveInSeconds;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.UseSystemBudget, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.UseSystemBudget;
                    }
                
                    break;
                case 30:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.ResourceTokenExpiry, key))
                    {
                        return this.ResourceTokenExpiry;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.CollectionRid, key))
                    {
                        return this.CollectionRid;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ExcludeSystemProperties, key))
                    {
                        return this.ExcludeSystemProperties;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.PartitionCount, key))
                    {
                        return this.PartitionCount;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.PartitionResourceFilter, key))
                    {
                        return this.PartitionResourceFilter;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.ResourceTokenExpiry, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.ResourceTokenExpiry;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.CollectionRid, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.CollectionRid;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.ExcludeSystemProperties, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.ExcludeSystemProperties;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.PartitionCount, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.PartitionCount;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.PartitionResourceFilter, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.PartitionResourceFilter;
                    }
                
                    break;
                case 31:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.CanOfferReplaceComplete, key))
                    {
                        return this.CanOfferReplaceComplete;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.ClientRetryAttemptCount, key))
                    {
                        return this.ClientRetryAttemptCount;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IsClientEncrypted, key))
                    {
                        return this.IsClientEncrypted;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.SystemDocumentType, key))
                    {
                        return this.SystemDocumentType;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.BinaryPassthroughRequest, key))
                    {
                        return this.BinaryPassthroughRequest;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.CanOfferReplaceComplete, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.CanOfferReplaceComplete;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.ClientRetryAttemptCount, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.ClientRetryAttemptCount;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.IsClientEncrypted, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.IsClientEncrypted;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.SystemDocumentType, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.SystemDocumentType;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.BinaryPassthroughRequest, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.BinaryPassthroughRequest;
                    }
                
                    break;
                case 32:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.MaxPollingIntervalMilliseconds, key))
                    {
                        return this.MaxPollingIntervalMilliseconds;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.MigrateCollectionDirective, key))
                    {
                        return this.MigrateCollectionDirective;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.TargetGlobalCommittedLsn, key))
                    {
                        return this.TargetGlobalCommittedLsn;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ForceQueryScan, key))
                    {
                        return this.ForceQueryScan;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.MaxPollingIntervalMilliseconds, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.MaxPollingIntervalMilliseconds;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.MigrateCollectionDirective, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.MigrateCollectionDirective;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.TargetGlobalCommittedLsn, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.TargetGlobalCommittedLsn;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.ForceQueryScan, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.ForceQueryScan;
                    }
                
                    break;
                case 33:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.EmitVerboseTracesInQuery, key))
                    {
                        return this.EmitVerboseTracesInQuery;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.EnableScanInQuery, key))
                    {
                        return this.EnableScanInQuery;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.PopulateQuotaInfo, key))
                    {
                        return this.PopulateQuotaInfo;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.PopulateLogStoreInfo, key))
                    {
                        return this.PopulateLogStoreInfo;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.PreserveFullContent, key))
                    {
                        return this.PreserveFullContent;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.EmitVerboseTracesInQuery, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.EmitVerboseTracesInQuery;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.EnableScanInQuery, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.EnableScanInQuery;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.PopulateQuotaInfo, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.PopulateQuotaInfo;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.PopulateLogStoreInfo, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.PopulateLogStoreInfo;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.PreserveFullContent, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.PreserveFullContent;
                    }
                
                    break;
                case 34:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.AllowTentativeWrites, key))
                    {
                        return this.AllowTentativeWrites;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.AllowTentativeWrites, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.AllowTentativeWrites;
                    }
                
                    break;
                case 35:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.FilterBySchemaResourceId, key))
                    {
                        return this.FilterBySchemaResourceId;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.PreTriggerExclude, key))
                    {
                        return this.PreTriggerExclude;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.PreTriggerInclude, key))
                    {
                        return this.PreTriggerInclude;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.RemainingTimeInMsOnClientRequest, key))
                    {
                        return this.RemainingTimeInMsOnClientRequest;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.ShouldBatchContinueOnError, key))
                    {
                        return this.ShouldBatchContinueOnError;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.CollectionSecurityIdentifier, key))
                    {
                        return this.CollectionSecurityIdentifier;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.PartitionKeyRangeId, key))
                    {
                        return this.PartitionKeyRangeId;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.FilterBySchemaResourceId, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.FilterBySchemaResourceId;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.PreTriggerExclude, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.PreTriggerExclude;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.PreTriggerInclude, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.PreTriggerInclude;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.RemainingTimeInMsOnClientRequest, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.RemainingTimeInMsOnClientRequest;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.ShouldBatchContinueOnError, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.ShouldBatchContinueOnError;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.CollectionSecurityIdentifier, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.CollectionSecurityIdentifier;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.PartitionKeyRangeId, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.PartitionKeyRangeId;
                    }
                
                    break;
                case 36:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IncludeTentativeWrites, key))
                    {
                        return this.IncludeTentativeWrites;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.PopulateQueryMetrics, key))
                    {
                        return this.PopulateQueryMetrics;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.PostTriggerExclude, key))
                    {
                        return this.PostTriggerExclude;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.PostTriggerInclude, key))
                    {
                        return this.PostTriggerInclude;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.IsRetriedWriteRequest, key))
                    {
                        return this.IsRetriedWriteRequest;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.IsUserRequest, key))
                    {
                        return this.IsUserRequest;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.IncludeTentativeWrites, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.IncludeTentativeWrites;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.PopulateQueryMetrics, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.PopulateQueryMetrics;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.PostTriggerExclude, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.PostTriggerExclude;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.PostTriggerInclude, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.PostTriggerInclude;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.IsRetriedWriteRequest, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.IsRetriedWriteRequest;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.IsUserRequest, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.IsUserRequest;
                    }
                
                    break;
                case 37:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.EnableLogging, key))
                    {
                        return this.EnableLogging;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.PopulateResourceCount, key))
                    {
                        return this.PopulateResourceCount;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.EnableLogging, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.EnableLogging;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.PopulateResourceCount, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.PopulateResourceCount;
                    }
                
                    break;
                case 38:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.MigrateOfferToAutopilot, key))
                    {
                        return this.MigrateOfferToAutopilot;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.RetriableWriteRequestId, key))
                    {
                        return this.RetriableWriteRequestId;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.MigrateOfferToAutopilot, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.MigrateOfferToAutopilot;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.RetriableWriteRequestId, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.RetriableWriteRequestId;
                    }
                
                    break;
                case 39:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.TruncateMergeLogRequest, key))
                    {
                        return this.TruncateMergeLogRequest;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.TruncateMergeLogRequest, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.TruncateMergeLogRequest;
                    }
                
                    break;
                case 40:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.EnableDynamicRidRangeAllocation, key))
                    {
                        return this.EnableDynamicRidRangeAllocation;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.UniqueIndexReIndexingState, key))
                    {
                        return this.UniqueIndexReIndexingState;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.EnableDynamicRidRangeAllocation, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.EnableDynamicRidRangeAllocation;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.UniqueIndexReIndexingState, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.UniqueIndexReIndexingState;
                    }
                
                    break;
                case 42:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.MergeCheckPointGLSN, key))
                    {
                        return this.MergeCheckPointGLSN;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.MergeCheckPointGLSN, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.MergeCheckPointGLSN;
                    }
                
                    break;
                case 43:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.DisableRUPerMinuteUsage, key))
                    {
                        return this.DisableRUPerMinuteUsage;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.PopulatePartitionStatistics, key))
                    {
                        return this.PopulatePartitionStatistics;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ForceSideBySideIndexMigration, key))
                    {
                        return this.ForceSideBySideIndexMigration;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.UniqueIndexNameEncodingMode, key))
                    {
                        return this.UniqueIndexNameEncodingMode;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.DisableRUPerMinuteUsage, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.DisableRUPerMinuteUsage;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.PopulatePartitionStatistics, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.PopulatePartitionStatistics;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.ForceSideBySideIndexMigration, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.ForceSideBySideIndexMigration;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.UniqueIndexNameEncodingMode, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.UniqueIndexNameEncodingMode;
                    }
                
                    break;
                case 44:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ContentSerializationFormat, key))
                    {
                        return this.ContentSerializationFormat;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.ContentSerializationFormat, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.ContentSerializationFormat;
                    }
                
                    break;
                case 45:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.ChangeFeedStartFullFidelityIfNoneMatch, key))
                    {
                        return this.ChangeFeedStartFullFidelityIfNoneMatch;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.ChangeFeedStartFullFidelityIfNoneMatch, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.ChangeFeedStartFullFidelityIfNoneMatch;
                    }
                
                    break;
                case 46:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.MigrateOfferToManualThroughput, key))
                    {
                        return this.MigrateOfferToManualThroughput;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.MigrateOfferToManualThroughput, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.MigrateOfferToManualThroughput;
                    }
                
                    break;
                case 47:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.SupportSpatialLegacyCoordinates, key))
                    {
                        return this.SupportSpatialLegacyCoordinates;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.AddResourcePropertiesToResponse, key))
                    {
                        return this.AddResourcePropertiesToResponse;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.CollectionChildResourceNameLimitInBytes, key))
                    {
                        return this.CollectionChildResourceNameLimitInBytes;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.SupportSpatialLegacyCoordinates, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.SupportSpatialLegacyCoordinates;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.AddResourcePropertiesToResponse, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.AddResourcePropertiesToResponse;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.CollectionChildResourceNameLimitInBytes, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.CollectionChildResourceNameLimitInBytes;
                    }
                
                    break;
                case 48:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.GetAllPartitionKeyStatistics, key))
                    {
                        return this.GetAllPartitionKeyStatistics;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.PopulateCollectionThroughputInfo, key))
                    {
                        return this.PopulateCollectionThroughputInfo;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.GetAllPartitionKeyStatistics, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.GetAllPartitionKeyStatistics;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.PopulateCollectionThroughputInfo, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.PopulateCollectionThroughputInfo;
                    }
                
                    break;
                case 49:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.UsePolygonsSmallerThanAHemisphere, key))
                    {
                        return this.UsePolygonsSmallerThanAHemisphere;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.UsePolygonsSmallerThanAHemisphere, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.UsePolygonsSmallerThanAHemisphere;
                    }
                
                    break;
                case 50:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.ResponseContinuationTokenLimitInKB, key))
                    {
                        return this.ResponseContinuationTokenLimitInKB;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.ResponseContinuationTokenLimitInKB, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.ResponseContinuationTokenLimitInKB;
                    }
                
                    break;
                case 51:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.EnableLowPrecisionOrderBy, key))
                    {
                        return this.EnableLowPrecisionOrderBy;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.RetriableWriteRequestStartTimestamp, key))
                    {
                        return this.RetriableWriteRequestStartTimestamp;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.EnableLowPrecisionOrderBy, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.EnableLowPrecisionOrderBy;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.RetriableWriteRequestStartTimestamp, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.RetriableWriteRequestStartTimestamp;
                    }
                
                    break;
                case 53:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IsOfferStorageRefreshRequest, key))
                    {
                        return this.IsOfferStorageRefreshRequest;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IsRUPerGBEnforcementRequest, key))
                    {
                        return this.IsRUPerGBEnforcementRequest;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.IsOfferStorageRefreshRequest, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.IsOfferStorageRefreshRequest;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.IsRUPerGBEnforcementRequest, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.IsRUPerGBEnforcementRequest;
                    }
                
                    break;
                case 56:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.CollectionChildResourceContentLimitInKB, key))
                    {
                        return this.CollectionChildResourceContentLimitInKB;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.CollectionChildResourceContentLimitInKB, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.CollectionChildResourceContentLimitInKB;
                    }
                
                    break;
                case 57:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.PopulateUnflushedMergeEntryCount, key))
                    {
                        return this.PopulateUnflushedMergeEntryCount;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.PopulateUnflushedMergeEntryCount, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.PopulateUnflushedMergeEntryCount;
                    }
                
                    break;
                case 58:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IgnoreSystemLoweringMaxThroughput, key))
                    {
                        return this.IgnoreSystemLoweringMaxThroughput;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.IgnoreSystemLoweringMaxThroughput, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.IgnoreSystemLoweringMaxThroughput;
                    }
                
                    break;
                case 59:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.UpdateMaxThroughputEverProvisioned, key))
                    {
                        return this.UpdateMaxThroughputEverProvisioned;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.UpdateMaxThroughputEverProvisioned, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.UpdateMaxThroughputEverProvisioned;
                    }
                
                    break;
                default:
                    break;
            }

            if(this.lazyNotCommonHeaders.IsValueCreated)
            {
                return this.lazyNotCommonHeaders.Value[key];
            }
            
            return null;
        }

        public void Set(string key, string value)
        {
            switch (key.Length)
            {
                case 4:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.HttpDate, key))
                    {
                        this.HttpDate = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.A_IM, key))
                    {
                        this.A_IM = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.HttpDate, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.HttpDate = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.A_IM, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.A_IM = value;
                        return;
                    }
                
                    break;
                case 6:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.Prefer, key))
                    {
                        this.Prefer = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.Prefer, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.Prefer = value;
                        return;
                    }
                
                    break;
                case 8:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IfMatch, key))
                    {
                        this.IfMatch = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.IfMatch, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.IfMatch = value;
                        return;
                    }
                
                    break;
                case 9:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.XDate, key))
                    {
                        this.XDate = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.XDate, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.XDate = value;
                        return;
                    }
                
                    break;
                case 11:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.EndId, key))
                    {
                        this.EndId = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.EndId, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.EndId = value;
                        return;
                    }
                
                    break;
                case 12:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.Version, key))
                    {
                        this.Version = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.EndEpk, key))
                    {
                        this.EndEpk = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.Version, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.Version = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.EndEpk, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.EndEpk = value;
                        return;
                    }
                
                    break;
                case 13:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.Authorization, key))
                    {
                        this.Authorization = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IfNoneMatch, key))
                    {
                        this.IfNoneMatch = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.StartId, key))
                    {
                        this.StartId = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.Authorization, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.Authorization = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.IfNoneMatch, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.IfNoneMatch = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.StartId, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.StartId = value;
                        return;
                    }
                
                    break;
                case 14:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.CanCharge, key))
                    {
                        this.CanCharge = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.StartEpk, key))
                    {
                        this.StartEpk = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.BinaryId, key))
                    {
                        this.BinaryId = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.CanCharge, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.CanCharge = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.StartEpk, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.StartEpk = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.BinaryId, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.BinaryId = value;
                        return;
                    }
                
                    break;
                case 15:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.TargetLsn, key))
                    {
                        this.TargetLsn = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.TargetLsn, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.TargetLsn = value;
                        return;
                    }
                
                    break;
                case 16:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.CanThrottle, key))
                    {
                        this.CanThrottle = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.SchemaHash, key))
                    {
                        this.SchemaHash = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.CanThrottle, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.CanThrottle = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.SchemaHash, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.SchemaHash = value;
                        return;
                    }
                
                    break;
                case 17:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.Continuation, key))
                    {
                        this.Continuation = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IfModifiedSince, key))
                    {
                        this.IfModifiedSince = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.BindReplicaDirective, key))
                    {
                        this.BindReplicaDirective = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.TransactionId, key))
                    {
                        this.TransactionId = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.Continuation, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.Continuation = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.IfModifiedSince, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.IfModifiedSince = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.BindReplicaDirective, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.BindReplicaDirective = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.TransactionId, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.TransactionId = value;
                        return;
                    }
                
                    break;
                case 18:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.ReadFeedKeyType, key))
                    {
                        this.ReadFeedKeyType = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.SessionToken, key))
                    {
                        this.SessionToken = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.ReadFeedKeyType, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.ReadFeedKeyType = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.SessionToken, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.SessionToken = value;
                        return;
                    }
                
                    break;
                case 19:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.PageSize, key))
                    {
                        this.PageSize = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.RestoreParams, key))
                    {
                        this.RestoreParams = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.PageSize, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.PageSize = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.RestoreParams, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.RestoreParams = value;
                        return;
                    }
                
                    break;
                case 20:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.ProfileRequest, key))
                    {
                        this.ProfileRequest = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.ProfileRequest, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.ProfileRequest = value;
                        return;
                    }
                
                    break;
                case 21:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.SchemaOwnerRid, key))
                    {
                        this.SchemaOwnerRid = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ShareThroughput, key))
                    {
                        this.ShareThroughput = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.TransactionCommit, key))
                    {
                        this.TransactionCommit = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.SchemaOwnerRid, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.SchemaOwnerRid = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.ShareThroughput, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.ShareThroughput = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.TransactionCommit, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.TransactionCommit = value;
                        return;
                    }
                
                    break;
                case 22:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.ConsistencyLevel, key))
                    {
                        this.ConsistencyLevel = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.GatewaySignature, key))
                    {
                        this.GatewaySignature = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.IsFanoutRequest, key))
                    {
                        this.IsFanoutRequest = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.ConsistencyLevel, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.ConsistencyLevel = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.GatewaySignature, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.GatewaySignature = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.IsFanoutRequest, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.IsFanoutRequest = value;
                        return;
                    }
                
                    break;
                case 23:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IndexingDirective, key))
                    {
                        this.IndexingDirective = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IsReadOnlyScript, key))
                    {
                        this.IsReadOnlyScript = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.PrimaryMasterKey, key))
                    {
                        this.PrimaryMasterKey = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.IndexingDirective, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.IndexingDirective = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.IsReadOnlyScript, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.IsReadOnlyScript = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.PrimaryMasterKey, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.PrimaryMasterKey = value;
                        return;
                    }
                
                    break;
                case 24:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IsBatchAtomic, key))
                    {
                        this.IsBatchAtomic = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.CollectionServiceIndex, key))
                    {
                        this.CollectionServiceIndex = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.RemoteStorageType, key))
                    {
                        this.RemoteStorageType = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.IsBatchAtomic, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.IsBatchAtomic = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.CollectionServiceIndex, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.CollectionServiceIndex = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.RemoteStorageType, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.RemoteStorageType = value;
                        return;
                    }
                
                    break;
                case 25:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IsBatchOrdered, key))
                    {
                        this.IsBatchOrdered = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.TransportRequestID, key))
                    {
                        this.TransportRequestID = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.PrimaryReadonlyKey, key))
                    {
                        this.PrimaryReadonlyKey = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ResourceSchemaName, key))
                    {
                        this.ResourceSchemaName = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ResourceTypes, key))
                    {
                        this.ResourceTypes = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.SecondaryMasterKey, key))
                    {
                        this.SecondaryMasterKey = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.IsBatchOrdered, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.IsBatchOrdered = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.TransportRequestID, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.TransportRequestID = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.PrimaryReadonlyKey, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.PrimaryReadonlyKey = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.ResourceSchemaName, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.ResourceSchemaName = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.ResourceTypes, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.ResourceTypes = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.SecondaryMasterKey, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.SecondaryMasterKey = value;
                        return;
                    }
                
                    break;
                case 26:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.EnumerationDirection, key))
                    {
                        this.EnumerationDirection = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.CollectionPartitionIndex, key))
                    {
                        this.CollectionPartitionIndex = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.EnumerationDirection, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.EnumerationDirection = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.CollectionPartitionIndex, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.CollectionPartitionIndex = value;
                        return;
                    }
                
                    break;
                case 27:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.FanoutOperationState, key))
                    {
                        this.FanoutOperationState = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.MergeStaticId, key))
                    {
                        this.MergeStaticId = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.SecondaryReadonlyKey, key))
                    {
                        this.SecondaryReadonlyKey = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.FanoutOperationState, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.FanoutOperationState = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.MergeStaticId, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.MergeStaticId = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.SecondaryReadonlyKey, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.SecondaryReadonlyKey = value;
                        return;
                    }
                
                    break;
                case 28:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.PartitionKey, key))
                    {
                        this.PartitionKey = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.RestoreMetadataFilter, key))
                    {
                        this.RestoreMetadataFilter = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.EffectivePartitionKey, key))
                    {
                        this.EffectivePartitionKey = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.TimeToLiveInSeconds, key))
                    {
                        this.TimeToLiveInSeconds = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.UseSystemBudget, key))
                    {
                        this.UseSystemBudget = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.PartitionKey, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.PartitionKey = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.RestoreMetadataFilter, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.RestoreMetadataFilter = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.EffectivePartitionKey, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.EffectivePartitionKey = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.TimeToLiveInSeconds, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.TimeToLiveInSeconds = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.UseSystemBudget, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.UseSystemBudget = value;
                        return;
                    }
                
                    break;
                case 30:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.ResourceTokenExpiry, key))
                    {
                        this.ResourceTokenExpiry = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.CollectionRid, key))
                    {
                        this.CollectionRid = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ExcludeSystemProperties, key))
                    {
                        this.ExcludeSystemProperties = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.PartitionCount, key))
                    {
                        this.PartitionCount = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.PartitionResourceFilter, key))
                    {
                        this.PartitionResourceFilter = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.ResourceTokenExpiry, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.ResourceTokenExpiry = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.CollectionRid, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.CollectionRid = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.ExcludeSystemProperties, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.ExcludeSystemProperties = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.PartitionCount, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.PartitionCount = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.PartitionResourceFilter, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.PartitionResourceFilter = value;
                        return;
                    }
                
                    break;
                case 31:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.CanOfferReplaceComplete, key))
                    {
                        this.CanOfferReplaceComplete = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.ClientRetryAttemptCount, key))
                    {
                        this.ClientRetryAttemptCount = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IsClientEncrypted, key))
                    {
                        this.IsClientEncrypted = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.SystemDocumentType, key))
                    {
                        this.SystemDocumentType = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.BinaryPassthroughRequest, key))
                    {
                        this.BinaryPassthroughRequest = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.CanOfferReplaceComplete, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.CanOfferReplaceComplete = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.ClientRetryAttemptCount, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.ClientRetryAttemptCount = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.IsClientEncrypted, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.IsClientEncrypted = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.SystemDocumentType, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.SystemDocumentType = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.BinaryPassthroughRequest, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.BinaryPassthroughRequest = value;
                        return;
                    }
                
                    break;
                case 32:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.MaxPollingIntervalMilliseconds, key))
                    {
                        this.MaxPollingIntervalMilliseconds = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.MigrateCollectionDirective, key))
                    {
                        this.MigrateCollectionDirective = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.TargetGlobalCommittedLsn, key))
                    {
                        this.TargetGlobalCommittedLsn = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ForceQueryScan, key))
                    {
                        this.ForceQueryScan = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.MaxPollingIntervalMilliseconds, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.MaxPollingIntervalMilliseconds = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.MigrateCollectionDirective, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.MigrateCollectionDirective = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.TargetGlobalCommittedLsn, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.TargetGlobalCommittedLsn = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.ForceQueryScan, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.ForceQueryScan = value;
                        return;
                    }
                
                    break;
                case 33:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.EmitVerboseTracesInQuery, key))
                    {
                        this.EmitVerboseTracesInQuery = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.EnableScanInQuery, key))
                    {
                        this.EnableScanInQuery = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.PopulateQuotaInfo, key))
                    {
                        this.PopulateQuotaInfo = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.PopulateLogStoreInfo, key))
                    {
                        this.PopulateLogStoreInfo = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.PreserveFullContent, key))
                    {
                        this.PreserveFullContent = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.EmitVerboseTracesInQuery, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.EmitVerboseTracesInQuery = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.EnableScanInQuery, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.EnableScanInQuery = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.PopulateQuotaInfo, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.PopulateQuotaInfo = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.PopulateLogStoreInfo, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.PopulateLogStoreInfo = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.PreserveFullContent, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.PreserveFullContent = value;
                        return;
                    }
                
                    break;
                case 34:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.AllowTentativeWrites, key))
                    {
                        this.AllowTentativeWrites = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.AllowTentativeWrites, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.AllowTentativeWrites = value;
                        return;
                    }
                
                    break;
                case 35:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.FilterBySchemaResourceId, key))
                    {
                        this.FilterBySchemaResourceId = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.PreTriggerExclude, key))
                    {
                        this.PreTriggerExclude = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.PreTriggerInclude, key))
                    {
                        this.PreTriggerInclude = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.RemainingTimeInMsOnClientRequest, key))
                    {
                        this.RemainingTimeInMsOnClientRequest = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.ShouldBatchContinueOnError, key))
                    {
                        this.ShouldBatchContinueOnError = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.CollectionSecurityIdentifier, key))
                    {
                        this.CollectionSecurityIdentifier = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.PartitionKeyRangeId, key))
                    {
                        this.PartitionKeyRangeId = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.FilterBySchemaResourceId, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.FilterBySchemaResourceId = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.PreTriggerExclude, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.PreTriggerExclude = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.PreTriggerInclude, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.PreTriggerInclude = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.RemainingTimeInMsOnClientRequest, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.RemainingTimeInMsOnClientRequest = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.ShouldBatchContinueOnError, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.ShouldBatchContinueOnError = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.CollectionSecurityIdentifier, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.CollectionSecurityIdentifier = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.PartitionKeyRangeId, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.PartitionKeyRangeId = value;
                        return;
                    }
                
                    break;
                case 36:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IncludeTentativeWrites, key))
                    {
                        this.IncludeTentativeWrites = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.PopulateQueryMetrics, key))
                    {
                        this.PopulateQueryMetrics = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.PostTriggerExclude, key))
                    {
                        this.PostTriggerExclude = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.PostTriggerInclude, key))
                    {
                        this.PostTriggerInclude = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.IsRetriedWriteRequest, key))
                    {
                        this.IsRetriedWriteRequest = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.IsUserRequest, key))
                    {
                        this.IsUserRequest = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.IncludeTentativeWrites, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.IncludeTentativeWrites = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.PopulateQueryMetrics, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.PopulateQueryMetrics = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.PostTriggerExclude, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.PostTriggerExclude = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.PostTriggerInclude, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.PostTriggerInclude = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.IsRetriedWriteRequest, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.IsRetriedWriteRequest = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.IsUserRequest, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.IsUserRequest = value;
                        return;
                    }
                
                    break;
                case 37:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.EnableLogging, key))
                    {
                        this.EnableLogging = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.PopulateResourceCount, key))
                    {
                        this.PopulateResourceCount = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.EnableLogging, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.EnableLogging = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.PopulateResourceCount, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.PopulateResourceCount = value;
                        return;
                    }
                
                    break;
                case 38:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.MigrateOfferToAutopilot, key))
                    {
                        this.MigrateOfferToAutopilot = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.RetriableWriteRequestId, key))
                    {
                        this.RetriableWriteRequestId = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.MigrateOfferToAutopilot, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.MigrateOfferToAutopilot = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.RetriableWriteRequestId, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.RetriableWriteRequestId = value;
                        return;
                    }
                
                    break;
                case 39:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.TruncateMergeLogRequest, key))
                    {
                        this.TruncateMergeLogRequest = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.TruncateMergeLogRequest, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.TruncateMergeLogRequest = value;
                        return;
                    }
                
                    break;
                case 40:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.EnableDynamicRidRangeAllocation, key))
                    {
                        this.EnableDynamicRidRangeAllocation = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.UniqueIndexReIndexingState, key))
                    {
                        this.UniqueIndexReIndexingState = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.EnableDynamicRidRangeAllocation, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.EnableDynamicRidRangeAllocation = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.UniqueIndexReIndexingState, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.UniqueIndexReIndexingState = value;
                        return;
                    }
                
                    break;
                case 42:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.MergeCheckPointGLSN, key))
                    {
                        this.MergeCheckPointGLSN = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.MergeCheckPointGLSN, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.MergeCheckPointGLSN = value;
                        return;
                    }
                
                    break;
                case 43:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.DisableRUPerMinuteUsage, key))
                    {
                        this.DisableRUPerMinuteUsage = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.PopulatePartitionStatistics, key))
                    {
                        this.PopulatePartitionStatistics = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ForceSideBySideIndexMigration, key))
                    {
                        this.ForceSideBySideIndexMigration = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.UniqueIndexNameEncodingMode, key))
                    {
                        this.UniqueIndexNameEncodingMode = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.DisableRUPerMinuteUsage, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.DisableRUPerMinuteUsage = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.PopulatePartitionStatistics, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.PopulatePartitionStatistics = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.ForceSideBySideIndexMigration, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.ForceSideBySideIndexMigration = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.UniqueIndexNameEncodingMode, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.UniqueIndexNameEncodingMode = value;
                        return;
                    }
                
                    break;
                case 44:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ContentSerializationFormat, key))
                    {
                        this.ContentSerializationFormat = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.ContentSerializationFormat, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.ContentSerializationFormat = value;
                        return;
                    }
                
                    break;
                case 45:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.ChangeFeedStartFullFidelityIfNoneMatch, key))
                    {
                        this.ChangeFeedStartFullFidelityIfNoneMatch = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.ChangeFeedStartFullFidelityIfNoneMatch, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.ChangeFeedStartFullFidelityIfNoneMatch = value;
                        return;
                    }
                
                    break;
                case 46:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.MigrateOfferToManualThroughput, key))
                    {
                        this.MigrateOfferToManualThroughput = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.MigrateOfferToManualThroughput, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.MigrateOfferToManualThroughput = value;
                        return;
                    }
                
                    break;
                case 47:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.SupportSpatialLegacyCoordinates, key))
                    {
                        this.SupportSpatialLegacyCoordinates = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.AddResourcePropertiesToResponse, key))
                    {
                        this.AddResourcePropertiesToResponse = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.CollectionChildResourceNameLimitInBytes, key))
                    {
                        this.CollectionChildResourceNameLimitInBytes = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.SupportSpatialLegacyCoordinates, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.SupportSpatialLegacyCoordinates = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.AddResourcePropertiesToResponse, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.AddResourcePropertiesToResponse = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.CollectionChildResourceNameLimitInBytes, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.CollectionChildResourceNameLimitInBytes = value;
                        return;
                    }
                
                    break;
                case 48:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.GetAllPartitionKeyStatistics, key))
                    {
                        this.GetAllPartitionKeyStatistics = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.PopulateCollectionThroughputInfo, key))
                    {
                        this.PopulateCollectionThroughputInfo = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.GetAllPartitionKeyStatistics, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.GetAllPartitionKeyStatistics = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.PopulateCollectionThroughputInfo, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.PopulateCollectionThroughputInfo = value;
                        return;
                    }
                
                    break;
                case 49:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.UsePolygonsSmallerThanAHemisphere, key))
                    {
                        this.UsePolygonsSmallerThanAHemisphere = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.UsePolygonsSmallerThanAHemisphere, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.UsePolygonsSmallerThanAHemisphere = value;
                        return;
                    }
                
                    break;
                case 50:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.ResponseContinuationTokenLimitInKB, key))
                    {
                        this.ResponseContinuationTokenLimitInKB = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.ResponseContinuationTokenLimitInKB, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.ResponseContinuationTokenLimitInKB = value;
                        return;
                    }
                
                    break;
                case 51:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.EnableLowPrecisionOrderBy, key))
                    {
                        this.EnableLowPrecisionOrderBy = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.RetriableWriteRequestStartTimestamp, key))
                    {
                        this.RetriableWriteRequestStartTimestamp = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.EnableLowPrecisionOrderBy, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.EnableLowPrecisionOrderBy = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.RetriableWriteRequestStartTimestamp, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.RetriableWriteRequestStartTimestamp = value;
                        return;
                    }
                
                    break;
                case 53:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IsOfferStorageRefreshRequest, key))
                    {
                        this.IsOfferStorageRefreshRequest = value;
                        return;
                    }
                
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IsRUPerGBEnforcementRequest, key))
                    {
                        this.IsRUPerGBEnforcementRequest = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.IsOfferStorageRefreshRequest, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.IsOfferStorageRefreshRequest = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.IsRUPerGBEnforcementRequest, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.IsRUPerGBEnforcementRequest = value;
                        return;
                    }
                
                    break;
                case 56:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.CollectionChildResourceContentLimitInKB, key))
                    {
                        this.CollectionChildResourceContentLimitInKB = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.CollectionChildResourceContentLimitInKB, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.CollectionChildResourceContentLimitInKB = value;
                        return;
                    }
                
                    break;
                case 57:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.PopulateUnflushedMergeEntryCount, key))
                    {
                        this.PopulateUnflushedMergeEntryCount = value;
                        return;
                    }
                
                    if (string.Equals(WFConstants.BackendHeaders.PopulateUnflushedMergeEntryCount, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.PopulateUnflushedMergeEntryCount = value;
                        return;
                    }
                
                    break;
                case 58:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IgnoreSystemLoweringMaxThroughput, key))
                    {
                        this.IgnoreSystemLoweringMaxThroughput = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.IgnoreSystemLoweringMaxThroughput, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.IgnoreSystemLoweringMaxThroughput = value;
                        return;
                    }
                
                    break;
                case 59:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.UpdateMaxThroughputEverProvisioned, key))
                    {
                        this.UpdateMaxThroughputEverProvisioned = value;
                        return;
                    }
                
                    if (string.Equals(HttpConstants.HttpHeaders.UpdateMaxThroughputEverProvisioned, key, StringComparison.OrdinalIgnoreCase))
                    {
                        this.UpdateMaxThroughputEverProvisioned = value;
                        return;
                    }
                
                    break;
                default:
                    break;
            }

            this.lazyNotCommonHeaders.Value[key] = value;
        }
    }
}