//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    internal static class Paths
    {
        //root ---------------------------------
        public const string Root = "/";

        public const string OperationsPathSegment = "operations";
        public const string OperationId = "operationId";
        public const string ReplicaOperations_Pause = "pause";
        public const string ReplicaOperations_Resume = "resume";
        public const string ReplicaOperations_Stop = "stop";
        public const string ReplicaOperations_Recycle = "recycle";
        public const string ReplicaOperations_Crash = "crash";
        public const string ReplicaOperations_ForceConfigRefresh = "forceConfigRefresh";
        public const string ReplicaOperations_ReportThroughputUtilization = "reportthroughpututilization";
        public const string ReplicaOperations_BatchReportThroughputUtilization = "batchreportthroughpututilization";
        public const string Operations_GetFederationConfigurations = "getfederationconfigurations";
        public const string Operations_GetConfiguration = "getconfiguration";
        public const string Operations_GetDatabaseAccountConfigurations = "getdatabaseaccountconfigurations";
        public const string Operations_GetStorageServiceConfigurations = "getstorageserviceconfigurations";
        public const string Operations_GetStorageAccountKey = "getstorageaccountkey";
        public const string Operations_GetStorageAccountSas = "getstorageaccountsas";
        public const string Operations_GetUnwrappedDek = "getunwrappeddek";
        public const string Operations_ReadReplicaFromMasterPartition = "readreplicafrommasterpartition";
        public const string Operations_ReadReplicaFromServerPartition = "readreplicafromserverpartition";
        public const string Operations_MasterInitiatedProgressCoordination = "masterinitiatedprogresscoordination";
        public const string Operations_GetAadGroups = "getaadgroups";

        //databases namespace off of root-------------------

        // /dbs
        public const string DatabasesPathSegment = "dbs";
        public const string Databases_Root = Root + "/" + DatabasesPathSegment + "/";

        // /dbs/{id}
        public const string DatabaseId = "dbId";
        public const string Database_Root = Databases_Root + "{" + DatabaseId + "}";

        // /dbs/{id}/users
        public const string UsersPathSegment = "users";
        public const string Users_Root = Database_Root + "/" + UsersPathSegment + "/";

        // /dbs/{id}/users/{id}
        public const string UserId = "userid";
        public const string User_Root = Users_Root + "{" + UserId + "}";

        // /dbs/{id}/clientencryptionkeys
        public const string ClientEncryptionKeysPathSegment = "clientencryptionkeys";
        public const string ClientEncryptionKeys_Root = Database_Root + "/" + ClientEncryptionKeysPathSegment + "/";

        // /dbs/{id}/clientencryptionkeys/{id}
        public const string ClientEncryptionKeyId = "clientencryptionkeyId";
        public const string ClientEncryptionKey_Root = ClientEncryptionKeys_Root + "{" + ClientEncryptionKeyId + "}";

        // /dbs/{id}/udts
        public const string UserDefinedTypesPathSegment = "udts";
        public const string UserDefinedTypes_Root = Database_Root + "/" + UserDefinedTypesPathSegment + "/";

        // /dbs/{id}/udts/{id}
        public const string UserDefinedTypeId = "udtId";
        public const string UserDefinedType_Root = UserDefinedTypes_Root + "{" + UserDefinedTypeId + "}";

        // /dbs/{id}/users/{id}/permissions
        public const string PermissionsPathSegment = "permissions";
        public const string Permissions_Root = User_Root + "/" + PermissionsPathSegment + "/";

        // /dbs/{id}/users/{id}/permissions/{id}
        public const string PermissionId = "permissionId";
        public const string Permission_Root = Permissions_Root + "{" + PermissionId + "}";

        // /dbs/{id}/colls
        public const string CollectionsPathSegment = "colls";
        public const string Collections_Root = Database_Root + "/" + CollectionsPathSegment + "/";

        // /dbs/{id}/colls/{id}
        public const string CollectionId = "collId";
        public const string Collection_Root = Collections_Root + "{" + CollectionId + "}";

        // /dbs/{id}/colls/{id}/sprocs
        public const string StoredProceduresPathSegment = "sprocs";
        public const string StoredProcedures_Root = Collection_Root + "/" + StoredProceduresPathSegment + "/";

        // /dbs/{id}/colls/{id}/sprocs/{id}
        public const string StoredProcedureId = "sprocId";
        public const string StoredProcedure_Root = StoredProcedures_Root + "{" + StoredProcedureId + "}";

        // /dbs/{id}/colls/{id}/triggers
        public const string TriggersPathSegment = "triggers";
        public const string Triggers_Root = Collection_Root + "/" + TriggersPathSegment + "/";

        // /dbs/{id}/colls/{id}/triggers/{id}
        public const string TriggerId = "triggerId";
        public const string Trigger_Root = Triggers_Root + "{" + TriggerId + "}";

        // /dbs/{id}/colls/{id}/udfs
        public const string UserDefinedFunctionsPathSegment = "udfs";
        public const string UserDefinedFunctions_Root = Collection_Root + "/" + UserDefinedFunctionsPathSegment + "/";

        // /dbs/{id}/colls/{id}/functions/{id}
        public const string UserDefinedFunctionId = "udfId";
        public const string UserDefinedFunction_Root = UserDefinedFunctions_Root + "{" + UserDefinedFunctionId + "}";

        // /dbs/{id}/colls/{id}/conflicts
        public const string ConflictsPathSegment = "conflicts";
        public const string Conflicts_Root = Collection_Root + "/" + ConflictsPathSegment + "/";

        // /dbs/{id}/colls/{id}/conflicts/{id}
        public const string ConflictId = "conflictId";
        public const string Conflict_Root = Conflicts_Root + "{" + ConflictId + "}";

        // /dbs/{id}/colls/{id}/partitionedsystemdocuments
        public const string PartitionedSystemDocumentsPathSegment = "partitionedsystemdocuments";
        public const string PartitionedSystemDocuments_Root = Collection_Root + "/" + PartitionedSystemDocumentsPathSegment + "/";

        // /dbs/{id}/colls/{id}/partitionedsystemdocuments/{id}
        public const string PartitionedSystemDocumentId = "partitionedSystemDocumentId";
        public const string PartitionedSystemDocument_Root = PartitionedSystemDocuments_Root + "{" + PartitionedSystemDocumentId + "}";

        // /dbs/{id}/colls/{id}/systemdocuments
        public const string SystemDocumentsPathSegment = "systemdocuments";
        public const string SystemDocuments_Root = Collection_Root + "/" + SystemDocumentsPathSegment + "/";

        // /dbs/{id}/colls/{id}/systemdocuments/{id}
        public const string SystemDocumentId = "systemDocumentId";
        public const string SystemDocument_Root = SystemDocuments_Root + "{" + SystemDocumentId + "}";

        // /dbs/{id}/colls/{id}/docs
        public const string DocumentsPathSegment = "docs";
        public const string Documents_Root = Collection_Root + "/" + DocumentsPathSegment + "/";

        // /dbs/{id}/colls/{id}/docs/{id}
        public const string DocumentId = "docId";
        public const string Document_Root = Documents_Root + "{" + DocumentId + "}";

        // /dbs/{id}/colls/{id}/docs/{id}/attachments
        public const string AttachmentsPathSegment = "attachments";
        public const string Attachments_Root = Document_Root + "/" + AttachmentsPathSegment + "/";

        // /dbs/{id}/colls/{id}/docs/{id}/attachments/{id}
        public const string AttachmentId = "attachmentId";
        public const string Attachment_Root = Attachments_Root + "{" + AttachmentId + "}";

        // /dbs/{id}/colls/{id}/pkranges
        public const string PartitionKeyRangesPathSegment = "pkranges";
        public const string PartitionKeyRanges_Root = Collection_Root + "/" + PartitionKeyRangesPathSegment + "/";

        // /dbs/{id}/colls/{id}/pkranges/{id}
        public const string PartitionKeyRangeId = "pkrangeId";
        public const string PartitionKeyRange_Root = PartitionKeyRanges_Root + "{" + PartitionKeyRangeId + "}";

        // /dbs/{id}/colls/{id}/pkranges/{id}/presplitaction
        public const string PartitionKeyRangePreSplitSegment = "presplitaction";
        public const string PartitionKeyRangePreSplit_Root = PartitionKeyRange_Root + "/" + PartitionKeyRangePreSplitSegment + "/";

        // /dbs/{id}/colls/{id}/pkranges/{id}/postsplitaction
        public const string PartitionKeyRangePostSplitSegment = "postsplitaction";
        public const string PartitionKeyRangePostSplit_Root = PartitionKeyRange_Root + "/" + PartitionKeyRangePostSplitSegment + "/";

        // /dbs/{id}/colls/{id}/pkranges/{id}/split
        public const string ParatitionKeyRangeOperations_Split = "split";

        // /partitions
        public const string PartitionsPathSegment = "partitions";
        public const string Partitions_Root = Root + "/" + PartitionsPathSegment + "/";

        // /databaseAccount
        public const string DatabaseAccountSegment = "databaseaccount";
        public const string DatabaseAccount_Root = Root + "/" + DatabaseAccountSegment + "/";

        // /files
        public const string FilesPathSegment = "files";
        public const string Files_Root = Root + "/" + FilesPathSegment + "/";

        public const string FileId = "fileId";
        public const string File_Root = Files_Root + "{" + FileId + "}";

        // /medias
        public const string MediaPathSegment = "media";
        public const string Medias_Root = Root + "/" + MediaPathSegment + "/";

        public const string MediaId = "mediaId";
        public const string Media_Root = Medias_Root + "{" + MediaId + "}";

        // /address
        public const string AddressPathSegment = "addresses";
        public const string Address_Root = Root + "/" + AddressPathSegment + "/";

        // /xpreplicatoraddress
        public const string XPReplicatorAddressPathSegment = "xpreplicatoraddreses";
        public const string XPReplicatorAddress_Root = Root + "/" + XPReplicatorAddressPathSegment + "/";

        // /offers
        public const string OffersPathSegment = "offers";
        public const string Offers_Root = Root + "/" + OffersPathSegment + "/";

        // /offers/{id}
        public const string OfferId = "offerId";
        public const string Offer_Root = Offers_Root + "{" + OfferId + "}";

        // /topology
        public const string TopologyPathSegment = "topology";
        public const string Topology_Root = Root + "/" + TopologyPathSegment + "/";

        // /dbs/{id}/colls/{id}/schemas
        public const string SchemasPathSegment = "schemas";
        public const string Schemas_Root = Collection_Root + "/" + SchemasPathSegment + "/";

        // /dbs/{id}/colls/{id}/schemas/{id}
        public const string SchemaId = "schemaId";
        public const string Schema_Root = Schemas_Root + "{" + SchemaId + "}";

        // /servicereservation
        public const string ServiceReservationPathSegment = "serviceReservation";
        public const string ServiceReservation_Root = Root + "/" + ServiceReservationPathSegment + "/";

        // document explorer
        public const string DataExplorerSegment = "_explorer";
        public const string DataExplorerAuthTokenSegment = "authorization";

        // /ridRange
        public const string RidRangePathSegment = "ridranges";
        public const string RidRange_Root = Root + "/" + RidRangePathSegment + "/";

        // /snapshots
        public const string SnapshotsPathSegment = "snapshots";
        public const string Snapshots_Root = Root + "/" + SnapshotsPathSegment + "/";

        // /snapshots/{id}
        public const string SnapshotId = "snapshotId";
        public const string Snapshot_Root = Snapshots_Root + "{" + SnapshotId + "}";

        public const string DataExplorer_Root = Paths.Root + "/" + DataExplorerSegment;
        public const string DataExplorerAuthToken_Root = DataExplorer_Root + "/" + DataExplorerAuthTokenSegment;
        public const string DataExplorerAuthToken_WithoutResourceId = DataExplorerAuthToken_Root + "/{verb}/{resourceType}";
        public const string DataExplorerAuthToken_WithResourceId = DataExplorerAuthToken_WithoutResourceId + "/" + "{resourceId}";

        // compute gateway charge path
        internal const string ComputeGatewayChargePathSegment = "computegatewaycharge";

        // controller service
        public const string ControllerOperations_BatchGetOutput = "controllerbatchgetoutput";
        public const string ControllerOperations_BatchReportCharges = "controllerbatchreportcharges";

        // vector clock
        public const string VectorClockPathSegment = "vectorclock";

        // partition key delete
        public const string PartitionKeyDeletePathSegment = "partitionkeydelete";
        public const string PartitionKeyDelete = Collection_Root + "/" + OperationsPathSegment + "/" + PartitionKeyDeletePathSegment;

        // /roleAssignments
        public const string RoleAssignmentsPathSegment = "roleassignments";
        public const string RoleAssignments_Root = Root + "/" + RoleAssignmentsPathSegment + "/";

        // /roleAssignments/{id}
        public const string RoleAssignmentId = "roleassignmentId";
        public const string RoleAssignment_Root = RoleAssignments_Root + "{" + RoleAssignmentId + "}";

        // /roleDefinitions
        public const string RoleDefinitionsPathSegment = "roledefinitions";
        public const string RoleDefinitions_Root = Root + "/" + RoleDefinitionsPathSegment + "/";

        // /roleDefinitions/{id}
        public const string RoleDefinitionId = "roledefinitionId";
        public const string RoleDefinition_Root = RoleDefinitions_Root + "{" + RoleDefinitionId + "}";

        // /dbs/{id}/colls/{id}/operations/collectiontruncate
        public const string CollectionTruncatePathsegment = "collectiontruncate";
        public const string CollectionTruncate = Collection_Root + "/" + OperationsPathSegment + "/" + CollectionTruncatePathsegment;

        // /transactions
        public const string TransactionsPathSegment = "transaction";
        public const string Transactions_Root = Root + "/" + TransactionsPathSegment + "/";

        // /transactions/{id}
        public const string TransactionId = "transactionId";
        public const string Transaction_Root = Transactions_Root + "{" + TransactionId + "}";

        // /authpolicyelements
        public const string AuthPolicyElementsPathSegment = "authpolicyelements";
        public const string AuthPolicyElements_Root = Root + "/" + AuthPolicyElementsPathSegment + "/";

        // /authpolicyelements/{id}
        public const string AuthPolicyElementId = "authpolicyelementId";
        public const string AuthPolicyElement_Root = AuthPolicyElements_Root + "{" + AuthPolicyElementId + "}";
    }
}
