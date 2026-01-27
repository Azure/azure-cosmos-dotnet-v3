//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Represents exported lease data for backup, restore, and migration scenarios.
    /// This class contains all metadata required to fully restore a lease to its previous state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When exporting leases, all current lease state including continuation tokens, 
    /// owner information, and timestamps are captured. The export format is designed 
    /// to be self-contained and can be used to import leases into a new lease container.
    /// </para>
    /// <para>
    /// During import, the ownership history is preserved and a new entry is added to 
    /// track the import action. The owner and timestamp are updated to reflect the 
    /// importing instance.
    /// </para>
    /// </remarks>
    public sealed class LeaseExportData
    {
        /// <summary>
        /// The version of the export format for forward compatibility.
        /// </summary>
        public const string CurrentExportVersion = "1.0";

        /// <summary>
        /// Initializes a new instance of the <see cref="LeaseExportData"/> class.
        /// </summary>
        public LeaseExportData()
        {
            this.OwnershipHistory = new List<LeaseOwnershipHistory>();
            this.Properties = new Dictionary<string, string>();
            this.ExportVersion = CurrentExportVersion;
        }

        /// <summary>
        /// Gets or sets the version of the export format.
        /// </summary>
        [JsonPropertyName("exportVersion")]
        public string ExportVersion { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier for the lease.
        /// </summary>
        [JsonPropertyName("leaseId")]
        public string LeaseId { get; set; }

        /// <summary>
        /// Gets or sets the lease token (partition identifier).
        /// </summary>
        [JsonPropertyName("leaseToken")]
        public string LeaseToken { get; set; }

        /// <summary>
        /// Gets or sets the partition key for the lease document.
        /// </summary>
        [JsonPropertyName("partitionKey")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string PartitionKey { get; set; }

        /// <summary>
        /// Gets or sets the current owner of the lease.
        /// </summary>
        [JsonPropertyName("owner")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Owner { get; set; }

        /// <summary>
        /// Gets or sets the continuation token for resuming change feed processing.
        /// </summary>
        [JsonPropertyName("continuationToken")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string ContinuationToken { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the lease.
        /// </summary>
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the feed range associated with this lease.
        /// </summary>
        [JsonPropertyName("feedRange")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string FeedRange { get; set; }

        /// <summary>
        /// Gets or sets the change feed mode (e.g., "LatestVersion", "AllVersionsAndDeletes").
        /// </summary>
        [JsonPropertyName("mode")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Mode { get; set; }

        /// <summary>
        /// Gets or sets the lease version (PartitionKeyRangeBasedLease or EPKRangeBasedLease).
        /// </summary>
        [JsonPropertyName("leaseVersion")]
        public string LeaseVersion { get; set; }

        /// <summary>
        /// Gets or sets custom properties associated with the lease.
        /// </summary>
        [JsonPropertyName("properties")]
        public Dictionary<string, string> Properties { get; set; }

        /// <summary>
        /// Gets or sets the history of ownership changes for this lease.
        /// </summary>
        [JsonPropertyName("ownershipHistory")]
        public List<LeaseOwnershipHistory> OwnershipHistory { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when this export was created.
        /// </summary>
        [JsonPropertyName("exportedAt")]
        public DateTime ExportedAt { get; set; }

        /// <summary>
        /// Gets or sets the name of the instance that created this export.
        /// </summary>
        [JsonPropertyName("exportedBy")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string ExportedBy { get; set; }

        /// <summary>
        /// Gets or sets the ETag/concurrency token at the time of export.
        /// </summary>
        /// <remarks>
        /// This is captured for informational purposes. During import, a new ETag will be generated.
        /// </remarks>
        [JsonPropertyName("concurrencyToken")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string ConcurrencyToken { get; set; }

        /// <summary>
        /// Returns a string representation of the lease export data.
        /// </summary>
        /// <returns>A string containing key lease information.</returns>
        public override string ToString()
        {
            return $"LeaseExportData[Id={this.LeaseId}, Token={this.LeaseToken}, Owner={this.Owner}, Mode={this.Mode}]";
        }
    }
}
