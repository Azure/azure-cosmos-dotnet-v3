//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Diagnostics;
    using System.Globalization;

    internal struct ResourceOperation
    {
        public readonly OperationType operationType;
        public readonly ResourceType resourceType;

        public ResourceOperation(
            OperationType operationType,
            ResourceType resourceType)
        {
            this.operationType = operationType;
            this.resourceType = resourceType;
        }

        // TODO, atulk, delete these definitions once the gateway refactoring is completed
        public static readonly ResourceOperation CreateOffer = new ResourceOperation(OperationType.Create, ResourceType.Offer);
        public static readonly ResourceOperation ReplaceOffer = new ResourceOperation(OperationType.Replace, ResourceType.Offer);
        public static readonly ResourceOperation ReadOffer = new ResourceOperation(OperationType.Read, ResourceType.Offer);
        public static readonly ResourceOperation DeleteOffer = new ResourceOperation(OperationType.Delete, ResourceType.Offer);
        public static readonly ResourceOperation ReadOfferFeed = new ResourceOperation(OperationType.ReadFeed, ResourceType.Offer);

#if !COSMOSCLIENT
        public static readonly ResourceOperation ReadPartitionSetInformation = new ResourceOperation(OperationType.ReadFeed, ResourceType.PartitionSetInformation);
#endif

        public static readonly ResourceOperation CreateDatabase = new ResourceOperation(OperationType.Create, ResourceType.Database);
        public static readonly ResourceOperation UpsertDatabase = new ResourceOperation(OperationType.Upsert, ResourceType.Database);
        public static readonly ResourceOperation PatchDatabase = new ResourceOperation(OperationType.Patch, ResourceType.Database);
        public static readonly ResourceOperation ReplaceDatabase = new ResourceOperation(OperationType.Replace, ResourceType.Database);
        public static readonly ResourceOperation DeleteDatabase = new ResourceOperation(OperationType.Delete, ResourceType.Database);
        public static readonly ResourceOperation ReadDatabase = new ResourceOperation(OperationType.Read, ResourceType.Database);
        public static readonly ResourceOperation ReadDatabaseFeed = new ResourceOperation(OperationType.ReadFeed, ResourceType.Database);
        public static readonly ResourceOperation HeadDatabaseFeed = new ResourceOperation(OperationType.HeadFeed, ResourceType.Database);

        public static readonly ResourceOperation CreateCollection = new ResourceOperation(OperationType.Create, ResourceType.Collection);
        public static readonly ResourceOperation PatchCollection = new ResourceOperation(OperationType.Patch, ResourceType.Collection);
        public static readonly ResourceOperation ReplaceCollection = new ResourceOperation(OperationType.Replace, ResourceType.Collection);
        public static readonly ResourceOperation DeleteCollection = new ResourceOperation(OperationType.Delete, ResourceType.Collection);
        public static readonly ResourceOperation ReadCollection = new ResourceOperation(OperationType.Read, ResourceType.Collection);
        public static readonly ResourceOperation HeadCollection = new ResourceOperation(OperationType.Head, ResourceType.Collection);
        public static readonly ResourceOperation ReadCollectionFeed = new ResourceOperation(OperationType.ReadFeed, ResourceType.Collection);

        public static readonly ResourceOperation CreateDocument = new ResourceOperation(OperationType.Create, ResourceType.Document);
        public static readonly ResourceOperation UpsertDocument = new ResourceOperation(OperationType.Upsert, ResourceType.Document);
        public static readonly ResourceOperation PatchDocument = new ResourceOperation(OperationType.Patch, ResourceType.Document);
        public static readonly ResourceOperation ReplaceDocument = new ResourceOperation(OperationType.Replace, ResourceType.Document);
        public static readonly ResourceOperation DeleteDocument = new ResourceOperation(OperationType.Delete, ResourceType.Document);
        public static readonly ResourceOperation ReadDocument = new ResourceOperation(OperationType.Read, ResourceType.Document);
        public static readonly ResourceOperation ReadDocumentFeed = new ResourceOperation(OperationType.ReadFeed, ResourceType.Document);

        public static readonly ResourceOperation ExecuteDocumentFeed = new ResourceOperation(OperationType.ExecuteJavaScript, ResourceType.StoredProcedure);

        public static readonly ResourceOperation CreateAttachment = new ResourceOperation(OperationType.Create, ResourceType.Attachment);
        public static readonly ResourceOperation UpsertAttachment = new ResourceOperation(OperationType.Upsert, ResourceType.Attachment);
        public static readonly ResourceOperation PatchAttachment = new ResourceOperation(OperationType.Patch, ResourceType.Attachment);
        public static readonly ResourceOperation ReplaceAttachment = new ResourceOperation(OperationType.Replace, ResourceType.Attachment);
        public static readonly ResourceOperation DeleteAttachment = new ResourceOperation(OperationType.Delete, ResourceType.Attachment);
        public static readonly ResourceOperation ReadAttachment = new ResourceOperation(OperationType.Read, ResourceType.Attachment);
        public static readonly ResourceOperation ReadAttachmentFeed = new ResourceOperation(OperationType.ReadFeed, ResourceType.Attachment);

        public static readonly ResourceOperation CreateStoredProcedure = new ResourceOperation(OperationType.Create, ResourceType.StoredProcedure);
        public static readonly ResourceOperation UpsertStoredProcedure = new ResourceOperation(OperationType.Upsert, ResourceType.StoredProcedure);
        public static readonly ResourceOperation ReplaceStoredProcedure = new ResourceOperation(OperationType.Replace, ResourceType.StoredProcedure);
        public static readonly ResourceOperation DeleteStoredProcedure = new ResourceOperation(OperationType.Delete, ResourceType.StoredProcedure);
        public static readonly ResourceOperation ReadStoredProcedure = new ResourceOperation(OperationType.Read, ResourceType.StoredProcedure);
        public static readonly ResourceOperation ReadStoredProcedureFeed = new ResourceOperation(OperationType.ReadFeed, ResourceType.StoredProcedure);

        public static readonly ResourceOperation CreateUser = new ResourceOperation(OperationType.Create, ResourceType.User);
        public static readonly ResourceOperation UpsertUser = new ResourceOperation(OperationType.Upsert, ResourceType.User);
        public static readonly ResourceOperation PatchUser = new ResourceOperation(OperationType.Patch, ResourceType.User);
        public static readonly ResourceOperation ReplaceUser = new ResourceOperation(OperationType.Replace, ResourceType.User);
        public static readonly ResourceOperation DeleteUser = new ResourceOperation(OperationType.Delete, ResourceType.User);
        public static readonly ResourceOperation ReadUser = new ResourceOperation(OperationType.Read, ResourceType.User);
        public static readonly ResourceOperation ReadUserFeed = new ResourceOperation(OperationType.ReadFeed, ResourceType.User);

        public static readonly ResourceOperation CreatePermission = new ResourceOperation(OperationType.Create, ResourceType.Permission);
        public static readonly ResourceOperation UpsertPermission = new ResourceOperation(OperationType.Upsert, ResourceType.Permission);
        public static readonly ResourceOperation PatchPermission = new ResourceOperation(OperationType.Patch, ResourceType.Permission);
        public static readonly ResourceOperation ReplacePermission = new ResourceOperation(OperationType.Replace, ResourceType.Permission);
        public static readonly ResourceOperation DeletePermission = new ResourceOperation(OperationType.Delete, ResourceType.Permission);
        public static readonly ResourceOperation ReadPermission = new ResourceOperation(OperationType.Read, ResourceType.Permission);
        public static readonly ResourceOperation ReadPermissionFeed = new ResourceOperation(OperationType.ReadFeed, ResourceType.Permission);

        public static readonly ResourceOperation CreateClientEncryptionKey = new ResourceOperation(OperationType.Create, ResourceType.ClientEncryptionKey);
        public static readonly ResourceOperation ReplaceClientEncryptionKey = new ResourceOperation(OperationType.Replace, ResourceType.ClientEncryptionKey);
        public static readonly ResourceOperation DeleteClientEncryptionKey = new ResourceOperation(OperationType.Delete, ResourceType.ClientEncryptionKey);
        public static readonly ResourceOperation ReadClientEncryptionKey = new ResourceOperation(OperationType.Read, ResourceType.ClientEncryptionKey);
        public static readonly ResourceOperation ReadClientEncryptionKeyFeed = new ResourceOperation(OperationType.ReadFeed, ResourceType.ClientEncryptionKey);

#if !COSMOSCLIENT
        public static readonly ResourceOperation ReadReplica = new ResourceOperation(OperationType.Read, ResourceType.Replica);
        public static readonly ResourceOperation ForceConfigRefresh = new ResourceOperation(OperationType.ForceConfigRefresh, ResourceType.Replica);

        public static readonly ResourceOperation CheckThrottlihg = new ResourceOperation(OperationType.Throttle, ResourceType.Collection);

        public static readonly ResourceOperation ReplaceModule = new ResourceOperation(OperationType.Replace, ResourceType.Module);
        public static readonly ResourceOperation ReadModule = new ResourceOperation(OperationType.Read, ResourceType.Module);

        public static readonly ResourceOperation Pause = new ResourceOperation(OperationType.Pause, ResourceType.Replica);
        public static readonly ResourceOperation Split = new ResourceOperation(OperationType.GetSplitPoint, ResourceType.PartitionKeyRange);
        public static readonly ResourceOperation NWaySplit = new ResourceOperation(OperationType.GetSplitPoints, ResourceType.PartitionKeyRange);
        public static readonly ResourceOperation AbortSplit = new ResourceOperation(OperationType.AbortSplit, ResourceType.PartitionKeyRange);
        public static readonly ResourceOperation CompleteSplit = new ResourceOperation(OperationType.CompleteSplit, ResourceType.PartitionKeyRange);
        public static readonly ResourceOperation Resume = new ResourceOperation(OperationType.Resume, ResourceType.Replica);
        public static readonly ResourceOperation Stop = new ResourceOperation(OperationType.Stop, ResourceType.Replica);
        public static readonly ResourceOperation Recycle = new ResourceOperation(OperationType.Recycle, ResourceType.Replica);
        
        // TODO: Add following for master partition once we support master partition migration.
        public static readonly ResourceOperation AbortServerPartitionMigration = new ResourceOperation(OperationType.AbortPartitionMigration, ResourceType.ServerPartition);
        public static readonly ResourceOperation CompleteServerPartitionMigration = new ResourceOperation(OperationType.CompleteSplit, ResourceType.ServerPartition);
        
        public static readonly ResourceOperation WatchdogCreateModuleCommand = new ResourceOperation(OperationType.Create, ResourceType.ModuleCommand);
        public static readonly ResourceOperation WatchdogReadModuleCommand = new ResourceOperation(OperationType.Read, ResourceType.ModuleCommand);

        public static readonly ResourceOperation WildMemoryAccess = new ResourceOperation(OperationType.Crash, ResourceType.Replica);
#endif

        public static readonly ResourceOperation XDeleteConflict = new ResourceOperation(OperationType.Delete, ResourceType.Conflict);
        public static readonly ResourceOperation XReadConflict = new ResourceOperation(OperationType.Read, ResourceType.Conflict);
        public static readonly ResourceOperation XReadConflictFeed = new ResourceOperation(OperationType.ReadFeed, ResourceType.Conflict);

        public static readonly ResourceOperation XReadRecordFeed = new ResourceOperation(OperationType.ReadFeed, ResourceType.Record);
        public static readonly ResourceOperation XCreateRecord = new ResourceOperation(OperationType.Create, ResourceType.Record);
        public static readonly ResourceOperation XReadRecord = new ResourceOperation(OperationType.Read, ResourceType.Record);
        public static readonly ResourceOperation XUpdateRecord = new ResourceOperation(OperationType.Replace, ResourceType.Record);
        public static readonly ResourceOperation XDeleteRecord = new ResourceOperation(OperationType.Delete, ResourceType.Record);

        public static readonly ResourceOperation XXCreateTrigger = new ResourceOperation(OperationType.Create, ResourceType.Trigger);
        public static readonly ResourceOperation XXUpsertTrigger = new ResourceOperation(OperationType.Upsert, ResourceType.Trigger);
        public static readonly ResourceOperation XXReplaceTrigger = new ResourceOperation(OperationType.Replace, ResourceType.Trigger);
        public static readonly ResourceOperation XXDeleteTrigger = new ResourceOperation(OperationType.Delete, ResourceType.Trigger);
        public static readonly ResourceOperation XXReadTrigger = new ResourceOperation(OperationType.Read, ResourceType.Trigger);
        public static readonly ResourceOperation XXReadTriggerFeed = new ResourceOperation(OperationType.ReadFeed, ResourceType.Trigger);

        public static readonly ResourceOperation XXCreateUserDefinedFunction = new ResourceOperation(OperationType.Create, ResourceType.UserDefinedFunction);
        public static readonly ResourceOperation XXUpsertUserDefinedFunction = new ResourceOperation(OperationType.Upsert, ResourceType.UserDefinedFunction);
        public static readonly ResourceOperation XXReplaceUserDefinedFunction = new ResourceOperation(OperationType.Replace, ResourceType.UserDefinedFunction);
        public static readonly ResourceOperation XXDeleteUserDefinedFunction = new ResourceOperation(OperationType.Delete, ResourceType.UserDefinedFunction);
        public static readonly ResourceOperation XXReadUserDefinedFunction = new ResourceOperation(OperationType.Read, ResourceType.UserDefinedFunction);
        public static readonly ResourceOperation XXReadUserDefinedFunctionFeed = new ResourceOperation(OperationType.ReadFeed, ResourceType.UserDefinedFunction);

        public static readonly ResourceOperation ReadSchema = new ResourceOperation(OperationType.Read, ResourceType.Schema);
        public static readonly ResourceOperation ReadSchemaFeed = new ResourceOperation(OperationType.ReadFeed, ResourceType.Schema);

        public static readonly ResourceOperation CompleteUserTransaction = new ResourceOperation(OperationType.CompleteUserTransaction, ResourceType.Transaction);

#if !COSMOSCLIENT
        public static readonly ResourceOperation ReadRestoreMetadataFeed = new ResourceOperation(OperationType.ReadFeed, ResourceType.RestoreMetadata);
#endif

        public static ResourceOperation Query(OperationType operationType, ResourceType resourceType)
        {
            Debug.Assert(
                operationType == OperationType.SqlQuery ||
                operationType == OperationType.Query);

            return new ResourceOperation(operationType, resourceType);
        }

        public override string ToString()
        {
            return String.Format(
                CultureInfo.InvariantCulture,
                "(operationType: {0}, resourceType: {1})",
                operationType,
                resourceType);
        }
    }
}
