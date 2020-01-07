//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Azure.Cosmos.ChangeFeed
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text.Json.Serialization;

    [Serializable]
    internal sealed class DocumentServiceLeaseCore : DocumentServiceLease
    {
        private static readonly DateTime UnixStartTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        public DocumentServiceLeaseCore()
        {
        }

        public DocumentServiceLeaseCore(DocumentServiceLeaseCore other)
        {
            this.LeaseId = other.LeaseId;
            this.LeaseToken = other.LeaseToken;
            this.Owner = other.Owner;
            this.ContinuationToken = other.ContinuationToken;
            this.ETag = other.ETag;
            this.TS = other.TS;
            this.ExplicitTimestamp = other.ExplicitTimestamp;
            this.Properties = other.Properties;
        }

        [JsonPropertyName("id")]
        public string LeaseId { get; set; }

        [JsonIgnore]
        public override string Id => this.LeaseId;

        [JsonPropertyName("_etag")]
        public string ETag { get; set; }

        [JsonPropertyName("LeaseToken")]
        public string LeaseToken { get; set; }

        [JsonPropertyName("PartitionId")]
        private string PartitionId
        {
            set
            {
                this.LeaseToken = value;
            }
        }

        [JsonIgnore]
        public override string CurrentLeaseToken => this.LeaseToken;

        [JsonPropertyName("Owner")]
        public override string Owner { get; set; }

        /// <summary>
        /// Gets or sets the current value for the offset in the stream.
        /// </summary>
        [JsonPropertyName("ContinuationToken")]
        public override string ContinuationToken { get; set; }

        [JsonIgnore]
        public override DateTime Timestamp
        {
            get { return this.ExplicitTimestamp ?? UnixStartTime.AddSeconds(this.TS); }
            set { this.ExplicitTimestamp = value; }
        }

        [JsonIgnore]
        public override string ConcurrencyToken => this.ETag;

        [JsonPropertyName("properties")]
        public override Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();

        [JsonPropertyName("timestamp")]
        private DateTime? ExplicitTimestamp { get; set; }

        [JsonPropertyName("_ts")]
        private long TS { get; set; }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} Owner='{1}' Continuation={2} Timestamp(local)={3} Timestamp(server)={4}",
                this.Id,
                this.Owner,
                this.ContinuationToken,
                this.Timestamp.ToUniversalTime(),
                UnixStartTime.AddSeconds(this.TS).ToUniversalTime());
        }
    }
}