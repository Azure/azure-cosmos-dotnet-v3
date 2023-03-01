﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    internal static class Constants
    {
        public const char DBSeparator = ';';
        public const char ModeSeparator = ':';
        public const char StartArray = '[';
        public const char EndArray = ']';
        public const char PartitionSeparator = ',';
        public const char UrlPathSeparator = '/';

        public const string DataContractNamespace = "http://schemas.microsoft.com/windowsazure";

        // This should match (maxResourceSize - resourceSizePadding) from Server settings.xml
        public const int MaxResourceSizeInBytes = (2 * 1024 * 1024) - 1;

        // This should match maxBatchRequestBodySize from Server settings.xml
        public const int MaxDirectModeBatchRequestBodySizeInBytes = 2202010;

        // This should match maxBatchOperationsPerRequest from Server settings.xml
        public const int MaxOperationsInDirectModeBatchRequest = 100;

        public const int MaxGatewayModeBatchRequestBodySizeInBytes = 16 * 1024 * 1024;

        public const int MaxOperationsInGatewayModeBatchRequest = 1000;

        public const string FirewallAuthorization = "FirewallAuthorization";

        // As per https://docs.microsoft.com/en-us/azure/storage/common/storage-redundancy
        // table secondary endpoint is suffixed by -secondary.
        public const string TableSecondaryEndpointSuffix = "-secondary";

        // Attribute stamped on BLOB containers/BLOBs
        // for various purposes.
        // MUST be in sync with XStoreUploader native code.
        public static class Quota
        {
            // Quota Header Strings, must be in sync with ResourceQuotaManager.h
            public const string Database = "databases";
            public const string Collection = "collections";
            public const string User = "users";
            public const string Permission = "permissions";
            public const string CollectionSize = "collectionSize";
            public const string DocumentsSize = "documentsSize";
            public const string DocumentsCount = "documentsCount";
            public const string SampledDistinctPartitionKeyCount = "sampledDistinctPartitionKeyCount";
            public const string StoredProcedure = "storedProcedures";
            public const string Trigger = "triggers";
            public const string UserDefinedFunction = "functions";
            public static char[] DelimiterChars = { '=', ';' };
        }

        public static class BlobStorageAttributes
        {
            // Container type. Only key is relevant. However set value to be
            // same as key to permit future enhancements.
            public const string attachmentContainer = "x_ms_attachmentContainer";

            // Container type. Only key is relevant. However set value to be
            // same as key to permit future enhancements.
            public const string backupContainer = "x_ms_backupContainer";

            // Container retention period.
            public const string backupRetentionIntervalInHours = "x_ms_backupRetentionIntervalInHours";

            // Container backup interval.
            public const string backupIntervalInMinutes = "x_ms_backupIntervalInMinutes";

            // non-FQDN.
            public const string owningFederation = "x_ms_owningFederation";

            // Name of the DatabaseAccount for which this blob stores data
            public const string owningDatabaseAccountName = "x_ms_databaseAccountName";

            // Last UTC time the attachment in Azure storage was checked and identified
            // to be NOT an orphan (i.e., still referenced from a document)
            public const string lastAttachmentCheckTime = "x_ms_lastAttachmentCheckTimeUtc";

            // Last UTC time the attachment (or an attachment container)
            // in Azure storage was checked and identified AS an orphan
            // (i.e., NOT referenced from a document or in use by any DSI)
            public const string orphanAttachmentProbationStartTime = "x_ms_orphanAttachmentProbationStartTimeUtc";

            // Last UTC time a backup container in Azure storage was checked and identified
            // AS an orphan (i.e., NOT used by any DSI in the federation)
            public const string orphanBackupProbationStartTime = "x_ms_orphanBackupProbationStartTime";

            // FQDN hostname stamped on document attachment BLOB uploads
            // to scope orphan attachment BLOB cleanup to
            // only those BLOBs that were uploaded by a given
            // OneBox.
            public const string oneBoxHostName = "x_ms_OneBoxHostName";

            // The last scan time of a backup log container
            public const string lastScanTime = "x_ms_lastScanTime";

            // Backup hold until
            public const string backupHoldUntil = "x_ms_backupHoldUntil";

            // Backup valid starting time
            public const string backupStartTime = "x_ms_backupStartTime";

            // Backup valid ending time
            public const string backupTime = "x_ms_backupTime";
        }

        public static class CosmosDBRequestProperties
        {
            public const string IsAllowedOrigin = "isAllowedOrigin";
        }

        public static class Offers
        {
            public const string OfferVersion_None = "";
            public const string OfferVersion_V1 = "V1";
            public const string OfferVersion_V2 = "V2";

            public const string OfferType_Invalid = "Invalid";
        }

        public static class MongoServerVersion
        {
            public const string Version3_2 = "3.2";
            public const string Version3_6 = "3.6";
            public const string Version4_0 = "4.0";
            public const string Version4_2 = "4.2";
        }

        internal static class Indexing
        {
            public const int IndexingSchemeVersionV1 = 1;
            public const int IndexingSchemeVersionV2 = 2;
        }

        public static class PartitionedQueryExecutionInfo
        {
            public const int Version_1 = 1;
            public const int Version_2 = 2;
            public const int CurrentVersion = PartitionedQueryExecutionInfo.Version_2;
        }

        public static class Properties
        {
            public const string Resource = "resource";
            public const string Options = "options";
            public const string SubscriptionId = "subscriptionId";
            public const string FabricClientEndpoints = "fabricClientEndpoints";
            public const string SubscriptionUsageType = "subscriptionUsageType";
            public const string EnabledLocations = "enabledLocations";
            public const string SubscriptionState = "subscriptionState";
            public const string MigratedSubscriptionId = "migratedSubscriptionId";
            public const string QuotaId = "quotaId";
            public const string OfferCategories = "offerCategories";
            public const string DocumentServiceName = "documentServiceName";
            public const string OperationId = "operationId";
            public const string OperationDetails = "operationDetails";
            public const string AffinityGroupName = "affinityGroupName";
            public const string LocationName = "locationName";
            public const string SubRegionId = "subRegionId";
            public const string InstanceSize = "instanceSize";
            public const string Status = "status";
            public const string IsMerged = "merged";
            public const string RequestedStatus = "requestedStatus";
            public const string ExtendedStatus = "extendedStatus";
            public const string ExtendedResult = "extendedResult";
            public const string StatusCode = "statusCode";
            public const string DocumentEndpoint = "documentEndpoint";
            public const string TableEndpoint = "tableEndpoint";
            public const string TableSecondaryEndpoint = "tableSecondaryEndpoint";
            public const string GremlinEndpoint = "gremlinEndpoint";
            public const string CassandraEndpoint = "cassandraEndpoint";
            public const string EtcdEndpoint = "etcdEndpoint";
            public const string SqlEndpoint = "sqlEndpoint";
            public const string SqlxEndpoint = "sqlxEndpoint";
            public const string MongoEndpoint = "mongoEndpoint";
            public const string AnalyticsEndpoint = "analyticsEndpoint";
            public const string DatabaseAccountEndpoint = "databaseAccountEndpoint";
            public const string PrimaryMasterKey = "primaryMasterKey";
            public const string SecondaryMasterKey = "secondaryMasterKey";
            public const string PrimaryReadonlyMasterKey = "primaryReadonlyMasterKey";
            public const string SecondaryReadonlyMasterKey = "secondaryReadonlyMasterKey";
            public const string PrimaryMasterKeyGenerationTimestamp = "primaryMasterKeyGenerationTimestamp";
            public const string SecondaryMasterKeyGenerationTimestamp = "secondaryMasterKeyGenerationTimestamp";
            public const string PrimaryReadonlyKeyGenerationTimestamp = "primaryReadonlyKeyGenerationTimestamp";
            public const string SecondaryReadonlyKeyGenerationTimestamp = "secondaryReadonlyMasterKeyGenerationTimestamp";
            public const string DatabaseAccountKeysMetadata = "keysMetadata";
            public const string ConnectionStrings = "connectionStrings";
            public const string ConnectionString = "connectionString";
            public const string Description = "description";
            public const string ResourceKeySeed = "resourceKeySeed";
            public const string MaxStoredProceduresPerCollection = "maxStoredProceduresPerCollection";
            public const string MaxUDFsPerCollections = "maxUDFsPerCollections";
            public const string MaxTriggersPerCollection = "maxTriggersPerCollection";
            public const string IsolationLevel = "isolationLevel";
            public const string SubscriptionDisabled = "subscriptionDisabled";
            public const string IsDynamic = "isDynamic";
            public const string FederationName = "federationName";
            public const string FederationName1 = "federationName1";
            public const string FederationName2 = "federationName2";
            public const string FederationId = "federationId";
            public const string ComputeFederationId = "computeFederationId";
            public const string PlacementHint = "placementHint";
            public const string CreationTimestamp = "creationTimestamp";
            public const string SourceCreationTimestamp = "sourceCreationTimestamp";
            public const string SourceDeletionTimestamp = "sourceDeletionTimestamp";
            public const string ExtendedProperties = "extendedProperties";
            public const string KeyKind = "keyKind";
            public const string SystemKeyKind = "systemKeyKind";
            public const string ScaleUnits = "scaleUnits";
            public const string Location = "location";
            public const string Kind = "kind";
            public const string Region1 = "region1";
            public const string Region2 = "region2";
            public const string WritableLocations = "writableLocations";
            public const string ReadableLocations = "readableLocations";
            public const string Tags = "tags";
            public const string ResourceGroupName = "resourceGroupName";
            public const string PropertiesName = "properties";
            public const string RegionalPropertiesName = "regionalStateProperties";
            public const string ProvisioningState = "provisioningState";
            public const string CommunicationAPIKind = "communicationAPIKind";
            public const string UseMongoGlobalCacheAccountCursor = "useMongoGlobalCacheAccountCursor";
            public const string StorageSizeInMB = "storageSizeInMB";
            public const string DatabaseAccountOfferType = "databaseAccountOfferType";
            public const string Type = "type";
            public const string TargetType = "targetType";
            public const string TargetSku = "targetSku";
            public const string TargetTier = "targetTier";
            public const string Error = "error";
            public const string State = "state";
            public const string RegistrationDate = "registrationDate";
            public const string Value = "value";
            public const string NextLink = "nextLink";
            public const string DestinationSubscription = "destinationSubscription";
            public const string TargetResourceGroup = "targetResourceGroup";
            public const string ConfigurationOverrides = "configurationOverrides";
            public const string Name = "name";
            public const string Fqdn = "fqdn";
            public const string PublicIPAddressResourceName = "publicIPAddressName";
            public const string PublicIPAddressResourceGroup = "publicIPAddressResourceGroup";
            public const string VnetName = "vnetName";
            public const string VnetResourceGroup = "vnetResourceGroup";
            public const string Ipv4Address = "Ipv4Address";
            public const string RoleNameSuffix = "roleNameSuffix";
            public const string ReservedCname = "reservedCname";
            public const string ServiceType = "serviceType";
            public const string ServiceCount = "serviceCount";
            public const string TargetCapacityInMB = "targetCapacityInMB";
            public const string IsRuntimeServiceBindingEnabled = "isRuntimeServiceBindingEnabled";
            public const string MaxDocumentCollectionCount = "maxDocumentCollectionCount";
            public const string Reserved = "reserved";
            public const string Resources = "resources";
            public const string NamingConfiguration = "namingConfiguration";
            public const string DatabaseAccountName = "databaseAccountName";
            public const string DocumentServiceApiEndpoint = "documentServiceApiEndpoint";
            public const string Health = "health";
            public const string APIVersion = "APIVersion";
            public const string EmitArrayContainsForMongoQueries = "emitArrayContainsForMongoQueries";
            public const string Flights = "flights";
            public const string MigratedRegionalAccountName = "migratedRegionalAccountName";
            public const string DatabaseResourceId = "databaseResourceId";
            public const string SubscriptionKind = "subscriptionKind";
            public const string PlacementPolicies = "placementPolicies";
            public const string Action = "action";
            public const string Audience = "audience";
            public const string AudienceKind = "audienceKind";
            public const string OfferKind = "offerKind";
            public const string TenantId = "tenantId";
            public const string SupportedCapabilities = "supportedCapabilities";
            public const string PlacementHints = "placementHints";
            public const string IsMasterService = "isMasterService";
            public const string IsCappedForServer = "isCappedForServer";
            public const string IsCappedForMaster = "isCappedForMaster";
            public const string CapabilityResource = "capabilityResource";
            public const string DocumentType = "documentType";
            public const string SystemDatabaseAccountStoreType = "systemDatabaseAccountStoreType";
            public const string FederationType = "federationType";
            public const string FederationGenerationKind = "federationGenerationKind";
            public const string UseEPKandNameAsPrimaryKeyInDocumentTable = "useEPKandNameAsPrimaryKeyInDocumentTable";
            public const string EnableUserDefinedType = "enableUserDefinedType";
            public const string ExcludeOwnerIdFromDocumentTable = "excludeOwnerIdFromDocumentTable";
            public const string EnableQuerySupportForHybridRow = "enableQuerySupportForHybridRow";
            public const string FederationProxyFqdn = "federationProxyFqdn";
            public const string IsFailedOver = "isFailedOver";
            public const string FederationProxyReservedCname = "federationProxyReservedCname";
            public const string EnableMultiMasterMigration = "enableMultiMasterMigration";
            public const string EnableNativeGridFS = "enableNativeGridFS";
            public const string MongoDefaultsVersion = "mongoDefaultsVersion";
            public const string ServerVersion = "serverVersion";
            public const string ContinuousBackupInformation = "continuousBackupInformation";
            public const string LatestRestorableTimestamp = "latestRestorableTimestamp";
            public const string EnableBinaryEncodingOfContent = "enableBinaryEncodingOfContent";
            public const string SkipMigrateToComputeDatabaseCollectionChecks = "skipMigrateToComputeDatabaseCollectionChecks";
            public const string UseApiOperationHandler = "UseApiOperationHandler";
            public const string AccountKeyManagementType = "accountKeyManagementType";
            public const string ManagedReadWriteAccountKeyResource = "managedReadWriteAccountKeyResource";
            public const string IsManagedReadWriteAccountKeyResourceStale = "isManagedReadWriteAccountKeyResourceStale";
            public const string ManagedReadOnlyAccountKeyResource = "managedReadOnlyAccountKeyResource";
            public const string IsManagedReadOnlyAccountKeyResourceStale = "isManagedReadOnlyAccountKeyResourceStale";
            public const string UserName = "userName";
            public const string Subscriptions = "subscriptions";
            public const string Providers = "providers";
            public const string RestorableDatabaseAccounts = "restorableDatabaseAccounts";

            public const string MasterValue = "masterValue";
            public const string SecondaryValue = "secondaryValue";

            public const string ArmLocation = "armLocation";

            // Query
            public const string Query = "query";
            public const string Parameters = "parameters";

            // Global Database account properties
            public const string GlobalDatabaseAccountName = "globalDatabaseAccountName";
            public const string FailoverPriority = "failoverPriority";
            public const string FailoverPolicies = "failoverPolicies";
            public const string Locations = "locations";
            public const string WriteLocations = "writeLocations";
            public const string ReadLocations = "readLocations";
            public const string LocationType = "locationType";
            public const string MasterServiceUri = "masterServiceUri";
            public const string RestoreParams = "restoreParameters";
            public const string CreateMode = "createMode";
            public const string RestoreMode = "restoreMode";
            public const string StoreType = "storeType";

            // CSM Metric properties
            public const string LocalizedValue = "localizedValue";
            public const string Unit = "unit";
            public const string ResourceUri = "resourceUri";
            public const string PrimaryAggregationType = "primaryAggregationType";
            public const string MetricAvailabilities = "metricAvailabilities";
            public const string MetricValues = "metricValues";
            public const string TimeGrain = "timeGrain";
            public const string Retention = "retention";
            public const string TimeStamp = "timestamp";
            public const string Average = "average";
            public const string Minimum = "minimum";
            public const string Maximum = "maximum";
            public const string MetricCount = "count";
            public const string Total = "total";
            public const string StartTime = "startTime";
            public const string EndTime = "endTime";
            public const string DisplayName = "displayName";
            public const string Limit = "limit";
            public const string CurrentValue = "currentValue";
            public const string NextResetTime = "nextResetTime";
            public const string QuotaPeriod = "quotaPeriod";
            public const string SupportedRegions = "supportedRegions";
            public const string Percentiles = "percentiles";
            public const string SourceRegion = "sourceRegion";
            public const string TargetRegion = "targetRegion";
            public const string P10 = "P10";
            public const string P25 = "P25";
            public const string P50 = "P50";
            public const string P75 = "P75";
            public const string P90 = "P90";
            public const string P95 = "P95";
            public const string P99 = "P99";

            public const string Id = "id";
            public const string RId = "_rid";
            public const string SelfLink = "_self";
            public const string LastModified = "_ts";
            public const string CreatedTime = "_cts";
            public const string Count = "_count";
            public const string ETag = "_etag";
            public const string TimeToLive = "ttl";
            public const string DefaultTimeToLive = "defaultTtl";
            public const string TimeToLivePropertyPath = "ttlPropertyPath";
            public const string AnalyticalStorageTimeToLive = "analyticalStorageTtl";

            public const string DatabasesLink = "_dbs";
            public const string CollectionsLink = "_colls";
            public const string UsersLink = "_users";
            public const string PermissionsLink = "_permissions";
            public const string AttachmentsLink = "_attachments";
            public const string StoredProceduresLink = "_sprocs";
            public const string TriggersLink = "_triggers";
            public const string UserDefinedFunctionsLink = "_udfs";
            public const string ConflictsLink = "_conflicts";
            public const string DocumentsLink = "_docs";
            public const string ResourceLink = "resource";
            public const string MediaLink = "media";
            public const string SchemasLink = "_schemas";

            public const string PermissionMode = "permissionMode";
            public const string ResourceKey = "key";
            public const string Token = "_token";

            public const string FederationOperationKind = "federationOperationKind";
            public const string RollbackKind = "rollbackKind";

            // Collection Truncate
            public const string CollectionTruncate = "collectionTruncate";

            // Indexing Policy.
            public const string IndexingPolicy = "indexingPolicy";
            public const string Automatic = "automatic";
            public const string StringPrecision = "StringPrecision";
            public const string NumericPrecision = "NumericPrecision";
            public const string MaxPathDepth = "maxPathDepth";
            public const string IndexingMode = "indexingMode";
            public const string IndexType = "IndexType";
            public const string IndexKind = "kind";
            public const string DataType = "dataType";
            public const string Precision = "precision";
            public const string PartitionKind = "kind";
            public const string SystemKey = "systemKey";

            public const string Paths = "paths";
            public const string Path = "path";
            public const string IsFullIndex = "isFullIndex";
            public const string Filter = "filter";
            public const string FrequentPaths = "Frequent";
            public const string IncludedPaths = "includedPaths";
            public const string InFrequentPaths = "InFrequent";
            public const string ExcludedPaths = "excludedPaths";
            public const string Indexes = "indexes";
            public const string IndexingSchemeVersion = "IndexVersion";

            public const string CompositeIndexes = "compositeIndexes";
            public const string Order = "order";

            public const string SpatialIndexes = "spatialIndexes";
            public const string Types = "types";
            public const string BoundingBox = "boundingBox";
            public const string Xmin = "xmin";
            public const string Ymin = "ymin";
            public const string Xmax = "xmax";
            public const string Ymax = "ymax";

            public const string EnableIndexingSchemeV2 = "enableIndexingSchemeV2";

            // GeospatialConfig
            public const string GeospatialType = "type";
            public const string GeospatialConfig = "geospatialConfig";

            // Unique index.
            public const string UniqueKeyPolicy = "uniqueKeyPolicy";
            public const string UniqueKeys = "uniqueKeys";
            public const string UniqueIndexReIndexContext = "uniqueIndexReIndexContext";
            public const string UniqueIndexReIndexingState = "uniqueIndexReIndexingState";
            public const string LastDocumentGLSN = "lastDocumentGLSN";
            public const string UniqueIndexNameEncodingMode = "uniqueIndexNameEncodingMode";

            // ChangeFeed policy
            public const string ChangeFeedPolicy = "changeFeedPolicy";
            public const string LogRetentionDuration = "retentionDuration";

            //MaterializedViewDefinition
            public const string MaterializedViewDefinition = "materializedViewDefinition";
            public const string AllowMaterializedViews = "allowMaterializedViews";
            public const string SourceCollectionRid = "sourceCollectionRid";
            public const string SourceCollectionId = "sourceCollectionId";
            public const string Definition = "definition";
            public const string ApiSpecificDefinition = "apiSpecificDefinition";
            public const string AllowMaterializedViewsInCollectionDeleteRollForward = "allowMaterializedViewsInCollectionDeleteRollForward";
            public const string MaterializedViews = "materializedViews";

            // Schema Policy
            public const string SchemaPolicy = "schemaPolicy";

            // PartitionKeyDelete
            public const string PartitionKeyDeleteThroughputFraction = "partitionKeyDeleteThroughputFraction";
            public const string EnableMultiRegionGlsnCheckForPKDelete = "enableMultiRegionGlsnCheckForPKDelete";

            // Internal Schema Properties
            public const string InternalSchemaProperties = "internalSchemaProperties";
            public const string UseSchemaForAnalyticsOnly = "useSchemaForAnalyticsOnly";

            // ReplicaResource
            public const string Quota = "quota";

            // PartitionInfo
            public const string ResourceType = "resourceType";
            public const string ServiceIndex = "serviceIndex";
            public const string PartitionIndex = "partitionIndex";

            // ModuleCommand
            public const string ModuleEvent = "moduleEvent";
            public const string ModuleEventReason = "moduleEventReason";
            public const string ModuleStatus = "moduleStatus";
            public const string ThrottleLevel = "throttleLevel";
            public const string ProcessId = "processId";
            public const string HasFaulted = "hasFaulted";
            public const string Result = "result";

            // ConsistencyPolicy
            public const string ConsistencyPolicy = "consistencyPolicy";
            public const string DefaultConsistencyLevel = "defaultConsistencyLevel";
            public const string MaxStalenessPrefix = "maxStalenessPrefix";
            public const string MaxStalenessIntervalInSeconds = "maxIntervalInSeconds";

            // ReplicationPolicy
            public const string ReplicationPolicy = "replicationPolicy";
            public const string AsyncReplication = "asyncReplication";
            public const string MaxReplicaSetSize = "maxReplicasetSize";
            public const string MinReplicaSetSize = "minReplicaSetSize";

            // WritePolicy.
            public const string WritePolicy = "writePolicy";
            public const string PrimaryCheckpointInterval = "primaryLoggingIntervalInMilliSeconds";
            public const string SecondaryCheckpointInterval = "secondaryLoggingIntervalInMilliSeconds";

            // Backup container properties
            public const string Collection = "RootResourceName";
            public const string CollectionId = "collectionId";
            public const string Completed = "completed";
            public const string BackupOfferType = "OfferType";
            public const string BackupDatabaseAccountName = "x_ms_databaseAccountName";
            public const string BackupContainerName = "backupContainerName";
            public const string UniquePartitionIdentifier = "UniquePartitionIdentifier";

            // Backup store settings
            public const string BackupStoreUri = "fileUploaderUri";
            public const string BackupStoreAccountName = "fileUploaderAccountName";

            // Backup Policy
            public const string GlobalBackupPolicy = "globalBackupPolicy";
            public const string BackupPolicy = "backupPolicy";
            public const string CollectionBackupPolicy = "backupPolicy";
            public const string CollectionBackupType = "type";
            public const string BackupStrategy = "backupStrategy";
            public const string BackupRedundancy = "backupRedundancy";
            public const string BackupStorageRedundancy = "backupStorageRedundancy";
            public const string BackupIntervalInMinutes = "backupIntervalInMinutes";
            public const string BackupRetentionIntervalInHours = "backupRetentionIntervalInHours";
            public const string PeriodicModeProperties = "periodicModeProperties";
            public const string ContinuousModeProperties = "continuousModeProperties";
            public const string BackupPolicyMigrationState = "migrationState";

            // PITR Migration
            public const string PitrMigrationState = "pitrMigrationState";
            public const string PitrMigrationStatus = "pitrMigrationStatus";
            public const string PitrMigrationBeginTimestamp = "pitrMigrationBeginTimestamp";
            public const string PitrMigrationEndTimestamp = "pitrMigrationEndTimestamp";
            public const string PitrMigrationAttemptTimestamp = "pitrMigrationAttemptTimestamp";
            public const string PreMigrationBackupPolicy = "preMigrationBackupPolicy";
            public const string LastPitrStandardOptInTimestamp = "lastPitrStandardOptInTimestamp";
            public const string LastPitrBasicOptInTimestamp = "lastPitrBasicOptInTimestamp";
            public const string PitrMigrationStartTimestamp = "startTime";
            public const string TargetPitrSku = "targetPitrSku";

            // Backup Storage Accounts
            public const string BackupStorageAccountsEnabled = "BackupStorageAccountsEnabled";
            public const string BackupStorageAccountNames = "BackupStorageAccountNames";
            public const string BackupStorageUris = "BackupStorageUris";
            public const string TotalNumberOfDedicatedStorageAccounts = "TotalNumberOfDedicatedStorageAccounts";
            public const string ReplaceOriginalBackupStorageAccounts = "replaceOriginalBackupStorageAccounts";
            public const string StandardStreamGRSStorageServiceNames = "standardStreamGRSStorageServiceNames";
            public const string EnableDedicatedStorageAccounts = "enableDedicatedStorageAccounts";
            public const string EnableSystemSnapshots = "enableSystemSnapshots";
            public const string BackupStorageRedundancies = "backupStorageRedundancies";
            public const string StorageAccountName = "storageAccountName";
            public const string StorageAccountLocation = "storageAccountLocation";

            // Restore Policy
            public const string RestorePolicy = "restorePolicy";
            public const string SourceServiceName = "sourceServiceName";
            public const string RegionalDatabaseAccountInstanceId = "regionalDatabaseAccountInstanceId";
            public const string GlobalDatabaseAccountInstanceId = "globalDatabaseAccountInstanceId";
            public const string InstanceId = "instanceId";
            public const string SourceBackupLocation = "sourceBackupLocation";
            public const string ReuseSourceDatabaseAccountAccessKeys = "reuseSourceDatabaseAccountAccessKeys";
            public const string RecreateDatabase = "recreateDatabase";
            public const string LatestBackupSnapshotInDateTime = "latestBackupSnapshotInDateTime";
            public const string TargetFederation = "targetFederation";
            public const string TargetFederationKind = "targetFederationKind";
            public const string CollectionOrDatabaseResourceIds = "collectionOrDatabaseResourceIds";
            public const string SourceHasMultipleStorageAccounts = "sourceHasMultipleStorageAccounts";
            public const string AllowPartialRestore = "allowPartialRestore";
            public const string DedicatedBackupAccountNames = "dedicatedBackupAccountNames";
            public const string AllowPartitionsRestoreOptimization = "allowPartitionsRestoreOptimization";
            public const string UseUniquePartitionIdBasedRestoreWorkflow = "useUniquePartitionIdBasedRestoreWorkflow";
            public const string UseSnapshotToRestore = "useSnapshotToRestore";
            public const string AllowVnetRestore = "allowVnetRestore";
            public const string UpdateBackupContainerMetadata = "updateBackupContainerMetadata";
            public const string RestoreWithBuiltinDatabaseAccountPicker = "restoreWithBuiltinDatabaseAccountPicker";
            public const string RestoreWithSourceGlobalDatabaseAccountInstanceId = "restoreWithSourceGlobalDatabaseAccountInstanceId";
            public const string DatabasesToRestore = "databasesToRestore";
            public const string DatabaseName = "databaseName";
            public const string CollectionNames = "collectionNames";
            public const string GraphNames = "graphNames";
            public const string TableNames = "tableNames";

            // Restore Properties
            public const string RestoreTimestampInUtc = "restoreTimestampInUtc";
            public const string RestoreSource = "restoreSource";
            public const string IsInAccountRestoreCapabilityEnabled = "isInAccountRestoreCapabilityEnabled";

            // Backup Hold
            public const string BackupHoldTimeInDays = "backupHoldTimeInDays";

            // Restore Tags
            public const string RestoredSourceDatabaseAccountName = "restoredSourceDatabaseAccountName";
            public const string RestoredAtTimestamp = "restoredAtTimestamp";

            // Read Policy
            // Indicates the relative weight of read from primary compared to secondary.
            // Higher this value, cheaper are the reads from primary.
            public const string PrimaryReadCoefficient = "primaryReadCoefficient";
            public const string SecondaryReadCoefficient = "secondaryReadCoefficient";

            // Scripting
            public const string Body = "body";
            public const string TriggerType = "triggerType";
            public const string TriggerOperation = "triggerOperation";

            public const string MaxSize = "maxSize";

            public const string Content = "content";

            public const string ContentType = "contentType";

            // ErrorResource.
            public const string Code = "code";
            public const string Message = "message";
            public const string ErrorDetails = "errorDetails";
            public const string AdditionalErrorInfo = "additionalErrorInfo";

            // AddressResource
            public const string IsPrimary = "isPrimary";
            public const string Protocol = "protocol";
            public const string LogicalUri = "logicalUri";
            public const string PhysicalUri = "physcialUri";

            // Authorization
            public const string AuthorizationFormat = "type={0}&ver={1}&sig={2}";
            public const string MasterToken = "master";
            public const string ResourceToken = "resource";
            public const string AadToken = "aad";
            public const string SasToken = "sas";
            public const string TokenVersion = "1.0";
            public const string AuthSchemaType = "type";
            public const string AuthVersion = "ver";
            public const string AuthSignature = "sig";
            public const string readPermissionMode = "read";
            public const string allPermissionMode = "all";

            // Federation Resource
            public const string PackageVersion = "packageVersion";
            public const string FabricApplicationVersion = "fabricApplicationVersion";
            public const string FabricRingCodeVersion = "fabricRingCodeVersion";
            public const string FabricRingConfigVersion = "fabricRingConfigVersion";
            public const string CertificateVersion = "certificateVersion";
            public const string DeploymentId = "deploymentId";
            public const string DecommisionedFederations = "decommisionedFederations";
            public const string FederationKind = "federationKind";
            public const string SupportedPlacementHints = "supportedPlacementHints";
            public const string PrimarySystemKeyReadOnly = "primarySystemKeyReadOnly";
            public const string SecondarySystemKeyReadOnly = "secondarySystemKeyReadOnly";
            public const string UsePrimarySystemKeyReadOnly = "UsePrimarySystemKeyReadOnly";
            public const string PrimarySystemKeyReadWrite = "primarySystemKeyReadWrite";
            public const string SecondarySystemKeyReadWrite = "secondarySystemKeyReadWrite";
            public const string UsePrimarySystemKeyReadWrite = "UsePrimarySystemKeyReadWrite";
            public const string PrimarySystemKeyAll = "primarySystemKeyAll";
            public const string SecondarySystemKeyAll = "secondarySystemKeyAll";
            public const string UsePrimarySystemKeyAll = "UsePrimarySystemKeyAll";
            public const string UseSecondaryComputeGatewayKey = "UseSecondaryComputeGatewayKey";
            public const string Weight = "Weight";
            public const string BatchId = "BatchId";
            public const string IsCappedForServerPartitionAllocation = "isCappedForServerPartitionAllocation";
            public const string IsDirty = "IsDirty";
            public const string IsAvailabilityZoneFederation = "IsAvailabilityZoneFederation";
            public const string AZIndex = "AZIndex";
            public const string IsFederationUnavailable = "isFederationUnavailable";
            public const string ServiceExtensions = "serviceExtensions";
            public const string HostedServicesDescription = "hostedServicesDescription";
            public const string CsesMigratedHostedServicesDescription = "csesMigratedHostedServicesDescription";
            public const string FederationProxyResource = "federationProxyResource";
            public const string ReservedDnsName = "reservedDnsName";
            public const string BuildoutStatus = "BuildoutStatus";
            public const string IsCsesCloudService = "IsCsesCloudService";
            public const string IsInBitLockerRotation = "IsInBitLockerRotation";
            public const string PrimaryBitLockerKeyVersion = "PrimaryBitLockerKeyVersion";
            public const string SecondaryBitLockerKeyVersion = "SecondaryBitLockerKeyVersion";
            public const string BitLockerKeysRotationState = "BitLockerKeysRotationState";

            //Deployment Constants
            public const string FabricApplicationName = "fabricApplicationName";
            public const string UseMonitoredUpgrade = "useMonitoredUpgrade";
            public const string UpgradeInfo = "UpgradeInfo";

            // ManagementVersion Resource
            public const string Version = "Version";
            public const string ManagementEndPoint = "ManagementEndPoint";

            // StorageService Resource
            public const string StorageServiceName = "name";
            public const string StorageServiceLocation = "location";
            public const string BlobEndpoint = "blobEndpoint";
            public const string StorageServiceSubscriptionId = "subscriptionId";
            public const string StorageServiceIndex = "storageIndex";
            public const string IsWritable = "isWritable";
            public const string StorageServiceKind = "storageServiceKind";
            public const string StorageAccountType = "storageAccountType";
            public const string StorageServiceResourceGroupName = "resourceGroupName";
            public const string StorageServiceFederationId = "federationId";
            public const string StorageAccountSku = "storageAccountSku";
            public const string IsRegisteredWithSms = "isRegisteredWithSms";
            public const string StorageAccountVersion = "storageAccountVersion";
            public const string StorageAccounts = "StorageAccounts";

            // Cassandra Connector Constants
            public const string ConnectorOffer = "connectorOffer";
            public const string EnableCassandraConnector = "enableCassandraConnector";
            public const string ConnectorMetadataAccountInfo = "connectorMetadataAccountInfo";
            public const string ConnectorMetadataAccountPrimaryKey = "connectorMetadataAccountPrimaryKey";
            public const string ConnectorMetadataAccountSecondaryKey = "connectorMetadataAccountSecondaryKey";
            public const string StorageServiceResource = "storageServiceResource";
            public const string OfferName = "offerName";
            public const string MaxNumberOfSupportedNodes = "maxNumberOfSupportedNodes";
            public const string MaxAllocationsPerStorageAccount = "maxAllocationsPerStorageAccount";
            public const string StorageAccountCount = "storageAccountCount";
            public const string ConnectorStorageAccounts = "connectorStorageAccounts";
            public const string UserProvidedConnectorStorageAccountsInfo = "userProvidedConnectorStorageAccountsInfo";
            public const string UserString = "User";
            public const string SystemString = "System";
            public const string ReplicationStatus = "ReplicationStatus";

            // ResourceIdResponse Resource
            public const string ResourceId = "resourceId";

            // Managed Service Identity (MSI)
            public const string ManagedServiceIdentityInfo = "managedServiceIdentityInfo";
            public const string IdentityName = "identity";
            public const string MsiSystemAssignedType = "SystemAssigned";
            public const string MsiNoneType = "None";
            public const string MsiUserAssignedType = "UserAssigned";
            public const string MsiSystemAndUserAssignedType = "SystemAssigned,UserAssigned";
            public const string DefaultIdentity = "defaultIdentity";
            public const string MsiDelegatedUserAssignedType = "DelegatedUserAssigned";
            public const string MsiDelegatedSystemAssignedType = "DelegatedSystemAssigned";

            // ServiceDocument Resource
            public const string AddressesLink = "addresses";
            public const string UserReplicationPolicy = "userReplicationPolicy";
            public const string UserConsistencyPolicy = "userConsistencyPolicy";
            public const string SystemReplicationPolicy = "systemReplicationPolicy";
            public const string ReadPolicy = "readPolicy";
            public const string QueryEngineConfiguration = "queryEngineConfiguration";
            public const string EnableMultipleWriteLocations = "enableMultipleWriteLocations";
            public const string CanEnableMultipleWriteLocations = "canEnableMultipleWriteLocations";
            public const string IsServerlessEnabled = "isServerlessEnabled";
            public const string EnableSnapshotAcrossDocumentStoreAndIndex = "enableSnapshotAcrossDocumentStoreAndIndex";
            public const string IsZoneRedundant = "isZoneRedundant";
            public const string EnableThroughputAutoScale = "enableThroughputAutoScale";
            public const string ReplicatorSequenceNumberToGLSNDeltaString = "replicatorSequenceNumberToGLSNDeltaString";
            public const string ReplicatorSequenceNumberToLLSNDeltaString = "replicatorSequenceNumberToLLSNDeltaString";
            public const string IsReplicatorSequenceNumberToLLSNDeltaSet = "isReplicatorSequenceNumberToLLSNDeltaSet";

            public const string LSN = "_lsn";
            public const string LLSN = "llsn";

            // Conflict Resource Properties
            public const string SourceResourceId = "resourceId";

            // ConflictResource property values
            public const string ResourceTypeDocument = "document";
            public const string ResourceTypeStoredProcedure = "storedProcedure";
            public const string ResourceTypeTrigger = "trigger";
            public const string ResourceTypeUserDefinedFunction = "userDefinedFunction";
            public const string ResourceTypeAttachment = "attachment";

            public const string ConflictLSN = "conflict_lsn";

            public const string OperationKindCreate = "create";
            public const string OperationKindPatch = "patch";
            public const string OperationKindReplace = "replace";
            public const string OperationKindDelete = "delete";

            // Conflict
            public const string Conflict = "conflict";
            public const string OperationType = "operationType";

            // Tombstone resource
            public const string TombstoneResourceType = "resourceType";
            public const string OwnerId = "ownerId";

            // Partition resource
            public const string CollectionResourceId = "collectionResourceId";
            public const string PartitionKeyRangeResourceId = "partitionKeyRangeResourceId";
            public const string WellKnownServiceUrl = "wellKnownServiceUrl";
            public const string LinkRelationType = "linkRelationType";
            public const string Region = "region";
            public const string GeoLinkIdentifier = "geoLinkIdentifier";

            // Progress resource
            public const string DatalossNumber = "datalossNumber";
            public const string ConfigurationNumber = "configurationNumber";

            // Replica resource
            public const string PartitionId = "partitionId";
            public const string SchemaVersion = "schemaVersion";
            public const string ReplicaId = "replicaId";
            public const string ReplicaRole = "replicaRole";
            public const string ReplicatorAddress = "replicatorAddress";
            public const string ReplicaStatus = "replicaStatus";
            public const string IsAvailableForWrites = "isAvailableForWrites";

            // Offer resource
            public const string OfferType = "offerType";
            public const string OfferResourceId = "offerResourceId";
            public const string OfferThroughput = "offerThroughput";
            public const string BackgroundTaskMaxAllowedThroughputPercent = "BackgroundTaskMaxAllowedThroughputPercent";
            public const string OfferIsRUPerMinuteThroughputEnabled = "offerIsRUPerMinuteThroughputEnabled";
            public const string OfferIsAutoScaleEnabled = "offerIsAutoScaleEnabled";
            public const string OfferVersion = "offerVersion";
            public const string OfferContent = "content";
            public const string CollectionThroughputInfo = "collectionThroughputInfo";
            public const string MinimumRuForCollection = "minimumRUForCollection";
            public const string NumPhysicalPartitions = "numPhysicalPartitions";
            public const string UserSpecifiedThroughput = "userSpecifiedThroughput";
            public const string OfferMinimumThroughputParameters = "offerMinimumThroughputParameters";
            public const string MaxConsumedStorageEverInKB = "maxConsumedStorageEverInKB";
            public const string MaxThroughputEverProvisioned = "maxThroughputEverProvisioned";
            public const string MaxCountOfSharedThroughputCollectionsEverCreated = "maxCountOfSharedThroughputCollectionsEverCreated";
            public const string OfferLastReplaceTimestamp = "offerLastReplaceTimestamp";
            public const string AutopilotSettings = "offerAutopilotSettings";

            public const string AutopilotTier = "tier";
            public const string AutopilotTargetTier = "targetTier";
            public const string AutopilotMaximumTierThroughput = "maximumTierThroughput";
            public const string AutopilotAutoUpgrade = "autoUpgrade";
            public const string EnableFreeTier = "enableFreeTier";

            public const string AutopilotMaxThroughput = "maxThroughput";
            public const string AutopilotTargetMaxThroughput = "targetMaxThroughput";
            public const string AutopilotAutoUpgradePolicy = "autoUpgradePolicy";
            public const string AutopilotThroughputPolicy = "throughputPolicy";
            public const string AutopilotThroughputPolicyIncrementPercent = "incrementPercent";

            public const string PhysicalPartitionThroughputInfo = "physicalPartitionThroughputInfo";
            public const string SourcePhysicalPartitionThroughputInfo = "sourcePhysicalPartitionThroughputInfo";
            public const string TargetPhysicalPartitionThroughputInfo = "targetPhysicalPartitionThroughputInfo";
            public const string ThroughputPolicy = "throughputPolicy";
            public const string PhysicalPartitionStorageInfoCollection = "physicalPartitionStorageInfoCollection";
            public const string PhysicalPartitionId = "id";
            public const string PhysicalPartitionIds = "physicalPartitionIds";
            public const string PhysicalPartitionThroughput = "throughput";
            public const string PhysicalPartitionStorageInKB = "storageInKB";

            public const string EnableAdaptiveRu = "enableAdaptiveRU";

            public const string EnablePartitionMerge = "enablePartitionMerge";

            public const string EnableBurstCapacity = "enableBurstCapacity";
            public const string EnableUserRateLimitingWithBursting = "enableUserRateLimitingWithBursting";
            public const string BlockOldSdkCallsForBurstCapacityEnabledAccount = "blockOldSdkCallsForBurstCapacityEnabledAccount";

            public const string IsDryRun = "isDryRun";

            // EnforceRUPerGB
            public const string EnforceRUPerGB = "enforceRUPerGB";
            public const string MinRUPerGB = "minRUPerGB";

            // Storage Management Store
            public const string EnableStorageAnalytics = "enableStorageAnalytics";
            public const string EnablePitrAndAnalyticsTogether = "enablePitrAndAnalyticsTogether";
            public const string AnalyticsStorageServiceNames = "analyticsStorageServiceNames";
            public const string LogStoreMetadataStorageAccountName = "logStoreMetadataStorageAccountName";
            public const string IsParallel = "isParallel";
            public const string SasAuthEnabled = "sasAuthEnabled";
            public const string AnalyticsSubDomain = ".analytics.cosmos.";
            public const string DocumentsSubDomain = ".documents";

            // GeoDrReplicaInformationResource
            public const string CurrentProgress = "currentProgress";
            public const string CatchupCapability = "catchupCapability";
            public const string PublicAddress = "publicAddress";

            // Operation resource
            public const string OperationName = "name";
            public const string OperationProperties = "properties";
            public const string OperationNextLink = "nextLink";
            public const string OperationDisplay = "display";
            public const string OperationDisplayProvider = "provider";
            public const string OperationDisplayResource = "resource";
            public const string OperationDisplayOperation = "operation";
            public const string OperationDisplayDescription = "description";

            public const string PartitionKey = "partitionKey";
            public const string PartitionKeyRangeId = "partitionKeyRangeId";
            public const string MinInclusiveEffectivePartitionKey = "minInclusiveEffectivePartitionKey";
            public const string MaxExclusiveEffectivePartitionKey = "maxExclusiveEffectivePartitionKey";
            public const string MinInclusive = "minInclusive";
            public const string MaxExclusive = "maxExclusive";
            public const string RidPrefix = "ridPrefix";
            public const string ThroughputFraction = "throughputFraction";
            public const string PartitionKeyRangeStatus = "status";
            public const string Parents = "parents";

            public const string NodeStatus = "NodeStatus";
            public const string NodeName = "NodeName";
            public const string HealthState = "HealthState";

            // CLient side encryption
            public const string WrappedDataEncryptionKey = "wrappedDataEncryptionKey";
            public const string EncryptionAlgorithmId = "encryptionAlgorithmId";
            public const string KeyWrapMetadata = "keyWrapMetadata";
            public const string KeyWrapMetadataName = "name";
            public const string KeyWrapMetadataType = "type";
            public const string KeyWrapMetadataValue = "value";
            public const string KeyWrapMetadataAlgorithm = "algorithm";
            public const string EncryptedInfo = "_ei";
            public const string DataEncryptionKeyRid = "_ek";
            public const string EncryptionFormatVersion = "_ef";
            public const string EncryptedData = "_ed";
            public const string ClientEncryptionKeyId = "clientEncryptionKeyId";
            public const string EncryptionType = "encryptionType";
            public const string EncryptionAlgorithm = "encryptionAlgorithm";
            public const string ClientEncryptionPolicy = "clientEncryptionPolicy";

            // ParitionKey Monitor
            public const string EnablePartitionKeyMonitor = "enablePartitionKeyMonitor";

            // Operation diagnostic logs
            public const string Origin = "origin";
            public const string AzureMonitorServiceSpecification = "serviceSpecification";
            public const string DiagnosticLogSpecifications = "logSpecifications";
            public const string DiagnosticLogsName = "name";
            public const string DiagnosticLogsDisplayName = "displayName";
            public const string BlobDuration = "blobDuration";

            // FederationPolicyOverrideResource
            public const string FederationPolicyOverride = "federationPolicyOverride";
            public const string MaxCapacityUnits = "maxCapacityUnits";
            public const string MaxDatabaseAccounts = "maxDatabaseAccounts";
            public const string MaxBindableServicesPercentOfFreeSpace = "maxBindableServicesPercentOfFreeSpace";
            public const string DisabledDatabaseAccountManager = "disabledDatabaseAccountManager";
            public const string EnableBsonSchemaOnNewAccounts = "enableBsonSchemaOnNewAccounts";

            // Metric Definitions
            public const string MetricSpecifications = "metricSpecifications";
            public const string DisplayDescription = "displayDescription";
            public const string AggregationType = "aggregationType";
            public const string LockAggregationType = "lockAggregationType";
            public const string SourceMdmAccount = "sourceMdmAccount";
            public const string SourceMdmNamespace = "sourceMdmNamespace";
            public const string FillGapWithZero = "fillGapWithZero";
            public const string Category = "category";
            public const string ResourceIdOverride = "resourceIdDimensionNameOverride";
            public const string Dimensions = "dimensions";
            public const string InternalName = "internalName";
            public const string IsHidden = "isHidden";
            public const string DefaultDimensionValues = "defaultDimensionValues";
            public const string Availabilities = "availabilities";
            public const string InternalMetricName = "internalMetricName";
            public const string SupportedTimeGrainTypes = "supportedTimeGrainTypes";
            public const string SupportedAggregationTypes = "supportedAggregationTypes";

            // IP Range resource
            public const string IpRangeFilter = "ipRangeFilter";
            public const string IpRules = "ipRules";
            public const string IpAddressOrRange = "ipAddressOrRange";

            // Property to check if point in time restore is enabled for a global database account
            public const string PitrEnabled = "pitrEnabled";
            public const string PitrSku = "pitrSku";
            public const string ContinuousBackupTier = "tier";
            public const string EnablePitrMigration = "enablePITRMigration";
            public const string EnableLogstoreHeadStartSequenceVector = "enableLogStoreHeadStartSequenceVector";
            public const string AllowSkippingOpLogFlushInRestore = "allowSkippingOpLogFlushInRestore";

            // Property to allow migration to analytical store
            public const string AllowCollectionMigrationToAnalyticalStore = "allowCollectionMigrationToAnalyticalStore";
            public const string AllowMongoCollectionMigrationToAnalyticalStore = "allowMongoCollectionMigrationToAnalyticalStore";

            // Property to enable storage analytics
            public const string EnableAnalyticalStorage = "enableAnalyticalStorage";

            //properties to enable MaterializedViews
            public const string EnableMaterializedViews = "enableMaterializedViews"; //at DB account level.

            // property to enable full fidelity change feed (change feed with retention from remote+local storage).
            public const string EnableFullFidelityChangeFeed = "enableFullFidelityChangeFeed";

            // Enable API type check
            public const string EnableApiTypeCheck = "enableApiTypeCheck";
            public const string EnabledApiTypesOverride = "enabledApiTypesOverride";

            public const string ForceDisableFailover = "forceDisableFailover";
            public const string EnableAutomaticFailover = "enableAutomaticFailover";
            public const string SkipGracefulFailoverAttempt = "skipGracefulFailoverAttempt";
            public const string ForceUngracefulFailover = "forceUngracefulFailover";

            // Location resource
            public const string IsEnabled = "isenabled";
            public const string FabricUri = "fabricUri";
            public const string ResourcePartitionKey = "resourcePartitionKey";
            public const string CanaryLocationSurffix = "euap";

            // Topology Resource
            public const string Topology = "topology";
            public const string AdjacencyList = "adjacencyList";
            public const string WriteRegion = "writeRegion";
            public const string GlobalConfigurationNumber = "globalConfigurationNumber";
            public const string WriteStatusRevokedSatelliteRegions = "writeStatusRevokedSatelliteRegions";
            public const string PreviousWriteRegion = "previousWriteRegion";
            public const string NextWriteRegion = "nextWriteRegion";
            public const string ReadStatusRevoked = "readStatusRevoked";

            // Capabilities Resource
            public const string Capabilities = "capabilities";
            public const string DefaultCapabilities = "defaultCapabilities";
            public const string CapabilityVisibilityPolicies = "capabilityVisibilityPolicies";
            public const string NamingServiceSettings = "namingServiceSettings";
            public const string IsEnabledForAll = "isEnabledForAll";
            public const string IsDefault = "isDefault";
            public const string Key = "key";
            public const string IsCapabilityRingFenced = "isCapabilityRingFenced";
            public const string IsEnabledForAllToOptIn = "isEnabledForAllToOptIn";
            public const string TargetedFederationTypes = "targetedFederationTypes";

            // PolicyStore
            public const string ConfigValueByKey = "configValueByKey";
            public const string NameBasedCollectionUri = "nameBasedCollectionUri";
            public const string UsePolicyStore = "usePolicyStore";
            public const string UsePolicyStoreRuntime = "usePolicyStoreRuntime";
            public const string PolicyStoreVersion = "policyStoreVersion";

            // System store properties
            public const string AccountEndpoint = "AccountEndpoint";
            public const string EncryptedAccountKey = "EncryptedAccountKey";

            // PolicyStoreConnectionInfo
            public const string PolicyStoreConnectionInfoNameBasedCollectionUri = "NameBasedCollectionUri";

            // BillingStoreConnectionInfo
            public const string BillingStoreConnectionInfoAccountEndpoint = "AccountEndpoint";
            public const string BillingStoreConnectionInfoEncryptedAccountKey = "EncryptedAccountKey";
            public const string EnableBackupPolicyBilling = "EnableBackupPolicyBilling";
            public const string EnableBackupRedundancyBilling = "EnableBackupRedundancyBilling";

            // ConfigurationStoreConnectionInfo
            public const string ConfigurationStoreConnectionInfoAccountEndpoint = "AccountEndpoint";
            public const string ConfigurationStoreConnectionInfoEncryptedAccountKey = "EncryptedAccountKey";
            public const string ConfigurationStoreConnectionInfoFederationCollectionUri = "FederationCollectionUri";
            public const string ConfigurationStoreConnectionInfoDatabaseAccountCollectionUri = "DatabaseAccountCollectionUri";
            public const string ConfigurationStoreConnectionInfoRegionalCollectionUri = "RegionalCollectionUri";

            // ConfigurationLevel
            public const string ConfigurationLevel = "configurationLevel";

            // SkipFederationEntityUpdate
            public const string SkipFederationEntityUpdate = "skipFederationEntityUpdate";

            // TargetOnlyNamingService
            public const string TargetOnlyNamingService = "targetOnlyNamingService";

            // Subscription Properties
            public const string SubscriptionTenantId = "tenantId";
            public const string SubscriptionLocationPlacementId = "locationPlacementId";
            public const string SubscriptionQuotaId = "quotaId";
            public const string SubscriptionAddionalProperties = "additionalProperties";

            // Schema resource
            public const string Schema = "schema";

            // PartitionedQueryExecutionInfo
            public const string PartitionedQueryExecutionInfoVersion = "partitionedQueryExecutionInfoVersion";
            public const string QueryInfo = "queryInfo";
            public const string QueryRanges = "queryRanges";

            // Arm resource type
            public const string DatabaseAccounts = "databaseAccounts";

            // SchemaDiscoveryPolicy
            public const string SchemaDiscoveryPolicy = "schemaDiscoveryPolicy";
            public const string SchemaBuilderMode = "mode";

            // Global database account resource configuration overrides
            public const string EnableBsonSchema = "enableBsonSchema";
            public const string EnableAdditiveSchemaForAnalytics = "enableAdditiveSchemaForAnalytics";

            // Billing
            public const string EnableV2Billing = "enableV2Billing";
            public const string CustomProperties = "CustomProperties";
            public const string StreamName = "StreamName";
            public const string ResourceLocation = "ResourceLocation";
            public const string EventTime = "EventTime";
            public const string ResourceTags = "ResourceTags";

            // PartitionStatistics
            public const string Statistics = "statistics";
            public const string SizeInKB = "sizeInKB";
            public const string CompressedSizeInKB = "compressedSizeInKB";
            public const string DocumentCount = "documentCount";
            public const string SampledDistinctPartitionKeyCount = "sampledDistinctPartitionKeyCount";
            public const string PartitionKeys = "partitionKeys";

            // Partition quota
            public const string AccountStorageQuotaInGB = "databaseAccountStorageQuotaInGB";

            // PartitionKeyStatistic
            public const string Percentage = "percentage";
            public const string EnablePartitionKeyStatsOptimization = "enablePartitionKeyStatsOptimization";

            public const string PartitionKeyDefinitionVersion = "version";

            // Federation pinning
            public const string EnforcePlacementHintConstraintOnPartitionAllocation = "enforcePlacementHintConstraintOnPartitionAllocation";
            public const string CanUsePolicyStore = "canUsePolicyStore";

            // SubscriptionsQuota resource
            public const string MaxDatabaseAccountCount = "maxDatabaseAccountCount";
            public const string MaxRegionsPerGlobalDatabaseAccount = "maxRegionsPerGlobalDatabaseAccount";
            public const string DisableManualFailoverThrottling = "disableManualFailoverThrottling";
            public const string DisableRemoveRegionThrottling = "disableRemoveRegionThrottling";
            public const string AnalyticsStorageAccountCount = "analyticsStorageAccountCount";
            public const string LibrariesFileShareQuotaInGB = "librariesFileShareQuotaInGB";

            // Policy Overrides
            public const string DefaultSubscriptionPolicySet = "defaultSubscriptionPolicySet";
            public const string SubscriptionPolicySetByLocation = "subscriptionPolicySetByLocation";
            public const string PlacementPolicy = "placementPolicy";
            public const string SubscriptionPolicy = "subscriptionPolicy";
            public const string LocationPolicySettings = "locationPolicySettings";
            public const string DefaultLocationPolicySet = "defaultLocationPolicySet";
            public const string LocationPolicySetBySubscriptionKind = "locationPolicySetBySubscriptionKind";
            public const string SubscriptionPolicySettings = "subscriptionPolicySettings";
            public const string CapabilityPolicySettings = "capabilityPolicySettings";

            // Location Policy
            public const string LocationVisibilityPolicy = "locationVisibilityPolicy";
            public const string IsVisible = "isVisible";

            // Virtual Network Filter/Acls
            // Customer Facing
            public const string IsVirtualNetworkFilterEnabled = "isVirtualNetworkFilterEnabled";

            // Backend
            public const string AccountVNETFilterEnabled = "accountVNETFilterEnabled";
            public const string VirtualNetworkArmUrl = "virtualNetworkArmUrl";
            public const string VirtualNetworkResourceEntries = "virtualNetworkResourceEntries";
            public const string VirtualNetworkResourceId = "virtualNetworkResourceId";
            public const string VirtualNetworkResourceIds = "virtualNetworkResourceIds";
            public const string VNetDatabaseAccountEntries = "vNetDatabaseAccountEntry";
            public const string VirtualNetworkTrafficTags = "vnetFilter";
            public const string VNetETag = "etag";
            public const string PrivateEndpointProxyETag = "etag";
            public const string VirtualNetworkRules = "virtualNetworkRules";
            public const string EnabledApiTypes = "EnabledApiTypes";
            public const string VirtualNetworkPrivateIpConfig = "vnetPrivateIps";
            public const string NspProfileProxyResources = "nspProfileProxyResources";
            public const string NspAssociationProxyResource = "nspAssociationProxyResource";
            public const string EnableNetworkSecurityPerimeter = "enableNetworkSecurityPerimeter";
            public const string PrototypeAdditionalEndpointsForPrivateEndpoint = "prototypeAdditionalEndpointsForPrivateEndpoint";

            // VNET/Subnet Resource(Network Resource Provider)
            public const string IgnoreMissingVNetServiceEndpoint = "ignoreMissingVNetServiceEndpoint";
            public const string SubnetTrafficTag = "subnetTrafficTag";
            public const string Owner = "owner";
            public const string VNetServiceAssociationLinks = "vNetServiceAssociationLinks";
            public const string VNetTrafficTag = "vNetTrafficTag";
            public const string VirtualNetworkAcled = "virtualNetworkAcled";
            public const string VirtualNetworkResourceGuid = "virtualNetworkResourceGuid";
            public const string VirtualNetworkLocation = "virtualNetworkLocation";
            public const string PrimaryLocations = "primaryLocations";
            public const string AcledSubscriptions = "acledSubscriptions";
            public const string AcledSubnets = "acledSubnets";
            public const string RetryAfter = "retryAfter";
            public const string OperationPollingUri = "operationPollingUri";
            public const string OperationPollingKind = "operationPollingKind";
            public const string PublicNetworkAccess = "publicNetworkAccess";
            public const string UseSubnetDatabaseAccountEntries = "useSubnetDatabaseAccountEntries";
            public const string UseRegionalVNet = "userRegionalVNet";

            // Auto Migration
            public const string EnableFederationDecommission = "enableFederationDecommission2";
            public const string AutoMigrationScheduleIntervalInSeconds = "autoMigrationScheduleIntervalInSeconds2";
            public const string MasterPartitionAutoMigrationProbability = "masterPartitionAutoMigrationProbability2";
            public const string ServerPartitionAutoMigrationProbability = "serverPartitionAutoMigrationProbability2";

            // Diagnostic Settings
            public const string StorageAccountId = "storageAccountId";
            public const string WorkspaceId = "workspaceId";
            public const string EventHubAuthorizationRuleId = "eventHubAuthorizationRuleId";
            public const string Enabled = "enabled";
            public const string Days = "days";
            public const string RetentionPolicy = "retentionPolicy";
            public const string Metrics = "metrics";
            public const string Logs = "logs";
            public const string CategoryGroup = "categoryGroup";

            // Atp(advanced threat protection) Settings
            public const string IsAtpEnabled = "isEnabled";

            // FullTextQuerySettings for plain text logging
            public const string IsFullTextQueryEnabled = "isEnabled";

            // ResourceLimits
            public const string EnableExtendedResourceLimit = "enableExtendedResourceLimit";
            public const string ExtendedResourceNameLimitInBytes = "extendedResourceNameLimitInBytes";
            public const string ExtendedResourceContentLimitInKB = "extendedResourceContentLimitInKB";
            public const string MaxResourceSize = "maxResourceSize";
            public const string MaxBatchRequestBodySize = "maxBatchRequestBodySize";
            public const string MaxResponseMessageSize = "maxResponseMessageSize";
            public const string AnalyzedTermChargeEnabled = "analyzedTermChargeEnabled ";
            public const string EnableWriteConflictCharge = "enableWriteConflictCharge";
            public const string ResourceContentBufferThreshold = "resourceContentBufferThreshold";
            public const string EnableExtendedResourceLimitForNewCollections = "enableExtendedResourceLimitForNewCollections";
            public const string DefaultExtendedCollectionChildResourceContentLimitInKB = "defaultExtendedCollectionChildResourceContentLimitInKB";
            public const string MaxScriptInputSize = "maxScriptInputSize";
            public const string MaxBatchTransactionSizeInBytes = "maxBatchTransactionSizeInBytes";
            public const string MaxScriptTransactionSize = "maxScriptTransactionSize";
            public const string ReplicationBatchMaxMessageSizeInBytes = "replicationBatchMaxMessageSizeInBytes";
            public const string TriggeredMasterBackupDuringAccountDelete = "triggeredMasterBackupDuringAccountDelete";
            public const string MaxReadFeedResponseMessageSize = "maxReadFeedResponseMessageSize";
            public const string EnableLargeDocumentSupport = "enableLargeDocumentSupport";

            // Resource Governance Settings
            public const string MaxFeasibleRequestChargeInSeconds = "MaxFeasibleRequestChargeInSeconds";
            public const string ReplicationChargeEnabled = "replicationChargeEnabled";

            // Multi-Region Strong
            public const string BypassMultiRegionStrongVersionCheck = "bypassMultiRegionStrongVersionCheck";
            public const string XPCatchupConfigurationEnabled = "xpCatchupConfigurationEnabled";

            // LogStore restore
            public const string IsRemoteStoreRestoreEnabled = "remoteStoreRestoreEnabled";
            public const string RetentionPeriodInSeconds = "retentionPeriodInSeconds";

            // Remove stale backup blobs
            public const string BlobNamePrefix = "blobNamePrefix";
            public const string BlobPartitionLevel = "blobPartitionLevel";
            public const string DryRun = "dryRun";
            public const string UseCustomRetentionPeriod = "UseCustomRetentionPeriod";
            public const string CustomRetentionPeriodInMins = "CustomRetentionPeriodInMins";

            // Shared throughput offer specific
            public const string DisallowOfferOnSharedThroughputCollection = "disallowOfferOnSharedThroughputCollection";
            public const string AllowOnlyPartitionedCollectionsForSharedOffer = "allowOnlyPartitionedCollectionsForSharedThroughputOffer";
            public const string RestrictDatabaseOfferContainerCount = "restrictDatabaseOfferContainerCount";
            public const string MaxSharedOfferDatabaseCount = "maxSharedOfferDatabaseCount";
            public const string MinRUsPerSharedThroughputCollection = "minRUsPerSharedThroughputCollection";

            // Conflict resolution policy
            public const string ConflictResolutionPolicy = "conflictResolutionPolicy";
            public const string Mode = "mode";
            public const string ConflictResolutionPath = "conflictResolutionPath";
            public const string ConflictResolutionProcedure = "conflictResolutionProcedure";

            public const string Progress = "progress";
            public const string ReservedServicesInfo = "reservedServicesInfo";

            // Update Federation Is Dirty Status Request
            public const string RoleInstanceCounts = "RoleInstanceCounts";
            public const string RoleName = "RoleName";
            public const string InstanceCount = "InstanceCount";

            // Federation Service Extension
            public const string ExtensionName = "ExtensionName";
            public const string ExtensionVersion = "ExtensionVersion";

            // CORS Rules
            public const string Cors = "cors";
            public const string AllowedOrigins = "allowedOrigins";
            public const string AllowedMethods = "allowedMethods";
            public const string AllowedHeaders = "allowedHeaders";
            public const string ExposedHeaders = "exposedHeaders";
            public const string MaxAgeInSeconds = "maxAgeInSeconds";

            // Certificates for SSL authentication
            public const string PrimaryClientCertificatePemBytes = "primaryClientCertificatePemBytes";
            public const string SecondaryClientCertificatePemBytes = "secondaryClientCertificatePemBytes";

            // Adds lsn property to document content
            public const string EnableLsnInDocumentContent = "enableLsnInDocumentContent";

            // Max content per collection In GB
            public const string MaxContentPerCollectionInGB = "maxContentPerCollection";

            // Visibility resource
            public const string SkipRepublishConfigFromTargetFederation = "skipRepublishConfigFromTargetFederation";
            public const string MaxDegreeOfParallelismForPublishingFederationKeys = "maxDegreeOfParallelismForPublishingFederationKeys";

            // Remove federation visibility resource
            public const string ValidationOnly = "validationOnly";
            public const string EnforceOrphanServiceCheck = "enforceOrphanServiceCheck";
            public const string ResetPoolCounters = "resetPoolCounters";

            public const string ProxyName = "proxyName";
            public const string GroupId = "groupId";
            public const string ConnectionDetails = "connectionDetails";
            public const string GroupIds = "groupIds";
            public const string InternalFqdn = "internalFqdn";
            public const string CustomerVisibleFqdns = "customerVisibleFqdns";
            public const string RequiredMembers = "requiredMembers";
            public const string RequiredZoneNames = "requiredZoneNames";
            public const string ManualPrivateLinkServiceConnections = "manualPrivateLinkServiceConnections";
            public const string PrivateLinkServiceConnections = "privateLinkServiceConnections";
            public const string PrivateLinkServiceProxies = "privateLinkServiceProxies";
            public const string RemotePrivateEndpoint = "remotePrivateEndpoint";
            public const string MemberName = "memberName";
            public const string BatchNotifications = "batchNotifications";
            public const string AccountSourceResourceId = "sourceResourceId";
            public const string TargetResourceGroupId = "targetResourceGroupId";
            public const string GroupConnectivityInformation = "groupConnectivityInformation";
            public const string RemotePrivateLinkServiceConnectionState = "remotePrivateLinkServiceConnectionState";
            public const string RemotePrivateEndpointConnection = "remotePrivateEndpointConnection";
            public const string PrivateEndpointConnectionId = "privateEndpointConnectionId";
            public const string ActionsRequired = "actionsRequired";
            public const string RequestMessage = "requestMessage";
            public const string LinkIdentifier = "linkIdentifier";
            public const string PrivateLinkServiceArmRegion = "privateLinkServiceArmRegion";
            public const string PrivateIpAddress = "privateIpAddress";
            public const string PrivateIpConfigGroups = "privateIpConfigGroups";
            public const string IsLastNrpPutRequestManualApprovalWorkflow = "isLastNrpPutRequestManualApprovalWorkflow";
            public const string PrivateLinkServiceConnectionName = "privateLinkServiceConnectionName";
            public const string RedirectMapId = "redirectMapId";
            public const string ImmutableSubscriptionId = "immutableSubscriptionId";
            public const string ImmutableResourceId = "immutableResourceId";
            public const string AccountPrivateEndpointConnectionEnabled = "accountPrivateEndpointConnectionEnabled";
            public const string AccountPrivateEndpointDnsZoneEnabled = "accountPrivateEndpointDnsZoneEnabled";
            public const string PrivateIpConfigs = "privateIpConfigs";
            public const string PrivateEndpoint = "privateEndpoint";
            public const string PrivateLinkServiceConnectionState = "privateLinkServiceConnectionState";
            public const string PrivateEndpointArmUrl = "privateEndpointArmUrl";
            public const string StoragePrivateEndpointConnections = "storagePrivateEndpointConnections";
            public const string PrivateLinkServiceProxyName = "privateLinkServiceProxyName";
            public const string PrivateEndpointConnections = "privateEndpointConnections";
            public const string UpdateIpRangeFilter = "updateIpRangeFilter";
            public const string UpdateVirtualNetworkResources = "updateVirtualNetworkResources";
            public const string UpdatePrivateEndpointConnections = "updatePrivateEndpointConnections";
            public const string ExplicitDnsSettings = "explicitDnsSettings";
            public const string ARecord = "aRecord";
            public const string DnsZoneName = "dnsZoneName";
            public const string ArmSubscriptionId = "armSubscriptionId";
            public const string ChildrenNames = "childrenNames";
            public const string ParentName = "parentName";
            public const string IsActive = "isActive";
            public const string IsRegional = "isRegional";
            public const string MapEntrySize = "mapEntrySize";
            public const string FederationMaps = "federationMaps";
            public const string FederationDns = "federationDns";
            public const string FederationVip = "federationVip";
            public const string Entries = "entries";
            public const string AllowEntryDelete = "allowEntryDelete";
            public const string DeletedFederations = "deletedFederations";
            public const string StartPublicPort = "startPublicPort";
            public const string StartPrivatePort = "startPrivatePort";
            public const string ServiceName = "serviceName";
            public const string PlatformId = "platformId";
            public const string VirtualPortBlockSize = "virtualPortBlockSize";
            public const string LookUpEntries = "lookupEntries";
            public const string RingPublicVipAddress = "ringPublicVipAddress";
            public const string StartInstancePort = "startInstancePort";
            public const string EndInstancePort = "endInstancePort";
            public const string StartVirtualPort = "startVirtualPort";
            public const string EndVirtualPort = "endVirtualPort";
            public const string PublishToNrp = "publishToNrp";
            public const string VnetMapPropagationWaitDurationInMinutes = "vnetMapPropagationWaitDurationInMinutes";
            public const string Map = "map";
            public const string IsSqlEndpointSwapped = "isSqlEndpointSwapped";
            public const string IsSqlEndpointDefaultProvisionedOnCompute = "isSqlEndpointDefaultProvisionedOnCompute";
            public const string ComputeFederationProcess = "computeFederationProcess";
            public const string DatabaseServicesInfo = "DatabaseServicesInfo";
            public const string RedirectMapVersion = "version";
            public const string IsOwnedByExternalProvider = "isOwnedByExternalProvider";
            public const string ConnectionInformation = "connectionInformation";
            public const string CrossSubRegionMigrationInProgress = "CrossSubRegionMigrationInProgress";

            // Data Plane Operation Policy
            public const string DisableKeyBasedMetadataWriteAccess = "disableKeyBasedMetadataWriteAccess";
            public const string DisableKeyBasedMetadataReadAccess = "disableKeyBasedMetadataReadAccess";

            //ControlPlane Metric and Diagnostic logs
            public const int DiagnosticLogPropertyLengthLimit = 2000;
            public const string EventStartSuffix = "Start";
            public const string EventCompleteSuffix = "Complete";
            public const string AddRegionEventGroupName = "RegionAdd";
            public const string RemoveRegionEventGroupName = "RegionRemove";
            public const string DeleteAccountEventGroupName = "AccountDelete";
            public const string RegionFailoverEventGroupName = "RegionFailover";
            public const string CreateAccountEventGroupName = "AccountCreate";
            public const string UpdateAccountEventGroupName = "AccountUpdate";
            public const string UpdateAccountBackUpPolicyEventGroupName = "AccountBackUpPolicyUpdate";
            public const string DeleteVNETEventGroupName = "VirtualNetworkDelete";
            public const string UpdateDiagnosticLogEventGroupName = "DiagnosticLogUpdate";
            public const string SqlRoleDefinitionCreate = "SqlRoleDefinitionCreate";
            public const string SqlRoleDefinitionReplace = "SqlRoleDefinitionReplace";
            public const string SqlRoleDefinitionDelete = "SqlRoleDefinitionDelete";
            public const string SqlRoleAssignmentCreate = "SqlRoleAssignmentCreate";
            public const string SqlRoleAssignmentReplace = "SqlRoleAssignmentReplace";
            public const string SqlRoleAssignmentDelete = "SqlRoleAssignmentDelete";

            public const string MongoRoleDefinitionCreate = "MongoRoleDefinitionCreate";
            public const string Replace = "AuthPolicyElementReplace";
            public const string MongoRoleDefinitionDelete = "MongoRoleDefinitionDelete";

            public const string CassandraRoleDefinitionCreate = "CassandraRoleDefinitionCreate";
            public const string CassandraRoleDefinitionReplace = "CassandraRoleDefinitionReplace";
            public const string CassandraRoleDefinitionDelete = "CassandraRoleDefinitionDelete";

            // Diagnostics settings
            public const string EnableControlPlaneRequestsTrace = "enableControlPlaneRequestsTrace";
            public const string AtpEnableControlPlaneRequestsTrace = "atpEnableControlPlaneRequestsTrace";

            public const string OwnerResourceId = "ownerResourceId";

            public const string ParentResourceId = "parentResourceId";

            //Notebook settings
            public const string NotebookStorageAllocationInfo = "notebookStorageAllocationInfo";

            // Spark settings
            public const string SparkStorageAllocationInfo = "sparkStorageAllocationInfo";

            // Throughput split settings
            public const string ThroughputSplitInfo = "ThroughputSplitInfo";

            //CosmosStoreBlobProperties
            public const string CacheControl = "cacheControl";
            public const string ContentDisposition = "contentDisposition";
            public const string ContentEncoding = "contentEncoding";
            public const string ContentLanguage = "contentLanguage";
            public const string ContentMD5 = "contentMD5";
            public const string Length = "length";

            //CosmosBlob
            public const string Metadata = "metadata";
            public const string BlobProperties = "blobProperties";

            // ARM Certificates
            public const string ClientCertificates = "clientCertificates";
            public const string Thumbprint = "thumbprint";
            public const string NotBefore = "notBefore";
            public const string NotAfter = "notAfter";
            public const string Certificate = "certificate";

            public const string IsDiskReceiverEnabled = "isDiskReceiverEnabled";

            // Service Association Link
            public const string LinkedResourceType = "linkedResourceType";
            public const string Link = "link";
            public const string AllowDelete = "allowDelete";
            public const string Details = "Details";
            public const string ServiceAssociationLinkETag = "etag";
            public const string PrimaryContextId = "primaryContextId";
            public const string SecondaryContextId = "secondaryContextId";
            public const string PrimaryContextRequestId = "primaryContextRequestId";
            public const string SecondaryContextRequestId = "secondaryContextRequestId";

            // GremlinV2 Properties.
            public const string GremlinProperties = "gremlinProperties";
            public const string GraphApiServerStartupConfigFileName = "graphapi.startupConfig";

            // Mongo Properties
            public const string GlobalMongoProperties = "globalMongoProperties";
            public const string ApiProperties = "apiProperties";

            // AAD Authentication
            public const string TrustedAadTenants = "trustedAadTenants";
            public const string NetworkAclBypass = "networkAclBypass";
            public const string NetworkAclBypassResourceIds = "networkAclBypassResourceIds";
            public const string DisableLocalAuth = "disableLocalAuth";

            //Logging properties
            public const string DiagnosticLogSettings = "diagnosticLogSettings";
            public const string EnableFullTextQuery = "enableFullTextQuery";

            // Restorable database accounts
            public const string CreationTime = "creationTime";
            public const string DeletionTime = "deletionTime";
            public const string OldestRestorableTime = "oldestRestorableTime";
            public const string AccountName = "accountName";
            public const string ApiType = "apiType";
            public const string RestorableLocations = "restorableLocations";

            public const string CreationTimeInUtc = "creationTimeInUtc";
            public const string DeletionTimeInUtc = "deletionTimeInUtc";
            public const string OldestRestorableTimeInUtc = "oldestRestorableTimeInUtc";

            // SystemData
            public const string SystemData = "systemData";

            // DeletedGlobalDatabaseAccount resource
            public const string DeletedRegionalDatabaseAccounts = "regionalDeletedDatabaseAccounts";
            // Compute MongoClient for RP
            public const string DisableMongoClientWorkflows = "disableMongoClientWorkflows";

            // Enable customer managed key
            public const string ByokStatus = "byokStatus";
            public const string ByokConfig = "byokConfig";
            public const string EnableBYOKOnExistingCollections = "enableBYOKOnExistingCollections";
            public const string DisableByokReEncryptor = "disableByokReEncryptor";
            public const string DataEncryptionState = "dataEncryptionState";

            // LocationResource
            public const string DeploymentBatch = "deploymentBatch";
            public const string HealthServiceSubscriptionId = "healthServiceSubscriptionId";
            public const string FederationActiveDirectoryTenantId = "federationActiveDirectoryTenantId";
            public const string FederationActiveDirectoryClientId = "federationActiveDirectoryClientId";
            public const string Longitude = "longitude";
            public const string Latitude = "latitude";
            public const string ShortId = "shortId";
            public const string PublicName = "publicName";
            public const string LongName = "longName";
            public const string ServicingLocationPublicName = "servicingLocationPublicName";
            public const string HealthServiceName = "healthServiceName";
            public const string SupportsAvailabilityZone = "supportsAvailabilityZone";
            public const string AvailabilityZonesReady = "availabilityZonesReady";
            public const string ActiveSubRegionId = "activeSubRegionId";
            public const string ActiveAvailabilityZoneSubRegionId = "activeAvailabilityZoneSubRegionId";
            public const string IsResidencyRestricted = "isResidencyRestricted";
            public const string RegionalRPLocationForBuildoutStoreFailover = "regionalRPLocationForBuildoutStoreFailover";
            public const string ManagementStoreName = "managementStoreName";
            public const string MgmtStoreSubscriptionId = "mgmtStoreSubscriptionId";
            public const string MgmtStoreResourceGroup = "mgmtStoreResourceGroup";
            public const string DisallowedSubscriptionOfferKinds = "disallowedSubscriptionOfferKinds";

            // This is read by Compute.
            public const string EnablePartitionLevelFailover = "enablePartitionLevelFailover";
            // This is read by Backend.
            public const string EnablePerPartitionAutomaticFailover = "enablePerPartitionAutomaticFailover";
            public const string EnableFailoverManager = "enableFailoverManager";

            // Capacity settings properties
            public const string Capacity = "capacity";
            public const string TotalThroughputLimit = "totalThroughputLimit";
            public const string TotalThroughputLimitBackendName = "currentThroughputCap";

            // Mongo Partial Unique Indexes properties
            public const string EnableConditionalUniqueIndex = "enableConditionalUniqueIndex";
            public const string EnableUniqueIndexReIndexing = "enableUniqueIndexReIndexing";
            public const string GenerateUniqueIndexTermsIfUniqueKeyPolicyDiffer = "generateUniqueIndexTermsIfUniqueKeyPolicyDiffer";
            public const string EnableUseLatestCollectionContent = "enableUseLatestCollectionContent";
            public const string AllowUniqueIndexModificationOnMaster = "allowUniqueIndexModificationOnMaster";
            public const string AllowUniqueIndexModificationWhenDataExists = "allowUniqueIndexModificationWhenDataExists";

            // UpgradeControlRule
            public const string RuleId = "ruleId";
            public const string RuleType = "ruleType";
            public const string RuleScope = "ruleScope";
            public const string ActivePeriod = "activePeriod";
            public const string Justification = "justification";
            public const string AdditionalProperties = "additionalProperties";

            // Purview-related properties
            public const string PurviewEnabled = "purviewEnabled";

            // TLS
            // New values for Self-Service Tls feature.
            public const string MinimalTlsVersion = "minimumAllowedTLSProtocol";
            public const string MinimalTlsVersionDisplay = "minimalTlsVersion";
            // Old deprecated value.
            public const string MinimumTransportLayerSecurityLevel = "minimumTransportLayerSecurityLevel";

            // LogStore Storage Account Rollover Timestamp
            public const string EarliestNextLogStoreStorageAccountKeyRolloverTime = "earliestNextLogStoreStorageAccountKeyRolloverTime";

            // RowRewriter properties
            public const string RowRewriterIdleIntervalInSeconds = "rowRewriterIdleIntervalInSeconds";
            public const string DisableRowRewriter = "disableRowRewriter";

            // Client Configuration Properties
            public const string ClientTelemetryConfiguration = "clientTelemetryConfiguration";
            public const string ClientTelemetryEnabled = "isEnabled";
            public const string ClientTelemetryEndpoint = "endpoint";
            public const string EnableClientTelemetry = "enableClientTelemetry";

            public static class FederationOperations
            {
                public const string IsCapOperation = "isCapOperation";
            }
        }

        public static class FederationCapActions
        {
            public const string Cap = "Cap";
            public const string Uncap = "Uncap";
        }

        public static class DocumentResourceExtendedProperties
        {
            public const string Tags = "Tags";
            public const string ResourceGroupName = "ResourceGroupName";
            public const string SkipDNSUpdateOnFailureDuringFailover = "SkipDNSUpdateOnFailureDuringFailover";
        }

        public static class SnapshotProperties
        {
            public const string GeoLinkToPKRangeRid = "geoLinkIdToPKRangeRid";
            public const string PartitionKeyRangeResourceIds = "partitionKeyRangeResourceIds";
            public const string PartitionKeyRanges = "partitionKeyRanges";
            public const string ClientEncryptionKeyResources = "clientEncryptionKeyResources";
            public const string CollectionContent = "collectionContent";
            public const string DatabaseContent = "databaseContent";
            public const string SnapshotTimestamp = "snapshotTimestamp";
            public const string EventTimestamp = "eventTimestamp";
            public const string DataDirectories = "dataDirectories";
            public const string MetadataDirectory = "metadataDirectory";
            public const string OperationType = "operationType";
            public const string RestoredPartitionInfo = "restoredPartitionInfo";
            public const string CollectionToSnapshotMap = "collectionToSnapshotMap";
            public const string PartitionKeyRangeList = "partitionKeyRangeList";
            public const string DocumentCollection = "documentCollection";
            public const string Database = "database";
            public const string OfferContent = "offerContent";
            public const string Container = "container";
            public const string Table = "table";
            public const string Keyspace = "keyspace";
            public const string LSN = "lsn";
        }

        public static class RestoreMetadataResourceProperties
        {
            // RestoreMetadata resource
            public const string RId = "_rid";
            public const string LSN = "lsn";
            public const string CollectionDeletionTimestamp = "_ts";
            public const string DatabaseName = "databaseName";
            public const string CollectionName = "collectionName";
            public const string CollectionResourceId = "collectionResourceId";
            public const string PartitionKeyRangeContent = "partitionKeyRangeContent";
            public const string CollectionContent = "collectionContent";
            public const string OfferContent = "offerContent";
            public const string CollectionSecurityIdentifier = "collectionSecurityIdentifier";
            public const string CollectionCreationTimestamp = "creationTimestamp";
            public const string RemoteStoreType = "remoteStorageType";
        }

        public static class CollectionRestoreParamsProperties
        {
            public const string Version = "Version";
            public const string SourcePartitionKeyRangeId = "SourcePartitionKeyRangeId";
            public const string SourcePartitionKeyRangeRid = "SourcePartitionKeyRangeRid";
            public const string SourceSecurityIdentifier = "SourceSecurityId";
            public const string RestorePointInTime = "RestorePointInTime";
            public const string PartitionCount = "PartitionCount";
            public const string RestoreState = "RestoreState";
        }

        public static class InternalIndexingProperties
        {
            public const string PropertyName = "internalIndexingProperties";
            public const string LogicalIndexVersion = "logicalIndexVersion";
            public const string IndexEncodingOptions = "indexEncodingOptions";
            public const string EnableIndexingFullFidelity = "enableIndexingFullFidelity";
            public const string IndexUniquifierId = "indexUniquifierId";
            public const string CellExpiryIndexingMethod = "cellExpiryIndexingMethod";
            public const string EnableIndexingEffectivePartitionKey = "enableIndexingEffectivePartitionKey";
        }

        public static class InternalStoreProperties
        {
            public const string PropertyName = "internalStoreProperties";
            public const string AllowEnableLocalStoreCompression = "allowEnableLocalStoreCompression";
        }

        public static class TypeSystemPolicy
        {
            public const string PropertyName = "typeSystemPolicy";
            public const string TypeSystem = "typeSystem";
            public const string CosmosCore = "CosmosCore";
            public const string Cql = "Cql";
            public const string Bson = "Bson";
        }

        public static class UpgradeTypes
        {
            public const string ClusterManifest = "Fabric";
            public const string Package = "Package";
            public const string Application = "App";
            public const string GrowShrink = "GrowShrink";
        }

        public static class TracesConstants
        {
            // Monitoring properties
            public static readonly string TraceContainerName = "trace";
        }

        public static class EncryptionScopeProperties
        {
            public const string EncryptionScopeId = "encryptionScopeId";
            public const string EncryptionScope = "encryptionScope";
            public const string Name = "name";
            public const string ManagedServiceIdentityInfoV2 = "managedServiceIdentityInfoV2";
            public const string CMKMetadataList = "cmkMetadataList";
            public const string CollectionFanoutCompleted = "collectionFanoutCompleted";

            public static class MsiProperties
            {
                public const string Type = "type";
                public const string MsiIdentityUrl = "msiIdentityUrl";
                public const string ImplicitIdentity = "implicitIdentity";
                public const string ExplicitIdentities = "explicitIdentities";
                public const string DelegatedIdentities = "delegatedIdentities";

                public static class DelegatedIdentityProperties
                {
                    public const string SourceInternalId = "sourceInternalId";
                    public const string SourceResourceId = "sourceResourceId";
                    public const string SourceTenantId = "sourceTenantId";
                    public const string DelegationId = "delegationId";
                    public const string DelegationUrl = "delegationUrl";
                    public const string UpdateAction = "updateAction";
                    public const string WildcardAction = "wildcardAction";
                }

                public static class IdentityProperties
                {
                    public const string ClientId = "clientId";
                    public const string ClientSecretEncrypted = "clientSecretEncrypted";
                    public const string ClientSecretUrl = "clientSecretUrl";
                    public const string TenantId = "tenantId";
                    public const string ObjectId = "objectId";
                    public const string ResourceId = "resourceId";
                    public const string NotBefore = "notBefore";
                    public const string NotAfter = "notAfter";
                    public const string RenewAfter = "renewAfter";
                    public const string CannotRenewAfter = "cannotRenewAfter";
                    public const string UpdateAction = "updateAction";
                }
            }
        }

        public static class StorageKeyManagementProperties
        {
            public const string StorageAccountSubscriptionId = "storageAccountSubscriptionId";
            public const string StorageAccountResourceGroup = "storageAccountResourceGroup";
            public const string StorageAccountName = "storageAccountName";
            public const string StorageAccountUri = "storageAccountUri";
            public const string StorageAccountPrimaryKeyInUse = "storageAccountPrimaryKeyInUse";
            public const string StorageAccountSecondaryKeyInUse = "storageAccountSecondaryKeyInUse";
            public const string StorageAccountPrimaryKey = "storageAccountPrimaryKey";
            public const string StorageAccountSecondaryKey = "storageAccountSecondaryKey";
            public const string StorageAccountType = "storageAccountType";
            public const string ForceRefresh = "forceRefresh";
            public const string Permissions = "permissions";
            public const string CurrentAccountSasToken = "currentAccountSasToken";
            public const string KeyToSign = "keyToSign";
            public const string AccountSasToken = "accountSasToken";
            public const string ExpiryTime = "expiryTime";
            public const string ListAccountSasRequestServices = "bqtf";
            public const string ListAccountSasRequestResourceTypes = "sco";
            public const string DefaultAccountSasPermissions = "rwdlacup";
            public const string DefaultReadAccountSasPermissions = "rl";
            public const string SasSeparator = "?";

            // The Storage Management API uses these names for storage account system, primary and secondary keys/sas tokens.
            public const string Key1Name = "key1";
            public const string Key2Name = "key2";
            public const string SystemKeyName = "system";

            // Config hooks
            public const string EnableStorageAccountKeyFetch = "enableStorageAccountKeyFetch";
            public const string CachedStorageAccountKeyRefreshIntervalInHours = "cachedStorageAccountKeyRefreshIntervalInHours";
            public const string StorageKeyManagementClientRequestTimeoutInSeconds = "storageKeyManagementClientRequestTimeoutInSeconds";
            public const string StorageKeyManagementAADAuthRetryIntervalInSeconds = "storageKeyManagementAADAuthRetryIntervalInSeconds";
            public const string StorageKeyManagementAADAuthRetryCount = "storageKeyManagementAADAuthRetryCount";
            public const string StorageAccountKeyCacheExpirationIntervalInHours = "storageAccountKeyCacheExpirationIntervalInHours";
            public const string StorageAccountKeyRequestTimeoutInSeconds = "storageAccountKeyRequestTimeoutInSeconds";
            public const string StorageServiceUrlSuffix = "storageServiceUrlSuffix";
            public const string AzureResourceManagerEndpoint = "azureResourceManagerEndpoint";
            public const string StorageSasManagementClientRequestTimeoutInSeconds = "storageSasManagementClientRequestTimeoutInSeconds";

            public const string FederationAzureActiveDirectoryEndpoint = "FederationAzureActiveDirectoryEndpoint";
            public const string FederationAzureActiveDirectoryClientId = "FederationAzureActiveDirectoryClientId";
            public const string FederationAzureActiveDirectoryTenantId = "FederationAzureActiveDirectoryTenantId";
            public const string FederationToAadCertDsmsSourceLocation = "FederationToAadCertDsmsSourceLocation";
            public const string MasterFederationId = "masterFederationId";
            public const string RegionalDatabaseAccountName = "regionalDatabaseAccountName";

            // Account sas related configuration properties
            public const string SystemSasValidityInHours = "systemSasValidityInHours";
            public const string PrimaryOrSecondaryKeyBasedSasValidityInHours = "primaryOrSecondaryKeyBasedSasValidityInHours";
            public const string CacheSystemSasExpiryInHours = "cacheSystemSasExpiryInHours";
            public const string CachePrimaryOrSecondaryKeyBasedSasExpiryInHours = "cachePrimaryOrSecondaryKeyBasedSasExpiryInHours";
            public const string SasStartTimeSubtractionIntervalInMinutes = "sasStartTimeSubtractionIntervalInMinutes";
            public const string AlwaysReturnKey1BasedSas = "alwaysReturnKey1BasedSas";
            public const string CacheExpirySubtractionTimeInMinutes = "cacheExpirySubtractionTimeInMinutes";
            public const string EnableMasterSideAuthTokenCaching = "enableMasterSideAuthTokenCaching";
            public const string EnableSasRequestRoutingToMasterFederation = "enableSasRequestRoutingToMasterFederation";
            public const string EnableSasTokenLogging = "enableSasTokenLogging";
        }

        public static class VNetServiceAssociationLinkProperties
        {
            public const string SubnetArmUrl = "subnetArmUrl";
            public const string PrimaryContextRequestId = "primaryContextRequestId";
            public const string SecondaryContextRequestId = "secondaryContextRequestId";
            public const string PrimaryContextId = "primaryContextId";
            public const string SecondaryContextId = "secondaryContextId";
        }

        public static class SystemSubscriptionUsageType
        {
            public const string SMS = "sms";
            public const string CassandraConnectorStorage = "cassandraconnectorstorage";
            public const string NotebooksStorage = "notebooksstorage";
            public const string AnalyticsStorage = "analyticsstorage";
            public const string SparkStorage = "sparkstorage";
        }

        public static class SystemSubscriptionProperties
        {
            public const string SystemSubscriptionAction = "systemSubscriptionAction";
            public const string SubscriptionUsageKind = "subscriptionUsageKind";
        }

        public static class ConnectorOfferKind
        {
            public const string Cassandra = "cassandra";
        }

        public static class ConnectorMetadataAccountNamePrefix
        {
            public const string CassandraConnectorMetadataAccountNamePrefix = "ccxmeta";
        }

        public static class ConnectorOfferName
        {
            public const string Small = "small";
        }

        public static class AnalyticsStorageAccountProperties
        {
            public const string AddStorageServices = "AddStorageServices";
            public const string ResourceGroupName = "AnalyticsStorageAccountRG";

            // BlobStorageAttributes for Storage Analytics
            public const string IsDeletedProperty = "IsDeleted";
            public const string DeletedTimeStampProperty = "DeletedTimeStamp";

            // Analytics Storage Properties (It is a contract with BE, need to be kept in sync with StreamManager2.h/.cpp)
            public const string PartitionedParQuetSuffix = "Partitioned.Parquet";
            public const string CosmosSuffix = "Cosmos";
            public const string RootMetaDataSuffix = "RootMetadata";
            public const string PartitionedPreffix = "Partitioned";
            public const string ParquetFileExtension = "Parquet";
            public const string CompressionType = "snappy";
            public const string RootFileName = "Root";
            public const string MetadataExtension = "Metadata";
            public const string InvalidationFileExtension = "Invalidation";
            public const string InvalidationManifestFileExtension = "InvalidationManifest";
            public const string MergedInvalidationFileExtension = "MergedInvalidation";
            public const string SnapshotFileName = "Snapshot";

            public const string MetadataBlobFileNameSuffix = "Root.Metadata";
        }

        public static class BackupConstants
        {
            public const int BackupDisabled = -1;
            public const int DefaultBackupIntervalInMinutes = 240;
            public const int DefaultBackupRetentionIntervalInHours = 8;
            public const int LegacyDefaultBackupRetentionIntervalInHours = 24;

            public const int PitrDefaultBackupIntervalInHours = 168; // Weekly full backups
            public const int PitrDefaultBackupRetentionIntervalInDays = 30;
            public const int PitrBasicDefaultBackupRetentionIntervalInDays = 7;

            public const int DisableBackupHoldFeature = -1;
            public const string BackupHoldIndefinite = "0";

            public const string ServerBackupContainerIdentifier = "-s-";
            public const string MasterBackupContainerIdentifier = "-m-";
            public const string DummyBackupContainerIdentifier = "-d-"; // Used for NoEdb Restore

            public const int ShortenedPartitionIdLengthInContainerName = 19;

            public const int MinBackupIntervalInMinutes = 60;
            public const int MaxBackupIntervalInMinutes = 1440;

            public const int MinBackupRetentionIntervalInHours = 8;
            public const int MaxBackupRetentionIntervalInHours = 720;

            public const int InvalidPeriodicBackupInterval = -1;
            public const int InvalidPeriodicBackupRetention = -1;

            public const string BackupEndTimestamp = "backupEndTimestamp";
        }

        public static class KeyVaultProperties
        {
            public const string KeyVaultKeyUri = "keyVaultKeyUri";
            public const string WrappedDek = "wrappedDek";
            public const string EnableByok = "enableByok";
            public const string KeyVaultKeyUriVersion = "keyVaultKeyUriVersion";
            public const string DataEncryptionKeyStatus = "dataEncryptionKeyStatus";
            public const string DataEncryptionKeyRequestOperation = "dataEncryptionKeyRequestOperation";
        }

        public static class ManagedServiceIdentityProperties
        {
            public const string MsiClientId = "msiClientId";
            public const string MsiClientSecretEncrypted = "msiClientSecretEncrypted";
            public const string MsiCertRenewAfter = "msiCertRenewAfter";
            public const string MsiTenantId = "msiTenantId";

            public const string AssignUserAssignedIdentity = "assignUserAssignedIdentity";
            public const string UnassignUserAssignedIdentity = "unassignUserAssignedIdentity";

            public const string AssignSourceDelegation = "assignSourceDelegation";
            public const string UnassignSourceDelegation = "unassignSourceDelegation";

            public const string WildcardEnabled = "wildcardEnabled";
            public const string WildcardDisabled = "wildcardDisabled";

            public const string FederatedClientId = "&FederatedClientId=";
            public const string SourceInternalId = "&SourceInternalId=";
        }

        public static class LogStoreConstants
        {
            public const int BasedOnSubscription = 0;
            public const int EnsureOperationLogsFlushedTimeoutTimeInMinutes = 60;
            public const int CrossRegionRestoreOperationLogsFlushTimeoutTimeInMinutes = 15;

            // StreamStandard => 0x0004 = 4
            public const int PreferredLogStoreMetadataStorageKind = 4;

            // Default timeout value of polling operation WaitForStorageKeyAccessOperation
            public const double TimeoutDurationInMinutes = 60.0;

            // It needs to be in sync with bancked config for operation logs flush interval, which is currently 100s
            public const int EnsureOperationLogsPauseTimeInSeconds = 200;

            //Blob metadata
            public const string PartitionKeyRangeRid = "PartitionKeyRangeRid";
            public const string CollectionLLSN = "CollectionLLSN";
            public const string CollectionGLSN = "CollectionGLSN";
            public const string IsValidTimeStamp = "IsValidTimeStamp";
            public const string CollectionTS = "CollectionTS";
        }

        public static class RestoreConstants
        {
            public const int ListBlobMaxResults = 1000;
            public const int ListBlobRetryDeltaBackoffInSeconds = 30;
            public const int ListBlobRetryMaxAttempts = 5;

            public const int MaxRetryCount = 3;
            public const int RetryDelayInSec = 20;

            public const int RestorableDatabaseAccountUriLength = 9;
            public const int DatabaseRequestUriLength = 11;
            public const int ContainerRequestUriLength = 13;
        }

        public static class SubscriptionsQuotaConstants
        {
            // based on doc: https://docs.microsoft.com/en-us/azure/cosmos-db/concepts-limits#control-plane-operations
            public const int DefaultMaxDatabaseAccountCount = 50;
            public const int DefaultMaxRegionsPerGlobalDatabaseAccount = 50;
        }

        public static class PolicyConstants
        {
            public const string PolicyType = "policytype";
            public const string BalancingThreshold = "balancingThreshold";
            public const string TriggerThreshold = "triggerThreshold";
            public const string MetricId = "metricId";
            public const string MetricValue = "metricValue";
            public const string MetricWeight = "metricWeight";
            public const string GlobalPolicyPartitionKey = "global";
            public const string Policies = "policies";
            public const string FederationListSortedByUsageScore = "federationListSortedByUsageScore";
            public const string ServiceAllocationOperationType = "serviceAllocationOperationType";
            public const string MaxAllowedPartitionCount = "maxAllowedPartitionCount";
            public const string DisAllowedSubscriptionOfferKinds = "disAllowedSubscriptionOfferKinds";
            public const string NumberOfFederationsToRandomize = "numberOfFederationsToRandomize";
            public const string RegionAccessTypes = "regionAccessTypes";
            public const string RegionAccessType = "regionAccessType";
        }

        public static class SystemStoreConstants
        {
            public const int DefaultBackupIntervalInMinute = 60;
            public const int SMSDefaultBackupIntervalInMinute = 30;
            public const int DefaultBackupRetentionInHour = 720;
            public const string ResourceGroupSuffix = "-rg";
        }

        public static class TransportControlCommandOperations
        {
            public const string DeactivateOutputQueue = "deactivateOutputQueue";
            public const string ActivateOutputQueue = "activateOutputQueue";
        }

        public static class RetryConstants
        {
            public const int DisableOverride = -1;
        }

        public static class DedicatedStorageAccountFeatures
        {
            public const string StorageAnalytics = "StorageAnalytics";
            public const string MaterializedViews = "MaterializedViews";
            public const string PITR = "Continuous mode backup policy (PITR)";
            public const string FullFidelityChangeFeed = "FullFidelityChangeFeed";
        }

        public static class GraphApiConstants
        {
            public const string ServerStartupConfigV1 = "gremlin-server-azurecosmos-configuration.yaml";
            public const string ServerStartupConfigV2 = "gremlin-server-azurecosmos-configuration-v2.yaml";
            public const string EnableFEHealthIntegration = "enableFEHealthIntegration";
            public const string GraphConfigPrefix = "graphapi.";
        }

        public static class ChangeFeedWireFormatVersions
        {
            // New wire format that separates document content, metadata and previousImage.
            // ConflictResolvedTimestamp(crts) exposed in this version.
            public static string SeparateMetadataWithCrts = "2021-09-15";
        }

        public static class BatchApplyResourceProperties
        {
            public static string BatchApplyOperations = "operations";
        }

        public static class ApiSpecificNames
        {
            public static string SqlDatabase = "Database";
            public static string SqlContainer = "Container";
            public static string MongoDatabase = "Database";
            public static string MongoCollection = "Collection";
            public static string GremlinDatabase = "GremlinDatabase";
            public static string GremlinGraph = "Graph";
            public static string CassandraKeyspace = "Keyspace";
            public static string CassandraTable = "Table";
            public static string Table = "Table";
        }

        public static class ApiSpecificUrlNames
        {
            public static string SqlDatabase = "sqlDatabases";
            public static string SqlContainer = "containers";
            public static string MongoDatabase = "mongodbDatabases";
            public static string MongoCollection = "collections";
            public static string GremlinDatabase = "gremlinDatabases";
            public static string GremlinGraph = "graphs";
            public static string CassandraKeyspace = "cassandraKeyspaces";
            public static string CassandraTable = "tables";
            public static string Table = "tables";
        }

        public static class MigratePartitionCallerSource
        {
            public static string Test = "Test";
            public static string Unknown = "Unknown";
            public static string PLB_localRegion = "PLB_localRegion";
            public static string PLB_CrossRegion = "PLB_CrossRegion";
            public static string ACIS_PartitionMigration = "ACIS_PartitionMigration";
            public static string ACIS_BulkPartitionMigration = "ACIS_BulkPartitionMigration";
            public static string ACIS_CrossSubregionAccountMigration = "ACIS_CrossSubregionAccountMigration";
            public static string ACIS_MitigateMasterMigrationFailure = "ACIS_MitigateMasterMigrationFailure";
            public static string ACIS_MitigateServerMigrationFailure = "ACIS_MitigateServerMigrationFailure";
            public static string FederationBuildout_CanaryAccountMigration = "FederationBuildout_CanaryAccountMigration";
        }

        public static class EnvironmentVariables
        {
            public const string ReplicaConnectivityValidationEnabled = "AZURE_COSMOS_REPLICA_VALIDATION_ENABLED";
        }
    }
}
