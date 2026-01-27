//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Represents a historical record of lease ownership, including the owner name and timestamp.
    /// This is used to track the history of lease ownership changes for auditing and debugging purposes.
    /// </summary>
    public sealed class LeaseOwnershipHistory
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LeaseOwnershipHistory"/> class.
        /// </summary>
        public LeaseOwnershipHistory()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LeaseOwnershipHistory"/> class.
        /// </summary>
        /// <param name="owner">The owner of the lease at this point in history.</param>
        /// <param name="timestamp">The timestamp when this ownership was recorded.</param>
        /// <param name="action">The action that occurred (e.g., "imported", "exported").</param>
        public LeaseOwnershipHistory(string owner, DateTime timestamp, string action)
        {
            this.Owner = owner;
            this.Timestamp = timestamp;
            this.Action = action;
        }

        /// <summary>
        /// Gets or sets the owner of the lease at this point in history.
        /// </summary>
        [JsonPropertyName("owner")]
        public string Owner { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when this ownership was recorded.
        /// </summary>
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the action that occurred (e.g., "acquired", "released", "imported", "exported").
        /// </summary>
        [JsonPropertyName("action")]
        public string Action { get; set; }
    }
}
