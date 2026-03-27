//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    internal enum OperationType
    {
        // Keep in sync with RequestOperationType enum in backend native.
        Invalid = -1,
        Create = 0,
        Patch = 1,
        Read = 2,
        ReadFeed = 3,
        Delete = 4,
        Replace = 5,
#if !COSMOSCLIENT
        Pause = 6,
        Resume = 7,
        Stop = 8,
#endif
        Execute = 9,
#if !COSMOSCLIENT
        Recycle = 10,
        Crash = 11,
        FanoutDelete = 12,
#endif
        BatchApply = 13,
        SqlQuery = 14,
        Query = 15,
#if !COSMOSCLIENT
        BindReplica = 16,
        JSQuery = 17,
#endif
        Head = 18,
        HeadFeed = 19,
        Upsert = 20,
#if !COSMOSCLIENT
        Recreate = 21,
        Throttle = 22,
        GetSplitPoint = 23,
        PreCreateValidation = 24,
        ApplyTransactionLogs = 25,
        Relocate = 26,
        AbortSplit = 27,
        CompleteSplit = 28,
        WriteValue = 29,
        CompletePartitionMigration = 30,
        AbortPartitionMigration = 31,
        OfferUpdateOperation = 32,
        OfferPreGrowValidation = 33,
        BatchReportThroughputUtilization = 34,
        PreReplaceValidation = 35,
        MigratePartition = 36,
#endif
        AddComputeGatewayRequestCharges = 37,
#if !COSMOSCLIENT
        MasterReplaceOfferOperation = 38,
        ProvisionedCollectionOfferUpdateOperation = 39,
#endif
        Batch = 40,
        QueryPlan = 41,

#if !COSMOSCLIENT
        InitiateDatabaseOfferPartitionShrink = 42,
        CompleteDatabaseOfferPartitionShrink = 43,
        EnsureSnapshotOperation = 44,
        GetSplitPoints = 45,
        // AddLogStoreCharge=46
#endif

#if !COSMOSCLIENT
        CompleteMergeOnTarget = 47,
        CompleteMergeOnMaster = 48,
        AbortMergeOnTarget = 49,
        AbortMergeOnMaster = 50,
#endif

#if !COSMOSCLIENT
        ForcePartitionBackup = 51,
#endif

        CompleteUserTransaction = 52,

#if !COSMOSCLIENT
        SystemOperation = 53,
#endif
        MetadataCheckAccess = 54,
#if !COSMOSCLIENT
        Prune = 55,
        CreateSystemSnapshot = 56,
#endif

        CollectionTruncate = 57,

#if !COSMOSCLIENT
        UpdateFailoverPriorityList = 58,
#endif

#if !COSMOSCLIENT
        GetStorageAuthToken = 59,
        CreateClientEncryptionKey = 60,
        ReplaceClientEncryptionKey = 61,
        UpdatePartitionThroughput = 62,

        // Operation type for recreating RidRange resources during the pitr restore of a multi master partition
        CreateRidRangeResources = 64,
        Truncate = 65,
        QueryStoredProc = 66,
        Other = 67,
        Count = 68,

        RelocateLeakedTentativeWrites = 70,

        // Operation type for checking if the backend is able to serve an external backup request.
        ExternalPreBackup = 71,

        // Operation type for uploading external backups
        ExternalBackup = 72,

        // Operation type for checking external backup status
        CheckExternalBackupStatus = 73,

        // Operation type for restoring external backups
        ExternalBackupRestore = 74,

        // Operation type for checking external backup restore status
        CheckExternalBackupRestoreStatus = 75,

        // Distributed transaction operations
        PrepareDistributedTransaction = 76, //this will not be used by Client SDKs
#endif
        CommitDistributedTransaction = 77,
        AbortDistributedTransaction = 78,

#if !COSMOSCLIENT
        // Operation type for cancelling external backup
        CancelExternalBackup = 79,
#endif

#if !COSMOSCLIENT
        // Operation type for cancelling external backup restore
        CancelExternalBackupRestore = 80,
#endif

        // Add new operation types above this
        Last = 81,

        // These names make it unclear what they map to in RequestOperationType.
        ExecuteJavaScript = -2,
        GetConfiguration = -8,
#if !COSMOSCLIENT
        ForceConfigRefresh = -3,
        ReportThroughputUtilization = -4,
        ServiceReservation = -5,
        ControllerBatchReportCharges = -6,
        ControllerBatchGetOutput = -7,
        GetStorageAccountKey = -9,
        GetFederationConfigurations = -10,
        GetDatabaseAccountConfigurations = -11,
        GetUnwrappedDek = -12,
        ReadReplicaFromMasterPartition = -13,
        ReadReplicaFromServerPartition = -14,
        MasterInitiatedProgressCoordination = -15,
        GetAadGroups = -16,
        GetStorageAccountSas = -17,
        GetStorageServiceConfigurations = -18,
        GetGraphDatabaseAccountConfiguration = -19,
        GetCustomerManagedKeyStatus = -20,
        GetBatchCustomerManagedKeyStatus = -21,
        XPDatabaseAccountMetaData = -22,
        ControllerBatchAutoscaleRUsConsumption = -23,
        ControllerBatchGetAutoscaleAggregateOutput = -24,
        GetDekProperties = -25,
        ControllerBatchReportChargesV2 = -26,
        ControllerBatchGetOutputV2 = -27,
        ControllerBatchWatchdogHealthCheckPing = -28,
        GetDatabaseAccountArtifactPermissions = -29,
        GetRegionalConfigurations = -30,
        GetAzureRbacAccessCheck = -31,
#endif
    }

    internal static class OperationTypeExtensions
    {
        private static readonly Dictionary<int, string> OperationTypeNames = new Dictionary<int, string>();

        private static readonly Dictionary<string, OperationType> OperationTypesMapping = new Dictionary<string, OperationType>();

#if !COSMOSCLIENT
        internal static readonly List<OperationType> operationTypesToSkipWriteBarrierCheck = new List<OperationType>() { OperationType.ForceConfigRefresh, OperationType.GetSplitPoint };
#endif

        static OperationTypeExtensions()
        {
            foreach (OperationType type in Enum.GetValues(typeof(OperationType)))
            {
                OperationTypeExtensions.OperationTypeNames[(int)type] = type.ToString();
            }

            OperationTypeExtensions.OperationTypesMapping.Add("PUT", OperationType.Patch);
            OperationTypeExtensions.OperationTypesMapping.Add("GET", OperationType.Read);
            OperationTypeExtensions.OperationTypesMapping.Add("DELETE", OperationType.Delete);
            OperationTypeExtensions.OperationTypesMapping.Add("POST", OperationType.Create);

            OperationTypeExtensions.OperationTypesMapping.Add("Update", OperationType.Patch);
            OperationTypeExtensions.OperationTypesMapping.Add("Insert", OperationType.Patch);

            OperationTypeExtensions.OperationTypesMapping.Add("CreateJob", OperationType.Create);
            OperationTypeExtensions.OperationTypesMapping.Add("GetJob", OperationType.Read);
            OperationTypeExtensions.OperationTypesMapping.Add("ListJobs", OperationType.Read);
            OperationTypeExtensions.OperationTypesMapping.Add("UpdateJob", OperationType.Patch);
            OperationTypeExtensions.OperationTypesMapping.Add("DeleteJob", OperationType.Delete);
            OperationTypeExtensions.OperationTypesMapping.Add("GetClusterUsage", OperationType.Read);
        }

        public static string ToOperationTypeString(this OperationType type)
        {
            return OperationTypeExtensions.OperationTypeNames[(int)type];
        }

        public static OperationType ToOperationType(string type)
        {
            OperationType toReturn = OperationType.Invalid;

            if (!ToOperationType(type, out toReturn))
            {
                toReturn = OperationType.Invalid;
            }

            return toReturn;
        }

        public static bool ToOperationType(string type, out OperationType operationType)
        {
            operationType = OperationType.Invalid;

            if (!Enum.TryParse<OperationType>(type, out operationType))
            {
                if (!OperationTypeExtensions.OperationTypesMapping.TryGetValue(type, out operationType))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IsWriteOperation(this OperationType type)
        {
            return type == OperationType.Create ||
                   type == OperationType.Patch ||
                   type == OperationType.Delete ||
                   type == OperationType.Replace ||
                   type == OperationType.ExecuteJavaScript ||
                   type == OperationType.BatchApply ||
                   type == OperationType.Batch ||
                   type == OperationType.Upsert ||
                   type == OperationType.CompleteUserTransaction ||
                   type == OperationType.AbortDistributedTransaction ||
                   type == OperationType.CommitDistributedTransaction
#if !COSMOSCLIENT
                   ||
                   type == OperationType.MasterInitiatedProgressCoordination ||
                   type == OperationType.Recreate ||
                   type == OperationType.GetSplitPoint ||
                   type == OperationType.AbortSplit ||
                   type == OperationType.CompleteSplit ||
                   type == OperationType.CompleteMergeOnMaster ||
                   type == OperationType.CompleteMergeOnTarget ||
                   type == OperationType.PreReplaceValidation ||
                   type == OperationType.ReportThroughputUtilization ||
                   type == OperationType.BatchReportThroughputUtilization ||
                   type == OperationType.OfferUpdateOperation ||
                   type == OperationType.CompletePartitionMigration ||
                   type == OperationType.AbortPartitionMigration ||
                   type == OperationType.MigratePartition ||
                   type == OperationType.ForceConfigRefresh ||
                   type == OperationType.MasterReplaceOfferOperation || 
                   type == OperationType.InitiateDatabaseOfferPartitionShrink ||
                   type == OperationType.CompleteDatabaseOfferPartitionShrink ||
                   type == OperationType.EnsureSnapshotOperation ||
                   type == OperationType.GetSplitPoints ||
                   type == OperationType.ForcePartitionBackup ||
                   type == OperationType.CreateSystemSnapshot ||
                   type == OperationType.CreateRidRangeResources ||
                   type == OperationType.UpdateFailoverPriorityList ||
                   type == OperationType.Pause ||
                   type == OperationType.Resume ||
                   type == OperationType.UpdatePartitionThroughput ||
                   type == OperationType.Truncate ||
                   type == OperationType.RelocateLeakedTentativeWrites ||
                   type == OperationType.ExternalBackup ||
                   type == OperationType.ExternalBackupRestore ||
                   type == OperationType.PrepareDistributedTransaction ||
                   type == OperationType.CancelExternalBackup ||
                   type == OperationType.CancelExternalBackupRestore
#endif
                   ;
        }

        public static bool IsPointOperation(this OperationType type)
        {
            return type == OperationType.Create ||
                    type == OperationType.Delete ||
                    type == OperationType.Read ||
                    type == OperationType.Patch ||
                    type == OperationType.Upsert ||
                    type == OperationType.Replace;
        }

        public static bool IsReadOperation(this OperationType type)
        {
            return type == OperationType.Read ||
                   type == OperationType.ReadFeed ||
                   type == OperationType.Query ||
                   type == OperationType.SqlQuery ||
                   type == OperationType.Head ||
                   type == OperationType.HeadFeed ||
                   type == OperationType.MetadataCheckAccess ||
                   type == OperationType.QueryPlan
#if !COSMOSCLIENT
                   ||
                   type == OperationType.GetStorageAuthToken ||
                   type == OperationType.ExternalPreBackup ||
                   type == OperationType.CheckExternalBackupStatus ||
                   type == OperationType.CheckExternalBackupRestoreStatus
#endif
                   ;
        }

        public static bool IsSkippedForWriteBarrier(this OperationType type)
        {
            return
#if !COSMOSCLIENT
                OperationTypeExtensions.operationTypesToSkipWriteBarrierCheck.Contains(type) ||
#endif
             false;
        }
    }
}
