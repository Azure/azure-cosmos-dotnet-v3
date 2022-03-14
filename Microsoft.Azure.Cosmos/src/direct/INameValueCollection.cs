//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Collections
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;

    internal interface INameValueCollection : IEnumerable
    {
        void Add(string key, string value);

        void Set(string key, string value);

        string Get(string key);

        string this[string key] { get;  set; }

        void Remove(string key);

        void Clear();

        int Count();

        INameValueCollection Clone();

        void Add(INameValueCollection collection);

        string[] GetValues(string key);

        string[] AllKeys();

        IEnumerable<string> Keys();

        NameValueCollection ToNameValueCollection();
    }

    /*
     * TransportSerialization.FillTokenFromHeader(request, HttpConstants.HttpHeaders.Authorization, rntbdRequest.authorizationToken, rntbdRequest);
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
            TransportSerialization.FillTokenFromHeader(request, WFConstants.BackendHeaders.SchemaId, rntbdRequest.schemaId, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, HttpConstants.HttpHeaders.IsClientEncrypted, rntbdRequest.isClientEncrypted, rntbdRequest);
            TransportSerialization.FillTokenFromHeader(request, WFConstants.BackendHeaders.CorrelatedActivityId, rntbdRequest.correlatedActivityId, rntbdRequest);
     */
    internal interface IRequestHeaders
    {
        string Authorization { get; }
        string SessionToken { get; }
        string PreTriggerInclude { get; }
        string PreTriggerExclude { get; }
        string PostTriggerInclude { get; }
        string PostTriggerExclude { get; }
        string PartitionKey { get; }
        string PartitionKeyRangeId { get; }
        string ResourceTokenExpiry { get; }
        string FilterBySchemaResourceId { get; }
        string ShouldBatchContinueOnError { get; }
        string IsBatchOrdered { get; }
        string IsBatchAtomic { get; }
        string CollectionPartitionIndex { get; }
        string CollectionServiceIndex { get; }
        string ResourceSchemaName { get; }
        string BindReplicaDirective { get; }
        string PrimaryMasterKey { get; }
        string SecondaryMasterKey { get; }
        string PrimaryReadonlyKey { get; }
        string SecondaryReadonlyKey { get; }
        string PartitionCount { get; }
        string CollectionRid { get; }
        string GatewaySignature { get; }
        string RemainingTimeInMsOnClientRequest { get; }
        string ClientRetryAttemptCount { get; }
        string TargetLsn { get; }
        string TargetGlobalCommittedLsn { get; }
        string TransportRequestID { get; }
        string RestoreMetadataFilter { get; }
        string RestoreParams { get; }
        string PartitionResourceFilter { get; }
        string EnableDynamicRidRangeAllocation { get; }
        string SchemaOwnerRid { get; }
        string SchemaHash { get; }
        string SchemaId { get; }
        string IsClientEncrypted { get; }
        string CorrelatedActivityId { get; }
        string TimeToLiveInSeconds { get; }
        string BinaryPassthroughRequest { get; }
        string AllowTentativeWrites { get; }
        string IncludeTentativeWrites { get; }
        string MaxPollingIntervalMilliseconds { get; }
        string PopulateLogStoreInfo { get; }
        string MergeCheckPointGLSN { get; }
        string PopulateUnflushedMergeEntryCount { get; }
        string AddResourcePropertiesToResponse { get; }
        string SystemRestoreOperation { get; }
        string ChangeFeedStartFullFidelityIfNoneMatch { get; }
        string SkipRefreshDatabaseAccountConfigs { get; }
        string IntendedCollectionRid { get; }
        string UseArchivalPartition { get; }
        string CollectionTruncate { get; }
        string SDKSupportedCapabilities { get; }
        string PopulateUniqueIndexReIndexProgress { get; }
        string IsMaterializedViewBuild { get; }
        string BuilderClientIdentifier { get; }
        string SourceCollectionIfMatch { get; }
        string PopulateAnalyticalMigrationProgress { get; }
        string ShouldReturnCurrentServerDateTime { get; }
        string RbacUserId { get; }
        string RbacAction { get; }
        string RbacResource { get; }
        string ChangeFeedWireFormatVersion { get; }
        string PopulateByokEncryptionProgress { get; }
        string UseUserBackgroundBudget { get; }
        string IncludePhysicalPartitionThroughputInfo { get; }
        string Version { get; }
    }

    internal class RequestHeaderWrapperForNameValue : IRequestHeaders
    {
        private readonly INameValueCollection nameValueCollection;
        public RequestHeaderWrapperForNameValue(INameValueCollection nameValueCollection)
        {
            this.nameValueCollection = nameValueCollection;
        }

        public string Authorization => this.nameValueCollection[HttpConstants.HttpHeaders.Authorization];

        public string SessionToken => this.nameValueCollection[HttpConstants.HttpHeaders.SessionToken];

        public string PreTriggerInclude => this.nameValueCollection[HttpConstants.HttpHeaders.PreTriggerInclude];

        public string PreTriggerExclude => this.nameValueCollection[HttpConstants.HttpHeaders.PreTriggerExclude];

        public string PostTriggerInclude => this.nameValueCollection[HttpConstants.HttpHeaders.PostTriggerInclude];

        public string PostTriggerExclude => this.nameValueCollection[HttpConstants.HttpHeaders.PostTriggerExclude];

        public string PartitionKey => this.nameValueCollection[HttpConstants.HttpHeaders.PartitionKey];

        public string PartitionKeyRangeId => this.nameValueCollection[HttpConstants.HttpHeaders.PartitionKeyRangeId];

        public string ResourceTokenExpiry => this.nameValueCollection[HttpConstants.HttpHeaders.ResourceTokenExpiry];

        public string FilterBySchemaResourceId => this.nameValueCollection[HttpConstants.HttpHeaders.FilterBySchemaResourceId];

        public string ShouldBatchContinueOnError => this.nameValueCollection[HttpConstants.HttpHeaders.ShouldBatchContinueOnError];

        public string IsBatchOrdered => this.nameValueCollection[HttpConstants.HttpHeaders.IsBatchOrdered];

        public string IsBatchAtomic => this.nameValueCollection[HttpConstants.HttpHeaders.IsBatchAtomic];

        public string CollectionPartitionIndex => this.nameValueCollection[WFConstants.BackendHeaders.CollectionPartitionIndex];

        public string CollectionServiceIndex => this.nameValueCollection[WFConstants.BackendHeaders.CollectionServiceIndex];

        public string ResourceSchemaName => this.nameValueCollection[WFConstants.BackendHeaders.ResourceSchemaName];

        public string BindReplicaDirective => this.nameValueCollection[WFConstants.BackendHeaders.BindReplicaDirective];

        public string PrimaryMasterKey => this.nameValueCollection[WFConstants.BackendHeaders.PrimaryMasterKey];

        public string SecondaryMasterKey => this.nameValueCollection[WFConstants.BackendHeaders.SecondaryMasterKey];

        public string PrimaryReadonlyKey => this.nameValueCollection[WFConstants.BackendHeaders.PrimaryReadonlyKey];

        public string SecondaryReadonlyKey => this.nameValueCollection[WFConstants.BackendHeaders.SecondaryReadonlyKey];

        public string PartitionCount => this.nameValueCollection[WFConstants.BackendHeaders.PartitionCount];

        public string CollectionRid => this.nameValueCollection[WFConstants.BackendHeaders.CollectionRid];

        public string GatewaySignature => this.nameValueCollection[HttpConstants.HttpHeaders.GatewaySignature];

        public string RemainingTimeInMsOnClientRequest => this.nameValueCollection[HttpConstants.HttpHeaders.RemainingTimeInMsOnClientRequest];

        public string ClientRetryAttemptCount => this.nameValueCollection[HttpConstants.HttpHeaders.ClientRetryAttemptCount];

        public string TargetLsn => this.nameValueCollection[HttpConstants.HttpHeaders.TargetLsn];

        public string TargetGlobalCommittedLsn => this.nameValueCollection[HttpConstants.HttpHeaders.TargetGlobalCommittedLsn];

        public string TransportRequestID => this.nameValueCollection[HttpConstants.HttpHeaders.TransportRequestID];

        public string RestoreMetadataFilter => this.nameValueCollection[HttpConstants.HttpHeaders.RestoreMetadataFilter];

        public string RestoreParams => this.nameValueCollection[WFConstants.BackendHeaders.RestoreParams];

        public string PartitionResourceFilter => this.nameValueCollection[WFConstants.BackendHeaders.PartitionResourceFilter];

        public string EnableDynamicRidRangeAllocation => this.nameValueCollection[WFConstants.BackendHeaders.EnableDynamicRidRangeAllocation];

        public string SchemaOwnerRid => this.nameValueCollection[WFConstants.BackendHeaders.SchemaOwnerRid];

        public string SchemaHash => this.nameValueCollection[WFConstants.BackendHeaders.SchemaHash];

        public string SchemaId => this.nameValueCollection[WFConstants.BackendHeaders.SchemaId];

        public string IsClientEncrypted => this.nameValueCollection[HttpConstants.HttpHeaders.IsClientEncrypted];

        public string CorrelatedActivityId => this.nameValueCollection[WFConstants.BackendHeaders.CorrelatedActivityId];

        public string TimeToLiveInSeconds => this.nameValueCollection[WFConstants.BackendHeaders.TimeToLiveInSeconds];

        public string BinaryPassthroughRequest => this.nameValueCollection[WFConstants.BackendHeaders.BinaryPassthroughRequest];

        public string AllowTentativeWrites => this.nameValueCollection[HttpConstants.HttpHeaders.AllowTentativeWrites];

        public string IncludeTentativeWrites => this.nameValueCollection[HttpConstants.HttpHeaders.IncludeTentativeWrites];

        public string MaxPollingIntervalMilliseconds => this.nameValueCollection[HttpConstants.HttpHeaders.MaxPollingIntervalMilliseconds];

        public string PopulateLogStoreInfo => this.nameValueCollection[WFConstants.BackendHeaders.PopulateLogStoreInfo];

        public string MergeCheckPointGLSN => this.nameValueCollection[WFConstants.BackendHeaders.MergeCheckPointGLSN];

        public string PopulateUnflushedMergeEntryCount => this.nameValueCollection[WFConstants.BackendHeaders.PopulateUnflushedMergeEntryCount];

        public string AddResourcePropertiesToResponse => this.nameValueCollection[WFConstants.BackendHeaders.AddResourcePropertiesToResponse];

        public string SystemRestoreOperation => this.nameValueCollection[HttpConstants.HttpHeaders.SystemRestoreOperation];

        public string ChangeFeedStartFullFidelityIfNoneMatch => this.nameValueCollection[HttpConstants.HttpHeaders.ChangeFeedStartFullFidelityIfNoneMatch];

        public string SkipRefreshDatabaseAccountConfigs => this.nameValueCollection[WFConstants.BackendHeaders.SkipRefreshDatabaseAccountConfigs];

        public string IntendedCollectionRid => this.nameValueCollection[WFConstants.BackendHeaders.IntendedCollectionRid];

        public string UseArchivalPartition => this.nameValueCollection[HttpConstants.HttpHeaders.UseArchivalPartition];

        public string CollectionTruncate => this.nameValueCollection[HttpConstants.HttpHeaders.CollectionTruncate];

        public string SDKSupportedCapabilities => this.nameValueCollection[HttpConstants.HttpHeaders.SDKSupportedCapabilities];

        public string PopulateUniqueIndexReIndexProgress => this.nameValueCollection[HttpConstants.HttpHeaders.PopulateUniqueIndexReIndexProgress];

        public string IsMaterializedViewBuild => this.nameValueCollection[HttpConstants.HttpHeaders.IsMaterializedViewBuild];

        public string BuilderClientIdentifier => this.nameValueCollection[HttpConstants.HttpHeaders.BuilderClientIdentifier];

        public string SourceCollectionIfMatch => this.nameValueCollection[WFConstants.BackendHeaders.SourceCollectionIfMatch];

        public string PopulateAnalyticalMigrationProgress => this.nameValueCollection[HttpConstants.HttpHeaders.PopulateAnalyticalMigrationProgress];

        public string ShouldReturnCurrentServerDateTime => this.nameValueCollection[HttpConstants.HttpHeaders.ShouldReturnCurrentServerDateTime];

        public string RbacUserId => this.nameValueCollection[HttpConstants.HttpHeaders.RbacUserId];

        public string RbacAction => this.nameValueCollection[HttpConstants.HttpHeaders.RbacAction];

        public string RbacResource => this.nameValueCollection[HttpConstants.HttpHeaders.RbacResource];

        public string ChangeFeedWireFormatVersion => this.nameValueCollection[HttpConstants.HttpHeaders.ChangeFeedWireFormatVersion];

        public string PopulateByokEncryptionProgress => this.nameValueCollection[HttpConstants.HttpHeaders.PopulateByokEncryptionProgress];

        public string UseUserBackgroundBudget => this.nameValueCollection[WFConstants.BackendHeaders.UseUserBackgroundBudget];

        public string IncludePhysicalPartitionThroughputInfo => this.nameValueCollection[HttpConstants.HttpHeaders.IncludePhysicalPartitionThroughputInfo];

        public string Version => this.nameValueCollection[HttpConstants.HttpHeaders.Version];
    }
}
