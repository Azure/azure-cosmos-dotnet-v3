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
    using System.Globalization;
    using System.Linq;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    internal class StoreRequestHeaders : InternalHeaders, INameValueCollection
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
        public string ClientRetryAttemptCount { get; set; }
        public string CollectionChildResourceContentLimitInKB { get; set; }
        public string CollectionChildResourceNameLimitInBytes { get; set; }
        public string CollectionPartitionIndex { get; set; }
        public string CollectionRid { get; set; }
        public string CollectionSecurityIdentifier { get; set; }
        public string CollectionServiceIndex { get; set; }
        public string ConsistencyLevel { get; set; }
        public string ContentSerializationFormat { get; set; }
        public override string Continuation { get; set; }
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
        public override string IfNoneMatch { get; set; }
        public string IncludeTentativeWrites { get; set; }
        public string IndexingDirective { get; set; }
        public string IsBatchAtomic { get; set; }
        public string IsBatchOrdered { get; set; }
        public string IsClientEncrypted { get; set; }
        public string IsFanoutRequest { get; set; }
        public string IsOfferStorageRefreshRequest { get; set; }
        public string IsReadOnlyScript { get; set; }
        public string IsRUPerGBEnforcementRequest { get; set; }
        public string IsUserRequest { get; set; }
        public string MaxPollingIntervalMilliseconds { get; set; }
        public string MergeCheckPointGLSN { get; set; }
        public string MergeStaticId { get; set; }
        public string MigrateCollectionDirective { get; set; }
        public string MigrateOfferToAutopilot { get; set; }
        public string MigrateOfferToManualThroughput { get; set; }
        public override string PageSize { get; set; }
        public string PartitionCount { get; set; }
        public override string PartitionKey { get; set; }
        public override string PartitionKeyRangeId { get; set; }
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
        public string SchemaHash { get; set; }
        public string SchemaOwnerRid { get; set; }
        public string SecondaryMasterKey { get; set; }
        public string SecondaryReadonlyKey { get; set; }
        public override string SessionToken { get; set; }
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
        public string UniqueIndexNameEncodingMode { get; set; }
        public string UpdateMaxThroughputEverProvisioned { get; set; }
        public string UsePolygonsSmallerThanAHemisphere { get; set; }
        public string UseSystemBudget { get; set; }
        public string Version { get; set; }
        public string XDate { get; set; }

        public StoreRequestHeaders()
            : this(new Lazy<Dictionary<string, string>>(() => new Dictionary<string, string>()))
        {
        }

        private StoreRequestHeaders(Lazy<Dictionary<string, string>> notCommonHeaders)
        {
            this.lazyNotCommonHeaders = notCommonHeaders ?? throw new ArgumentNullException(nameof(notCommonHeaders));
        }

        public override bool TryGetValue(string headerName, out string value)
        {
            value = this.Get(headerName);
            return value != null;
        }

        public override void Add(INameValueCollection collection)
        {
            foreach (string key in collection.Keys())
            {
                this.Set(key, collection[key]);
            }
        }

        public override string[] AllKeys()
        {
            return this.Keys().ToArray();
        }

        public override void Clear()
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
            this.IncludeTentativeWrites = null;
            this.IndexingDirective = null;
            this.IsBatchAtomic = null;
            this.IsBatchOrdered = null;
            this.IsClientEncrypted = null;
            this.IsFanoutRequest = null;
            this.IsOfferStorageRefreshRequest = null;
            this.IsReadOnlyScript = null;
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
            this.UniqueIndexNameEncodingMode = null;
            this.UpdateMaxThroughputEverProvisioned = null;
            this.UsePolygonsSmallerThanAHemisphere = null;
            this.UseSystemBudget = null;
            this.Version = null;
            this.XDate = null;

        }

        public override INameValueCollection Clone()
        {
            Lazy<Dictionary<string, string>> cloneNotCommonHeaders = new Lazy<Dictionary<string, string>>(() => new Dictionary<string, string>());
            if (this.lazyNotCommonHeaders.IsValueCreated)
            {
                foreach (KeyValuePair<string, string> notCommonHeader in this.lazyNotCommonHeaders.Value)
                {
                    cloneNotCommonHeaders.Value[notCommonHeader.Key] = notCommonHeader.Value;
                }
            }

            StoreRequestHeaders cloneHeaders = new StoreRequestHeaders(cloneNotCommonHeaders)
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
                IncludeTentativeWrites = this.IncludeTentativeWrites,
                IndexingDirective = this.IndexingDirective,
                IsBatchAtomic = this.IsBatchAtomic,
                IsBatchOrdered = this.IsBatchOrdered,
                IsClientEncrypted = this.IsClientEncrypted,
                IsFanoutRequest = this.IsFanoutRequest,
                IsOfferStorageRefreshRequest = this.IsOfferStorageRefreshRequest,
                IsReadOnlyScript = this.IsReadOnlyScript,
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
                UniqueIndexNameEncodingMode = this.UniqueIndexNameEncodingMode,
                UpdateMaxThroughputEverProvisioned = this.UpdateMaxThroughputEverProvisioned,
                UsePolygonsSmallerThanAHemisphere = this.UsePolygonsSmallerThanAHemisphere,
                UseSystemBudget = this.UseSystemBudget,
                Version = this.Version,
                XDate = this.XDate,
            };

            return cloneHeaders;
        }

        public override int Count()
        {
            return this.Keys().Count();
        }

        public override IEnumerator<string> GetEnumerator()
        {
            return this.Keys().GetEnumerator();
        }

        public override string[] GetValues(string key)
        {
            string value = this.Get(key);
            if (value != null)
            {
                return new string[] { value };
            }
            
            return null;
        }

        public override IEnumerable<string> Keys()
        {
            if (this.Authorization != null)
            {
                yield return HttpConstants.HttpHeaders.Authorization;
            }
            if (this.HttpDate != null)
            {
                yield return HttpConstants.HttpHeaders.HttpDate;
            }
            if (this.XDate != null)
            {
                yield return HttpConstants.HttpHeaders.XDate;
            }
            if (this.Version != null)
            {
                yield return HttpConstants.HttpHeaders.Version;
            }
            if (this.A_IM != null)
            {
                yield return HttpConstants.HttpHeaders.A_IM;
            }
            if (this.CanCharge != null)
            {
                yield return HttpConstants.HttpHeaders.CanCharge;
            }
            if (this.CanOfferReplaceComplete != null)
            {
                yield return HttpConstants.HttpHeaders.CanOfferReplaceComplete;
            }
            if (this.CanThrottle != null)
            {
                yield return HttpConstants.HttpHeaders.CanThrottle;
            }
            if (this.ClientRetryAttemptCount != null)
            {
                yield return HttpConstants.HttpHeaders.ClientRetryAttemptCount;
            }
            if (this.ConsistencyLevel != null)
            {
                yield return HttpConstants.HttpHeaders.ConsistencyLevel;
            }
            if (this.Continuation != null)
            {
                yield return HttpConstants.HttpHeaders.Continuation;
            }
            if (this.DisableRUPerMinuteUsage != null)
            {
                yield return HttpConstants.HttpHeaders.DisableRUPerMinuteUsage;
            }
            if (this.EmitVerboseTracesInQuery != null)
            {
                yield return HttpConstants.HttpHeaders.EmitVerboseTracesInQuery;
            }
            if (this.EnableLogging != null)
            {
                yield return HttpConstants.HttpHeaders.EnableLogging;
            }
            if (this.EnableLowPrecisionOrderBy != null)
            {
                yield return HttpConstants.HttpHeaders.EnableLowPrecisionOrderBy;
            }
            if (this.EnableScanInQuery != null)
            {
                yield return HttpConstants.HttpHeaders.EnableScanInQuery;
            }
            if (this.EndEpk != null)
            {
                yield return HttpConstants.HttpHeaders.EndEpk;
            }
            if (this.EndId != null)
            {
                yield return HttpConstants.HttpHeaders.EndId;
            }
            if (this.EnumerationDirection != null)
            {
                yield return HttpConstants.HttpHeaders.EnumerationDirection;
            }
            if (this.FilterBySchemaResourceId != null)
            {
                yield return HttpConstants.HttpHeaders.FilterBySchemaResourceId;
            }
            if (this.GatewaySignature != null)
            {
                yield return HttpConstants.HttpHeaders.GatewaySignature;
            }
            if (this.GetAllPartitionKeyStatistics != null)
            {
                yield return HttpConstants.HttpHeaders.GetAllPartitionKeyStatistics;
            }
            if (this.IfMatch != null)
            {
                yield return HttpConstants.HttpHeaders.IfMatch;
            }
            if (this.IfModifiedSince != null)
            {
                yield return HttpConstants.HttpHeaders.IfModifiedSince;
            }
            if (this.IfNoneMatch != null)
            {
                yield return HttpConstants.HttpHeaders.IfNoneMatch;
            }
            if (this.IncludeTentativeWrites != null)
            {
                yield return HttpConstants.HttpHeaders.IncludeTentativeWrites;
            }
            if (this.IndexingDirective != null)
            {
                yield return HttpConstants.HttpHeaders.IndexingDirective;
            }
            if (this.IsBatchAtomic != null)
            {
                yield return HttpConstants.HttpHeaders.IsBatchAtomic;
            }
            if (this.IsBatchOrdered != null)
            {
                yield return HttpConstants.HttpHeaders.IsBatchOrdered;
            }
            if (this.IsClientEncrypted != null)
            {
                yield return HttpConstants.HttpHeaders.IsClientEncrypted;
            }
            if (this.IsOfferStorageRefreshRequest != null)
            {
                yield return HttpConstants.HttpHeaders.IsOfferStorageRefreshRequest;
            }
            if (this.IsReadOnlyScript != null)
            {
                yield return HttpConstants.HttpHeaders.IsReadOnlyScript;
            }
            if (this.IsRUPerGBEnforcementRequest != null)
            {
                yield return HttpConstants.HttpHeaders.IsRUPerGBEnforcementRequest;
            }
            if (this.MaxPollingIntervalMilliseconds != null)
            {
                yield return HttpConstants.HttpHeaders.MaxPollingIntervalMilliseconds;
            }
            if (this.MigrateCollectionDirective != null)
            {
                yield return HttpConstants.HttpHeaders.MigrateCollectionDirective;
            }
            if (this.MigrateOfferToAutopilot != null)
            {
                yield return HttpConstants.HttpHeaders.MigrateOfferToAutopilot;
            }
            if (this.MigrateOfferToManualThroughput != null)
            {
                yield return HttpConstants.HttpHeaders.MigrateOfferToManualThroughput;
            }
            if (this.PageSize != null)
            {
                yield return HttpConstants.HttpHeaders.PageSize;
            }
            if (this.PartitionKey != null)
            {
                yield return HttpConstants.HttpHeaders.PartitionKey;
            }
            if (this.PopulateCollectionThroughputInfo != null)
            {
                yield return HttpConstants.HttpHeaders.PopulateCollectionThroughputInfo;
            }
            if (this.PopulatePartitionStatistics != null)
            {
                yield return HttpConstants.HttpHeaders.PopulatePartitionStatistics;
            }
            if (this.PopulateQueryMetrics != null)
            {
                yield return HttpConstants.HttpHeaders.PopulateQueryMetrics;
            }
            if (this.PopulateQuotaInfo != null)
            {
                yield return HttpConstants.HttpHeaders.PopulateQuotaInfo;
            }
            if (this.PopulateResourceCount != null)
            {
                yield return HttpConstants.HttpHeaders.PopulateResourceCount;
            }
            if (this.PostTriggerExclude != null)
            {
                yield return HttpConstants.HttpHeaders.PostTriggerExclude;
            }
            if (this.PostTriggerInclude != null)
            {
                yield return HttpConstants.HttpHeaders.PostTriggerInclude;
            }
            if (this.Prefer != null)
            {
                yield return HttpConstants.HttpHeaders.Prefer;
            }
            if (this.PreTriggerExclude != null)
            {
                yield return HttpConstants.HttpHeaders.PreTriggerExclude;
            }
            if (this.PreTriggerInclude != null)
            {
                yield return HttpConstants.HttpHeaders.PreTriggerInclude;
            }
            if (this.ProfileRequest != null)
            {
                yield return HttpConstants.HttpHeaders.ProfileRequest;
            }
            if (this.ReadFeedKeyType != null)
            {
                yield return HttpConstants.HttpHeaders.ReadFeedKeyType;
            }
            if (this.RemainingTimeInMsOnClientRequest != null)
            {
                yield return HttpConstants.HttpHeaders.RemainingTimeInMsOnClientRequest;
            }
            if (this.ResourceTokenExpiry != null)
            {
                yield return HttpConstants.HttpHeaders.ResourceTokenExpiry;
            }
            if (this.ResponseContinuationTokenLimitInKB != null)
            {
                yield return HttpConstants.HttpHeaders.ResponseContinuationTokenLimitInKB;
            }
            if (this.RestoreMetadataFilter != null)
            {
                yield return HttpConstants.HttpHeaders.RestoreMetadataFilter;
            }
            if (this.SessionToken != null)
            {
                yield return HttpConstants.HttpHeaders.SessionToken;
            }
            if (this.ShouldBatchContinueOnError != null)
            {
                yield return HttpConstants.HttpHeaders.ShouldBatchContinueOnError;
            }
            if (this.StartEpk != null)
            {
                yield return HttpConstants.HttpHeaders.StartEpk;
            }
            if (this.StartId != null)
            {
                yield return HttpConstants.HttpHeaders.StartId;
            }
            if (this.SupportSpatialLegacyCoordinates != null)
            {
                yield return HttpConstants.HttpHeaders.SupportSpatialLegacyCoordinates;
            }
            if (this.SystemDocumentType != null)
            {
                yield return HttpConstants.HttpHeaders.SystemDocumentType;
            }
            if (this.TargetGlobalCommittedLsn != null)
            {
                yield return HttpConstants.HttpHeaders.TargetGlobalCommittedLsn;
            }
            if (this.TargetLsn != null)
            {
                yield return HttpConstants.HttpHeaders.TargetLsn;
            }
            if (this.TransportRequestID != null)
            {
                yield return HttpConstants.HttpHeaders.TransportRequestID;
            }
            if (this.UpdateMaxThroughputEverProvisioned != null)
            {
                yield return HttpConstants.HttpHeaders.UpdateMaxThroughputEverProvisioned;
            }
            if (this.UsePolygonsSmallerThanAHemisphere != null)
            {
                yield return HttpConstants.HttpHeaders.UsePolygonsSmallerThanAHemisphere;
            }
            if (this.AddResourcePropertiesToResponse != null)
            {
                yield return WFConstants.BackendHeaders.AddResourcePropertiesToResponse;
            }
            if (this.AllowTentativeWrites != null)
            {
                yield return WFConstants.BackendHeaders.AllowTentativeWrites;
            }
            if (this.BinaryId != null)
            {
                yield return WFConstants.BackendHeaders.BinaryId;
            }
            if (this.BinaryPassthroughRequest != null)
            {
                yield return WFConstants.BackendHeaders.BinaryPassthroughRequest;
            }
            if (this.BindReplicaDirective != null)
            {
                yield return WFConstants.BackendHeaders.BindReplicaDirective;
            }
            if (this.CollectionChildResourceContentLimitInKB != null)
            {
                yield return WFConstants.BackendHeaders.CollectionChildResourceContentLimitInKB;
            }
            if (this.CollectionChildResourceNameLimitInBytes != null)
            {
                yield return WFConstants.BackendHeaders.CollectionChildResourceNameLimitInBytes;
            }
            if (this.CollectionPartitionIndex != null)
            {
                yield return WFConstants.BackendHeaders.CollectionPartitionIndex;
            }
            if (this.CollectionRid != null)
            {
                yield return WFConstants.BackendHeaders.CollectionRid;
            }
            if (this.CollectionSecurityIdentifier != null)
            {
                yield return WFConstants.BackendHeaders.CollectionSecurityIdentifier;
            }
            if (this.CollectionServiceIndex != null)
            {
                yield return WFConstants.BackendHeaders.CollectionServiceIndex;
            }
            if (this.ContentSerializationFormat != null)
            {
                yield return WFConstants.BackendHeaders.ContentSerializationFormat;
            }
            if (this.EffectivePartitionKey != null)
            {
                yield return WFConstants.BackendHeaders.EffectivePartitionKey;
            }
            if (this.EnableDynamicRidRangeAllocation != null)
            {
                yield return WFConstants.BackendHeaders.EnableDynamicRidRangeAllocation;
            }
            if (this.ExcludeSystemProperties != null)
            {
                yield return WFConstants.BackendHeaders.ExcludeSystemProperties;
            }
            if (this.FanoutOperationState != null)
            {
                yield return WFConstants.BackendHeaders.FanoutOperationState;
            }
            if (this.ForceQueryScan != null)
            {
                yield return WFConstants.BackendHeaders.ForceQueryScan;
            }
            if (this.ForceSideBySideIndexMigration != null)
            {
                yield return WFConstants.BackendHeaders.ForceSideBySideIndexMigration;
            }
            if (this.IsFanoutRequest != null)
            {
                yield return WFConstants.BackendHeaders.IsFanoutRequest;
            }
            if (this.IsUserRequest != null)
            {
                yield return WFConstants.BackendHeaders.IsUserRequest;
            }
            if (this.MergeCheckPointGLSN != null)
            {
                yield return WFConstants.BackendHeaders.MergeCheckPointGLSN;
            }
            if (this.MergeStaticId != null)
            {
                yield return WFConstants.BackendHeaders.MergeStaticId;
            }
            if (this.PartitionCount != null)
            {
                yield return WFConstants.BackendHeaders.PartitionCount;
            }
            if (this.PartitionKeyRangeId != null)
            {
                yield return WFConstants.BackendHeaders.PartitionKeyRangeId;
            }
            if (this.PartitionResourceFilter != null)
            {
                yield return WFConstants.BackendHeaders.PartitionResourceFilter;
            }
            if (this.PopulateLogStoreInfo != null)
            {
                yield return WFConstants.BackendHeaders.PopulateLogStoreInfo;
            }
            if (this.PopulateUnflushedMergeEntryCount != null)
            {
                yield return WFConstants.BackendHeaders.PopulateUnflushedMergeEntryCount;
            }
            if (this.PreserveFullContent != null)
            {
                yield return WFConstants.BackendHeaders.PreserveFullContent;
            }
            if (this.PrimaryMasterKey != null)
            {
                yield return WFConstants.BackendHeaders.PrimaryMasterKey;
            }
            if (this.PrimaryReadonlyKey != null)
            {
                yield return WFConstants.BackendHeaders.PrimaryReadonlyKey;
            }
            if (this.RemoteStorageType != null)
            {
                yield return WFConstants.BackendHeaders.RemoteStorageType;
            }
            if (this.ResourceSchemaName != null)
            {
                yield return WFConstants.BackendHeaders.ResourceSchemaName;
            }
            if (this.ResourceTypes != null)
            {
                yield return WFConstants.BackendHeaders.ResourceTypes;
            }
            if (this.RestoreParams != null)
            {
                yield return WFConstants.BackendHeaders.RestoreParams;
            }
            if (this.SchemaHash != null)
            {
                yield return WFConstants.BackendHeaders.SchemaHash;
            }
            if (this.SchemaOwnerRid != null)
            {
                yield return WFConstants.BackendHeaders.SchemaOwnerRid;
            }
            if (this.SecondaryMasterKey != null)
            {
                yield return WFConstants.BackendHeaders.SecondaryMasterKey;
            }
            if (this.SecondaryReadonlyKey != null)
            {
                yield return WFConstants.BackendHeaders.SecondaryReadonlyKey;
            }
            if (this.ShareThroughput != null)
            {
                yield return WFConstants.BackendHeaders.ShareThroughput;
            }
            if (this.TimeToLiveInSeconds != null)
            {
                yield return WFConstants.BackendHeaders.TimeToLiveInSeconds;
            }
            if (this.TransactionCommit != null)
            {
                yield return WFConstants.BackendHeaders.TransactionCommit;
            }
            if (this.TransactionId != null)
            {
                yield return WFConstants.BackendHeaders.TransactionId;
            }
            if (this.UniqueIndexNameEncodingMode != null)
            {
                yield return WFConstants.BackendHeaders.UniqueIndexNameEncodingMode;
            }
            if (this.UseSystemBudget != null)
            {
                yield return WFConstants.BackendHeaders.UseSystemBudget;
            }

            if (this.lazyNotCommonHeaders.IsValueCreated)
            {
                foreach (string key in this.lazyNotCommonHeaders.Value.Keys)
                {
                    yield return key;
                }
            }
        }

        public override NameValueCollection ToNameValueCollection()
        {
            throw new NotImplementedException();
        }

        public override string Get(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

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
                    if (string.Equals(HttpConstants.HttpHeaders.Prefer, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.Prefer;
                    }
                
                    break;
                case 8:
                    if (string.Equals(HttpConstants.HttpHeaders.IfMatch, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.IfMatch;
                    }
                
                    break;
                case 9:
                    if (string.Equals(HttpConstants.HttpHeaders.XDate, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.XDate;
                    }
                
                    break;
                case 11:
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
                    if (string.Equals(HttpConstants.HttpHeaders.MigrateOfferToAutopilot, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.MigrateOfferToAutopilot;
                    }
                
                    break;
                case 40:
                    if (string.Equals(WFConstants.BackendHeaders.EnableDynamicRidRangeAllocation, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.EnableDynamicRidRangeAllocation;
                    }
                
                    break;
                case 42:
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
                    if (string.Equals(WFConstants.BackendHeaders.ContentSerializationFormat, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.ContentSerializationFormat;
                    }
                
                    break;
                case 46:
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
                    if (string.Equals(HttpConstants.HttpHeaders.UsePolygonsSmallerThanAHemisphere, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.UsePolygonsSmallerThanAHemisphere;
                    }
                
                    break;
                case 50:
                    if (string.Equals(HttpConstants.HttpHeaders.ResponseContinuationTokenLimitInKB, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.ResponseContinuationTokenLimitInKB;
                    }
                
                    break;
                case 51:
                    if (string.Equals(HttpConstants.HttpHeaders.EnableLowPrecisionOrderBy, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.EnableLowPrecisionOrderBy;
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
                    if (string.Equals(WFConstants.BackendHeaders.CollectionChildResourceContentLimitInKB, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.CollectionChildResourceContentLimitInKB;
                    }
                
                    break;
                case 57:
                    if (string.Equals(WFConstants.BackendHeaders.PopulateUnflushedMergeEntryCount, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.PopulateUnflushedMergeEntryCount;
                    }
                
                    break;
                case 59:
                    if (string.Equals(HttpConstants.HttpHeaders.UpdateMaxThroughputEverProvisioned, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return this.UpdateMaxThroughputEverProvisioned;
                    }
                
                    break;
                default:
                    break;
            }

            if (this.lazyNotCommonHeaders.IsValueCreated
                && this.lazyNotCommonHeaders.Value.TryGetValue(key, out string value))
            {
                return value;
            }
            
            return null;
        }

        public override void Add(string key, string value)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            this.UpdateHelper(
                key: key, 
                value: value, 
                throwIfAlreadyExists: true);
        }

        public override void Remove(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            this.UpdateHelper(
                key: key, 
                value: null, 
                throwIfAlreadyExists: false);
        }

        public override void Set(string key, string value)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            this.UpdateHelper(
                key: key, 
                value: value, 
                throwIfAlreadyExists: false);
        }

        public void UpdateHelper(
            string key, 
            string value,
            bool throwIfAlreadyExists)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            switch (key.Length)
            {
                case 4:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.HttpDate, key))
                    {
                        if (throwIfAlreadyExists && this.HttpDate != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.HttpDate = value;
                        return;
                    }
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.A_IM, key))
                    {
                        if (throwIfAlreadyExists && this.A_IM != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.A_IM = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.HttpDate, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.HttpDate != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.HttpDate = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.A_IM, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.A_IM != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.A_IM = value;
                        return;
                    }
                    break;
                case 6:
                    if (string.Equals(HttpConstants.HttpHeaders.Prefer, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.Prefer != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.Prefer = value;
                        return;
                    }
                    break;
                case 8:
                    if (string.Equals(HttpConstants.HttpHeaders.IfMatch, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.IfMatch != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.IfMatch = value;
                        return;
                    }
                    break;
                case 9:
                    if (string.Equals(HttpConstants.HttpHeaders.XDate, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.XDate != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.XDate = value;
                        return;
                    }
                    break;
                case 11:
                    if (string.Equals(HttpConstants.HttpHeaders.EndId, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.EndId != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.EndId = value;
                        return;
                    }
                    break;
                case 12:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.Version, key))
                    {
                        if (throwIfAlreadyExists && this.Version != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.Version = value;
                        return;
                    }
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.EndEpk, key))
                    {
                        if (throwIfAlreadyExists && this.EndEpk != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.EndEpk = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.Version, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.Version != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.Version = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.EndEpk, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.EndEpk != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.EndEpk = value;
                        return;
                    }
                    break;
                case 13:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.Authorization, key))
                    {
                        if (throwIfAlreadyExists && this.Authorization != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.Authorization = value;
                        return;
                    }
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IfNoneMatch, key))
                    {
                        if (throwIfAlreadyExists && this.IfNoneMatch != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.IfNoneMatch = value;
                        return;
                    }
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.StartId, key))
                    {
                        if (throwIfAlreadyExists && this.StartId != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.StartId = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.Authorization, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.Authorization != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.Authorization = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.IfNoneMatch, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.IfNoneMatch != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.IfNoneMatch = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.StartId, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.StartId != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.StartId = value;
                        return;
                    }
                    break;
                case 14:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.CanCharge, key))
                    {
                        if (throwIfAlreadyExists && this.CanCharge != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.CanCharge = value;
                        return;
                    }
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.StartEpk, key))
                    {
                        if (throwIfAlreadyExists && this.StartEpk != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.StartEpk = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.BinaryId, key))
                    {
                        if (throwIfAlreadyExists && this.BinaryId != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.BinaryId = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.CanCharge, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.CanCharge != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.CanCharge = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.StartEpk, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.StartEpk != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.StartEpk = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.BinaryId, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.BinaryId != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.BinaryId = value;
                        return;
                    }
                    break;
                case 15:
                    if (string.Equals(HttpConstants.HttpHeaders.TargetLsn, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.TargetLsn != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.TargetLsn = value;
                        return;
                    }
                    break;
                case 16:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.CanThrottle, key))
                    {
                        if (throwIfAlreadyExists && this.CanThrottle != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.CanThrottle = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.SchemaHash, key))
                    {
                        if (throwIfAlreadyExists && this.SchemaHash != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.SchemaHash = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.CanThrottle, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.CanThrottle != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.CanThrottle = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.SchemaHash, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.SchemaHash != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.SchemaHash = value;
                        return;
                    }
                    break;
                case 17:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.Continuation, key))
                    {
                        if (throwIfAlreadyExists && this.Continuation != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.Continuation = value;
                        return;
                    }
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IfModifiedSince, key))
                    {
                        if (throwIfAlreadyExists && this.IfModifiedSince != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.IfModifiedSince = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.BindReplicaDirective, key))
                    {
                        if (throwIfAlreadyExists && this.BindReplicaDirective != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.BindReplicaDirective = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.TransactionId, key))
                    {
                        if (throwIfAlreadyExists && this.TransactionId != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.TransactionId = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.Continuation, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.Continuation != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.Continuation = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.IfModifiedSince, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.IfModifiedSince != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.IfModifiedSince = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.BindReplicaDirective, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.BindReplicaDirective != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.BindReplicaDirective = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.TransactionId, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.TransactionId != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.TransactionId = value;
                        return;
                    }
                    break;
                case 18:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.ReadFeedKeyType, key))
                    {
                        if (throwIfAlreadyExists && this.ReadFeedKeyType != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.ReadFeedKeyType = value;
                        return;
                    }
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.SessionToken, key))
                    {
                        if (throwIfAlreadyExists && this.SessionToken != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.SessionToken = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.ReadFeedKeyType, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.ReadFeedKeyType != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.ReadFeedKeyType = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.SessionToken, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.SessionToken != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.SessionToken = value;
                        return;
                    }
                    break;
                case 19:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.PageSize, key))
                    {
                        if (throwIfAlreadyExists && this.PageSize != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PageSize = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.RestoreParams, key))
                    {
                        if (throwIfAlreadyExists && this.RestoreParams != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.RestoreParams = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.PageSize, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.PageSize != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PageSize = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.RestoreParams, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.RestoreParams != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.RestoreParams = value;
                        return;
                    }
                    break;
                case 20:
                    if (string.Equals(HttpConstants.HttpHeaders.ProfileRequest, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.ProfileRequest != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.ProfileRequest = value;
                        return;
                    }
                    break;
                case 21:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.SchemaOwnerRid, key))
                    {
                        if (throwIfAlreadyExists && this.SchemaOwnerRid != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.SchemaOwnerRid = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ShareThroughput, key))
                    {
                        if (throwIfAlreadyExists && this.ShareThroughput != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.ShareThroughput = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.TransactionCommit, key))
                    {
                        if (throwIfAlreadyExists && this.TransactionCommit != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.TransactionCommit = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.SchemaOwnerRid, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.SchemaOwnerRid != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.SchemaOwnerRid = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.ShareThroughput, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.ShareThroughput != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.ShareThroughput = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.TransactionCommit, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.TransactionCommit != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.TransactionCommit = value;
                        return;
                    }
                    break;
                case 22:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.ConsistencyLevel, key))
                    {
                        if (throwIfAlreadyExists && this.ConsistencyLevel != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.ConsistencyLevel = value;
                        return;
                    }
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.GatewaySignature, key))
                    {
                        if (throwIfAlreadyExists && this.GatewaySignature != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.GatewaySignature = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.IsFanoutRequest, key))
                    {
                        if (throwIfAlreadyExists && this.IsFanoutRequest != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.IsFanoutRequest = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.ConsistencyLevel, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.ConsistencyLevel != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.ConsistencyLevel = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.GatewaySignature, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.GatewaySignature != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.GatewaySignature = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.IsFanoutRequest, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.IsFanoutRequest != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.IsFanoutRequest = value;
                        return;
                    }
                    break;
                case 23:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IndexingDirective, key))
                    {
                        if (throwIfAlreadyExists && this.IndexingDirective != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.IndexingDirective = value;
                        return;
                    }
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IsReadOnlyScript, key))
                    {
                        if (throwIfAlreadyExists && this.IsReadOnlyScript != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.IsReadOnlyScript = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.PrimaryMasterKey, key))
                    {
                        if (throwIfAlreadyExists && this.PrimaryMasterKey != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PrimaryMasterKey = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.IndexingDirective, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.IndexingDirective != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.IndexingDirective = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.IsReadOnlyScript, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.IsReadOnlyScript != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.IsReadOnlyScript = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.PrimaryMasterKey, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.PrimaryMasterKey != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PrimaryMasterKey = value;
                        return;
                    }
                    break;
                case 24:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IsBatchAtomic, key))
                    {
                        if (throwIfAlreadyExists && this.IsBatchAtomic != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.IsBatchAtomic = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.CollectionServiceIndex, key))
                    {
                        if (throwIfAlreadyExists && this.CollectionServiceIndex != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.CollectionServiceIndex = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.RemoteStorageType, key))
                    {
                        if (throwIfAlreadyExists && this.RemoteStorageType != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.RemoteStorageType = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.IsBatchAtomic, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.IsBatchAtomic != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.IsBatchAtomic = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.CollectionServiceIndex, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.CollectionServiceIndex != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.CollectionServiceIndex = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.RemoteStorageType, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.RemoteStorageType != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.RemoteStorageType = value;
                        return;
                    }
                    break;
                case 25:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IsBatchOrdered, key))
                    {
                        if (throwIfAlreadyExists && this.IsBatchOrdered != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.IsBatchOrdered = value;
                        return;
                    }
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.TransportRequestID, key))
                    {
                        if (throwIfAlreadyExists && this.TransportRequestID != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.TransportRequestID = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.PrimaryReadonlyKey, key))
                    {
                        if (throwIfAlreadyExists && this.PrimaryReadonlyKey != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PrimaryReadonlyKey = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ResourceSchemaName, key))
                    {
                        if (throwIfAlreadyExists && this.ResourceSchemaName != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.ResourceSchemaName = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ResourceTypes, key))
                    {
                        if (throwIfAlreadyExists && this.ResourceTypes != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.ResourceTypes = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.SecondaryMasterKey, key))
                    {
                        if (throwIfAlreadyExists && this.SecondaryMasterKey != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.SecondaryMasterKey = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.IsBatchOrdered, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.IsBatchOrdered != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.IsBatchOrdered = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.TransportRequestID, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.TransportRequestID != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.TransportRequestID = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.PrimaryReadonlyKey, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.PrimaryReadonlyKey != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PrimaryReadonlyKey = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.ResourceSchemaName, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.ResourceSchemaName != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.ResourceSchemaName = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.ResourceTypes, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.ResourceTypes != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.ResourceTypes = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.SecondaryMasterKey, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.SecondaryMasterKey != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.SecondaryMasterKey = value;
                        return;
                    }
                    break;
                case 26:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.EnumerationDirection, key))
                    {
                        if (throwIfAlreadyExists && this.EnumerationDirection != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.EnumerationDirection = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.CollectionPartitionIndex, key))
                    {
                        if (throwIfAlreadyExists && this.CollectionPartitionIndex != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.CollectionPartitionIndex = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.EnumerationDirection, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.EnumerationDirection != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.EnumerationDirection = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.CollectionPartitionIndex, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.CollectionPartitionIndex != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.CollectionPartitionIndex = value;
                        return;
                    }
                    break;
                case 27:
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.FanoutOperationState, key))
                    {
                        if (throwIfAlreadyExists && this.FanoutOperationState != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.FanoutOperationState = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.MergeStaticId, key))
                    {
                        if (throwIfAlreadyExists && this.MergeStaticId != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.MergeStaticId = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.SecondaryReadonlyKey, key))
                    {
                        if (throwIfAlreadyExists && this.SecondaryReadonlyKey != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.SecondaryReadonlyKey = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.FanoutOperationState, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.FanoutOperationState != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.FanoutOperationState = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.MergeStaticId, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.MergeStaticId != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.MergeStaticId = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.SecondaryReadonlyKey, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.SecondaryReadonlyKey != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.SecondaryReadonlyKey = value;
                        return;
                    }
                    break;
                case 28:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.PartitionKey, key))
                    {
                        if (throwIfAlreadyExists && this.PartitionKey != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PartitionKey = value;
                        return;
                    }
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.RestoreMetadataFilter, key))
                    {
                        if (throwIfAlreadyExists && this.RestoreMetadataFilter != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.RestoreMetadataFilter = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.EffectivePartitionKey, key))
                    {
                        if (throwIfAlreadyExists && this.EffectivePartitionKey != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.EffectivePartitionKey = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.TimeToLiveInSeconds, key))
                    {
                        if (throwIfAlreadyExists && this.TimeToLiveInSeconds != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.TimeToLiveInSeconds = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.UseSystemBudget, key))
                    {
                        if (throwIfAlreadyExists && this.UseSystemBudget != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.UseSystemBudget = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.PartitionKey, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.PartitionKey != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PartitionKey = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.RestoreMetadataFilter, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.RestoreMetadataFilter != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.RestoreMetadataFilter = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.EffectivePartitionKey, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.EffectivePartitionKey != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.EffectivePartitionKey = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.TimeToLiveInSeconds, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.TimeToLiveInSeconds != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.TimeToLiveInSeconds = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.UseSystemBudget, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.UseSystemBudget != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.UseSystemBudget = value;
                        return;
                    }
                    break;
                case 30:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.ResourceTokenExpiry, key))
                    {
                        if (throwIfAlreadyExists && this.ResourceTokenExpiry != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.ResourceTokenExpiry = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.CollectionRid, key))
                    {
                        if (throwIfAlreadyExists && this.CollectionRid != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.CollectionRid = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ExcludeSystemProperties, key))
                    {
                        if (throwIfAlreadyExists && this.ExcludeSystemProperties != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.ExcludeSystemProperties = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.PartitionCount, key))
                    {
                        if (throwIfAlreadyExists && this.PartitionCount != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PartitionCount = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.PartitionResourceFilter, key))
                    {
                        if (throwIfAlreadyExists && this.PartitionResourceFilter != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PartitionResourceFilter = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.ResourceTokenExpiry, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.ResourceTokenExpiry != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.ResourceTokenExpiry = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.CollectionRid, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.CollectionRid != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.CollectionRid = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.ExcludeSystemProperties, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.ExcludeSystemProperties != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.ExcludeSystemProperties = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.PartitionCount, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.PartitionCount != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PartitionCount = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.PartitionResourceFilter, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.PartitionResourceFilter != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PartitionResourceFilter = value;
                        return;
                    }
                    break;
                case 31:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.CanOfferReplaceComplete, key))
                    {
                        if (throwIfAlreadyExists && this.CanOfferReplaceComplete != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.CanOfferReplaceComplete = value;
                        return;
                    }
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.ClientRetryAttemptCount, key))
                    {
                        if (throwIfAlreadyExists && this.ClientRetryAttemptCount != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.ClientRetryAttemptCount = value;
                        return;
                    }
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IsClientEncrypted, key))
                    {
                        if (throwIfAlreadyExists && this.IsClientEncrypted != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.IsClientEncrypted = value;
                        return;
                    }
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.SystemDocumentType, key))
                    {
                        if (throwIfAlreadyExists && this.SystemDocumentType != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.SystemDocumentType = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.BinaryPassthroughRequest, key))
                    {
                        if (throwIfAlreadyExists && this.BinaryPassthroughRequest != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.BinaryPassthroughRequest = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.CanOfferReplaceComplete, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.CanOfferReplaceComplete != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.CanOfferReplaceComplete = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.ClientRetryAttemptCount, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.ClientRetryAttemptCount != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.ClientRetryAttemptCount = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.IsClientEncrypted, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.IsClientEncrypted != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.IsClientEncrypted = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.SystemDocumentType, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.SystemDocumentType != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.SystemDocumentType = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.BinaryPassthroughRequest, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.BinaryPassthroughRequest != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.BinaryPassthroughRequest = value;
                        return;
                    }
                    break;
                case 32:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.MaxPollingIntervalMilliseconds, key))
                    {
                        if (throwIfAlreadyExists && this.MaxPollingIntervalMilliseconds != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.MaxPollingIntervalMilliseconds = value;
                        return;
                    }
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.MigrateCollectionDirective, key))
                    {
                        if (throwIfAlreadyExists && this.MigrateCollectionDirective != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.MigrateCollectionDirective = value;
                        return;
                    }
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.TargetGlobalCommittedLsn, key))
                    {
                        if (throwIfAlreadyExists && this.TargetGlobalCommittedLsn != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.TargetGlobalCommittedLsn = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ForceQueryScan, key))
                    {
                        if (throwIfAlreadyExists && this.ForceQueryScan != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.ForceQueryScan = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.MaxPollingIntervalMilliseconds, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.MaxPollingIntervalMilliseconds != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.MaxPollingIntervalMilliseconds = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.MigrateCollectionDirective, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.MigrateCollectionDirective != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.MigrateCollectionDirective = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.TargetGlobalCommittedLsn, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.TargetGlobalCommittedLsn != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.TargetGlobalCommittedLsn = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.ForceQueryScan, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.ForceQueryScan != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.ForceQueryScan = value;
                        return;
                    }
                    break;
                case 33:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.EmitVerboseTracesInQuery, key))
                    {
                        if (throwIfAlreadyExists && this.EmitVerboseTracesInQuery != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.EmitVerboseTracesInQuery = value;
                        return;
                    }
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.EnableScanInQuery, key))
                    {
                        if (throwIfAlreadyExists && this.EnableScanInQuery != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.EnableScanInQuery = value;
                        return;
                    }
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.PopulateQuotaInfo, key))
                    {
                        if (throwIfAlreadyExists && this.PopulateQuotaInfo != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PopulateQuotaInfo = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.PopulateLogStoreInfo, key))
                    {
                        if (throwIfAlreadyExists && this.PopulateLogStoreInfo != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PopulateLogStoreInfo = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.PreserveFullContent, key))
                    {
                        if (throwIfAlreadyExists && this.PreserveFullContent != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PreserveFullContent = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.EmitVerboseTracesInQuery, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.EmitVerboseTracesInQuery != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.EmitVerboseTracesInQuery = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.EnableScanInQuery, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.EnableScanInQuery != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.EnableScanInQuery = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.PopulateQuotaInfo, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.PopulateQuotaInfo != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PopulateQuotaInfo = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.PopulateLogStoreInfo, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.PopulateLogStoreInfo != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PopulateLogStoreInfo = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.PreserveFullContent, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.PreserveFullContent != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PreserveFullContent = value;
                        return;
                    }
                    break;
                case 34:
                    if (string.Equals(WFConstants.BackendHeaders.AllowTentativeWrites, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.AllowTentativeWrites != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.AllowTentativeWrites = value;
                        return;
                    }
                    break;
                case 35:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.FilterBySchemaResourceId, key))
                    {
                        if (throwIfAlreadyExists && this.FilterBySchemaResourceId != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.FilterBySchemaResourceId = value;
                        return;
                    }
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.PreTriggerExclude, key))
                    {
                        if (throwIfAlreadyExists && this.PreTriggerExclude != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PreTriggerExclude = value;
                        return;
                    }
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.PreTriggerInclude, key))
                    {
                        if (throwIfAlreadyExists && this.PreTriggerInclude != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PreTriggerInclude = value;
                        return;
                    }
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.RemainingTimeInMsOnClientRequest, key))
                    {
                        if (throwIfAlreadyExists && this.RemainingTimeInMsOnClientRequest != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.RemainingTimeInMsOnClientRequest = value;
                        return;
                    }
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.ShouldBatchContinueOnError, key))
                    {
                        if (throwIfAlreadyExists && this.ShouldBatchContinueOnError != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.ShouldBatchContinueOnError = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.CollectionSecurityIdentifier, key))
                    {
                        if (throwIfAlreadyExists && this.CollectionSecurityIdentifier != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.CollectionSecurityIdentifier = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.PartitionKeyRangeId, key))
                    {
                        if (throwIfAlreadyExists && this.PartitionKeyRangeId != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PartitionKeyRangeId = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.FilterBySchemaResourceId, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.FilterBySchemaResourceId != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.FilterBySchemaResourceId = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.PreTriggerExclude, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.PreTriggerExclude != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PreTriggerExclude = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.PreTriggerInclude, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.PreTriggerInclude != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PreTriggerInclude = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.RemainingTimeInMsOnClientRequest, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.RemainingTimeInMsOnClientRequest != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.RemainingTimeInMsOnClientRequest = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.ShouldBatchContinueOnError, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.ShouldBatchContinueOnError != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.ShouldBatchContinueOnError = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.CollectionSecurityIdentifier, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.CollectionSecurityIdentifier != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.CollectionSecurityIdentifier = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.PartitionKeyRangeId, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.PartitionKeyRangeId != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PartitionKeyRangeId = value;
                        return;
                    }
                    break;
                case 36:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IncludeTentativeWrites, key))
                    {
                        if (throwIfAlreadyExists && this.IncludeTentativeWrites != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.IncludeTentativeWrites = value;
                        return;
                    }
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.PopulateQueryMetrics, key))
                    {
                        if (throwIfAlreadyExists && this.PopulateQueryMetrics != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PopulateQueryMetrics = value;
                        return;
                    }
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.PostTriggerExclude, key))
                    {
                        if (throwIfAlreadyExists && this.PostTriggerExclude != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PostTriggerExclude = value;
                        return;
                    }
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.PostTriggerInclude, key))
                    {
                        if (throwIfAlreadyExists && this.PostTriggerInclude != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PostTriggerInclude = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.IsUserRequest, key))
                    {
                        if (throwIfAlreadyExists && this.IsUserRequest != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.IsUserRequest = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.IncludeTentativeWrites, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.IncludeTentativeWrites != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.IncludeTentativeWrites = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.PopulateQueryMetrics, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.PopulateQueryMetrics != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PopulateQueryMetrics = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.PostTriggerExclude, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.PostTriggerExclude != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PostTriggerExclude = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.PostTriggerInclude, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.PostTriggerInclude != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PostTriggerInclude = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.IsUserRequest, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.IsUserRequest != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.IsUserRequest = value;
                        return;
                    }
                    break;
                case 37:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.EnableLogging, key))
                    {
                        if (throwIfAlreadyExists && this.EnableLogging != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.EnableLogging = value;
                        return;
                    }
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.PopulateResourceCount, key))
                    {
                        if (throwIfAlreadyExists && this.PopulateResourceCount != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PopulateResourceCount = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.EnableLogging, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.EnableLogging != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.EnableLogging = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.PopulateResourceCount, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.PopulateResourceCount != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PopulateResourceCount = value;
                        return;
                    }
                    break;
                case 38:
                    if (string.Equals(HttpConstants.HttpHeaders.MigrateOfferToAutopilot, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.MigrateOfferToAutopilot != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.MigrateOfferToAutopilot = value;
                        return;
                    }
                    break;
                case 40:
                    if (string.Equals(WFConstants.BackendHeaders.EnableDynamicRidRangeAllocation, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.EnableDynamicRidRangeAllocation != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.EnableDynamicRidRangeAllocation = value;
                        return;
                    }
                    break;
                case 42:
                    if (string.Equals(WFConstants.BackendHeaders.MergeCheckPointGLSN, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.MergeCheckPointGLSN != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.MergeCheckPointGLSN = value;
                        return;
                    }
                    break;
                case 43:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.DisableRUPerMinuteUsage, key))
                    {
                        if (throwIfAlreadyExists && this.DisableRUPerMinuteUsage != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.DisableRUPerMinuteUsage = value;
                        return;
                    }
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.PopulatePartitionStatistics, key))
                    {
                        if (throwIfAlreadyExists && this.PopulatePartitionStatistics != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PopulatePartitionStatistics = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.ForceSideBySideIndexMigration, key))
                    {
                        if (throwIfAlreadyExists && this.ForceSideBySideIndexMigration != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.ForceSideBySideIndexMigration = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.UniqueIndexNameEncodingMode, key))
                    {
                        if (throwIfAlreadyExists && this.UniqueIndexNameEncodingMode != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.UniqueIndexNameEncodingMode = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.DisableRUPerMinuteUsage, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.DisableRUPerMinuteUsage != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.DisableRUPerMinuteUsage = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.PopulatePartitionStatistics, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.PopulatePartitionStatistics != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PopulatePartitionStatistics = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.ForceSideBySideIndexMigration, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.ForceSideBySideIndexMigration != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.ForceSideBySideIndexMigration = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.UniqueIndexNameEncodingMode, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.UniqueIndexNameEncodingMode != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.UniqueIndexNameEncodingMode = value;
                        return;
                    }
                    break;
                case 44:
                    if (string.Equals(WFConstants.BackendHeaders.ContentSerializationFormat, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.ContentSerializationFormat != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.ContentSerializationFormat = value;
                        return;
                    }
                    break;
                case 46:
                    if (string.Equals(HttpConstants.HttpHeaders.MigrateOfferToManualThroughput, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.MigrateOfferToManualThroughput != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.MigrateOfferToManualThroughput = value;
                        return;
                    }
                    break;
                case 47:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.SupportSpatialLegacyCoordinates, key))
                    {
                        if (throwIfAlreadyExists && this.SupportSpatialLegacyCoordinates != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.SupportSpatialLegacyCoordinates = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.AddResourcePropertiesToResponse, key))
                    {
                        if (throwIfAlreadyExists && this.AddResourcePropertiesToResponse != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.AddResourcePropertiesToResponse = value;
                        return;
                    }
                    if (object.ReferenceEquals(WFConstants.BackendHeaders.CollectionChildResourceNameLimitInBytes, key))
                    {
                        if (throwIfAlreadyExists && this.CollectionChildResourceNameLimitInBytes != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.CollectionChildResourceNameLimitInBytes = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.SupportSpatialLegacyCoordinates, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.SupportSpatialLegacyCoordinates != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.SupportSpatialLegacyCoordinates = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.AddResourcePropertiesToResponse, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.AddResourcePropertiesToResponse != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.AddResourcePropertiesToResponse = value;
                        return;
                    }
                    if (string.Equals(WFConstants.BackendHeaders.CollectionChildResourceNameLimitInBytes, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.CollectionChildResourceNameLimitInBytes != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.CollectionChildResourceNameLimitInBytes = value;
                        return;
                    }
                    break;
                case 48:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.GetAllPartitionKeyStatistics, key))
                    {
                        if (throwIfAlreadyExists && this.GetAllPartitionKeyStatistics != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.GetAllPartitionKeyStatistics = value;
                        return;
                    }
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.PopulateCollectionThroughputInfo, key))
                    {
                        if (throwIfAlreadyExists && this.PopulateCollectionThroughputInfo != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PopulateCollectionThroughputInfo = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.GetAllPartitionKeyStatistics, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.GetAllPartitionKeyStatistics != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.GetAllPartitionKeyStatistics = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.PopulateCollectionThroughputInfo, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.PopulateCollectionThroughputInfo != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PopulateCollectionThroughputInfo = value;
                        return;
                    }
                    break;
                case 49:
                    if (string.Equals(HttpConstants.HttpHeaders.UsePolygonsSmallerThanAHemisphere, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.UsePolygonsSmallerThanAHemisphere != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.UsePolygonsSmallerThanAHemisphere = value;
                        return;
                    }
                    break;
                case 50:
                    if (string.Equals(HttpConstants.HttpHeaders.ResponseContinuationTokenLimitInKB, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.ResponseContinuationTokenLimitInKB != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.ResponseContinuationTokenLimitInKB = value;
                        return;
                    }
                    break;
                case 51:
                    if (string.Equals(HttpConstants.HttpHeaders.EnableLowPrecisionOrderBy, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.EnableLowPrecisionOrderBy != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.EnableLowPrecisionOrderBy = value;
                        return;
                    }
                    break;
                case 53:
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IsOfferStorageRefreshRequest, key))
                    {
                        if (throwIfAlreadyExists && this.IsOfferStorageRefreshRequest != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.IsOfferStorageRefreshRequest = value;
                        return;
                    }
                    if (object.ReferenceEquals(HttpConstants.HttpHeaders.IsRUPerGBEnforcementRequest, key))
                    {
                        if (throwIfAlreadyExists && this.IsRUPerGBEnforcementRequest != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.IsRUPerGBEnforcementRequest = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.IsOfferStorageRefreshRequest, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.IsOfferStorageRefreshRequest != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.IsOfferStorageRefreshRequest = value;
                        return;
                    }
                    if (string.Equals(HttpConstants.HttpHeaders.IsRUPerGBEnforcementRequest, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.IsRUPerGBEnforcementRequest != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.IsRUPerGBEnforcementRequest = value;
                        return;
                    }
                    break;
                case 56:
                    if (string.Equals(WFConstants.BackendHeaders.CollectionChildResourceContentLimitInKB, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.CollectionChildResourceContentLimitInKB != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.CollectionChildResourceContentLimitInKB = value;
                        return;
                    }
                    break;
                case 57:
                    if (string.Equals(WFConstants.BackendHeaders.PopulateUnflushedMergeEntryCount, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.PopulateUnflushedMergeEntryCount != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.PopulateUnflushedMergeEntryCount = value;
                        return;
                    }
                    break;
                case 59:
                    if (string.Equals(HttpConstants.HttpHeaders.UpdateMaxThroughputEverProvisioned, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (throwIfAlreadyExists && this.UpdateMaxThroughputEverProvisioned != null)
                        {
                            throw new ArgumentException($"The {key} already exists in the collection");
                        }

                        this.UpdateMaxThroughputEverProvisioned = value;
                        return;
                    }
                    break;
                default:
                    break;
            }

            if (throwIfAlreadyExists)
            {
                this.lazyNotCommonHeaders.Value.Add(key, value);
            }
            else
            {
                if (value == null)
                {
                    this.lazyNotCommonHeaders.Value.Remove(key);
                }
                else
                {
                    this.lazyNotCommonHeaders.Value[key] = value;
                }
            }
        }
    }
}