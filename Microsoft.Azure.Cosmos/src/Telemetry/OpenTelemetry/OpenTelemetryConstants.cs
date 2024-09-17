// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.OpenTelemetry
{
    internal class OpenTelemetryConstants
    {
        public static class Operations
        {
            // Batch/Bulk Operations
            public const string ExecuteBatch = "execute_batch";
            public const string ExecuteBulkPrefix = "bulk_";

            // Change feed operations
            public const string QueryChangeFeed = "query_change_feed";
            public const string QueryChangeFeedEstimator = "get_change_feed_processor_estimate";

            // Account Operations
            public const string ReadAccount = "read_account";

            // Conflict Operations
            public const string DeleteConflict = "delete_conflict";
            public const string QueryConflicts = "query_conflicts";
            public const string ReadConflict = "read_conflict";

            //Container Operations
            public const string CreateContainer = "create_container";
            public const string CreateContainerIfNotExists = "create_container_if_not_exists";
            public const string DeleteContainer = "delete_container";
            public const string ReadContainer = "read_container";
            public const string ReplaceContainer = "replace_container";
            public const string ReadFeedRanges = "read_feed_ranges";
            public const string ReadPartitionKeyRanges = "read_partition_key_ranges";

            // Database Operations
            public const string CreateDatabase = "create_database";
            public const string CreateDatabaseIfNotExists = "create_database_if_not_exists";
            public const string DeleteDatabase = "delete_database";
            public const string ReadDatabase = "read_database";

            // Item Operations
            public const string CreateItem = "create_item";
            public const string DeleteAllItemsByPartitionKey = "delete_all_items_by_partition_key";
            public const string DeleteItem = "delete_item";
            public const string PatchItem = "patch_item";
            public const string QueryItems = "query_items";
            public const string ReadManyItems = "read_many_items";
            public const string ReadItem = "read_item";
            public const string ReplaceItem = "replace_item";
            public const string UpsertItem = "upsert_item";

            // Permission operations
            public const string CreatePermission = "create_permission";
            public const string DeletePermission = "delete_permission";
            public const string ReadPermission = "read_permission";
            public const string ReplacePermission = "replace_permission";
            public const string UpsertPermission = "upsert_permission";

            // Stored procedure operations
            public const string CreateStoredProcedure = "create_stored_procedure";
            public const string DeleteStoreProcedure = "delete_stored_procedure";
            public const string ExecuteStoredProcedure = "execute_stored_procedure";
            public const string ReadStoredProcedure = "read_stored_procedure";
            public const string ReplaceStoredProcedure = "replace_stored_procedure";

            // Throughput operations
            public const string ReadThroughput = "read_throughput";
            public const string ReadThroughputIfExists = "read_throughput_if_exists";
            public const string ReplaceThroughput = "replace_throughput";
            public const string ReplaceThroughputIfExists = "replace_throughput_if_exists";

            // Trigger operations
            public const string CreateTrigger = "create_trigger";
            public const string DeleteTrigger = "delete_trigger";
            public const string ReadTrigger = "read_trigger";
            public const string ReplaceTrigger = "replace_trigger";

            // User operations
            public const string CreateUser = "create_user";
            public const string DeleteUser = "delete_user";
            public const string ReadUser = "read_user";
            public const string ReplaceUser = "replace_user";
            public const string UpsertUser = "upsert_user";

            // User-defined function operations
            public const string CreateUserDefinedFunction = "create_user_defined_function";
            public const string DeleteUserDefinedFunctions = "delete_user_defined_function";
            public const string ReplaceUserDefinedFunctions = "replace_user_defined_function";
            public const string ReadAllUserDefinedFunctions = "read_all_user_defined_functions";
            public const string ReadUserDefinedFunction = "read_user_defined_function";

            // Encryption Key operations
            public const string CreateClientEncryptionKey = "create_client_encryption_key";
            public const string ReadClientEncryptionKey = "read_client_encryption_key";
            public const string ReplaceClientEncryptionKey = "replace_client_encryption_key";
        }
    }
}
