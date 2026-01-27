//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    /// <summary>
    /// Helper class for converting between <see cref="DocumentServiceLease"/> and <see cref="LeaseExportData"/>.
    /// </summary>
    internal static class LeaseExportHelper
    {
        /// <summary>
        /// Converts a <see cref="DocumentServiceLease"/> to a <see cref="LeaseExportData"/>.
        /// </summary>
        /// <param name="lease">The lease to convert.</param>
        /// <param name="exportedBy">The name of the instance performing the export.</param>
        /// <returns>The exported lease data.</returns>
        public static LeaseExportData ToExportData(DocumentServiceLease lease, string exportedBy)
        {
            if (lease == null)
            {
                throw new ArgumentNullException(nameof(lease));
            }

            LeaseExportData exportData = new LeaseExportData
            {
                LeaseId = lease.Id,
                LeaseToken = lease.CurrentLeaseToken,
                PartitionKey = lease.PartitionKey,
                Owner = lease.Owner,
                ContinuationToken = lease.ContinuationToken,
                Timestamp = lease.Timestamp,
                FeedRange = lease.FeedRange?.ToJsonString(),
                Mode = lease.Mode,
                ConcurrencyToken = lease.ConcurrencyToken,
                ExportedAt = DateTime.UtcNow,
                ExportedBy = exportedBy,
            };

            // Copy properties
            if (lease.Properties != null)
            {
                exportData.Properties = new Dictionary<string, string>(lease.Properties);
            }

            // Set lease version based on type
            if (lease is DocumentServiceLeaseCoreEpk)
            {
                exportData.LeaseVersion = nameof(DocumentServiceLeaseVersion.EPKRangeBasedLease);
            }
            else if (lease is DocumentServiceLeaseCore)
            {
                exportData.LeaseVersion = nameof(DocumentServiceLeaseVersion.PartitionKeyRangeBasedLease);
            }

            // Add current state to ownership history
            if (!string.IsNullOrEmpty(lease.Owner))
            {
                exportData.OwnershipHistory.Add(new LeaseOwnershipHistory(
                    owner: lease.Owner,
                    timestamp: lease.Timestamp,
                    action: "exported"));
            }

            return exportData;
        }

        /// <summary>
        /// Converts a <see cref="LeaseExportData"/> to a <see cref="DocumentServiceLease"/>.
        /// </summary>
        /// <param name="exportData">The exported lease data to convert.</param>
        /// <param name="importedBy">The name of the instance performing the import.</param>
        /// <returns>The document service lease.</returns>
        public static DocumentServiceLease FromExportData(LeaseExportData exportData, string importedBy)
        {
            if (exportData == null)
            {
                throw new ArgumentNullException(nameof(exportData));
            }

            DocumentServiceLease lease;

            // Create the appropriate lease type based on version
            if (exportData.LeaseVersion == nameof(DocumentServiceLeaseVersion.EPKRangeBasedLease))
            {
                DocumentServiceLeaseCoreEpk epkLease = new DocumentServiceLeaseCoreEpk
                {
                    LeaseId = exportData.LeaseId,
                    LeaseToken = exportData.LeaseToken,
                    LeasePartitionKey = exportData.PartitionKey,
                    Owner = importedBy, // Set new owner on import
                    ContinuationToken = exportData.ContinuationToken,
                    Mode = exportData.Mode,
                };

                // Parse feed range if present
                if (!string.IsNullOrEmpty(exportData.FeedRange) 
                    && FeedRangeInternal.TryParse(exportData.FeedRange, out FeedRangeInternal feedRange))
                {
                    epkLease.FeedRange = feedRange;
                }

                lease = epkLease;
            }
            else
            {
                DocumentServiceLeaseCore coreLeaseCore = new DocumentServiceLeaseCore
                {
                    LeaseId = exportData.LeaseId,
                    LeaseToken = exportData.LeaseToken,
                    LeasePartitionKey = exportData.PartitionKey,
                    Owner = importedBy, // Set new owner on import
                    ContinuationToken = exportData.ContinuationToken,
                    Mode = exportData.Mode,
                };

                // Parse feed range if present
                if (!string.IsNullOrEmpty(exportData.FeedRange)
                    && FeedRangeInternal.TryParse(exportData.FeedRange, out FeedRangeInternal feedRangeCore))
                {
                    coreLeaseCore.FeedRange = feedRangeCore;
                }

                lease = coreLeaseCore;
            }

            // Set timestamp to now for the import
            lease.Timestamp = DateTime.UtcNow;

            // Copy properties and add import metadata
            if (exportData.Properties != null)
            {
                lease.Properties = new Dictionary<string, string>(exportData.Properties);
            }
            else
            {
                lease.Properties = new Dictionary<string, string>();
            }

            // Store import metadata in properties
            lease.Properties["_importedAt"] = DateTime.UtcNow.ToString("o");
            lease.Properties["_importedBy"] = importedBy;
            lease.Properties["_importedFromOwner"] = exportData.Owner ?? string.Empty;
            lease.Properties["_exportedAt"] = exportData.ExportedAt.ToString("o");

            // Serialize ownership history to properties for persistence
            if (exportData.OwnershipHistory != null && exportData.OwnershipHistory.Count > 0)
            {
                // Add import action to history
                List<LeaseOwnershipHistory> updatedHistory = new List<LeaseOwnershipHistory>(exportData.OwnershipHistory);
                updatedHistory.Add(new LeaseOwnershipHistory(
                    owner: importedBy,
                    timestamp: DateTime.UtcNow,
                    action: "imported"));

                lease.Properties["_ownershipHistory"] = JsonSerializer.Serialize(updatedHistory);
            }

            return lease;
        }

        /// <summary>
        /// Retrieves ownership history from lease properties if available.
        /// </summary>
        /// <param name="lease">The lease to get history from.</param>
        /// <returns>The ownership history, or an empty list if none exists.</returns>
        public static List<LeaseOwnershipHistory> GetOwnershipHistory(DocumentServiceLease lease)
        {
            if (lease?.Properties != null && 
                lease.Properties.TryGetValue("_ownershipHistory", out string historyJson) &&
                !string.IsNullOrEmpty(historyJson))
            {
                try
                {
                    return JsonSerializer.Deserialize<List<LeaseOwnershipHistory>>(historyJson) 
                        ?? new List<LeaseOwnershipHistory>();
                }
                catch (JsonException)
                {
                    return new List<LeaseOwnershipHistory>();
                }
            }

            return new List<LeaseOwnershipHistory>();
        }
    }
}
