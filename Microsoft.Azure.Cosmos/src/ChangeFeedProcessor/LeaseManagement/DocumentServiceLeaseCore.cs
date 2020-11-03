//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    [Serializable]
    [JsonConverter(typeof(DocumentServiceLeaseDisabledConverter))]
    internal sealed class DocumentServiceLeaseCore : DocumentServiceLease
    {
        private static readonly DateTime UnixStartTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        // Used to detect if the user is migrating from a V2 CFP schema
        private bool isMigratingFromV2 = false;

        public DocumentServiceLeaseCore()
        {
        }

        [JsonProperty("id")]
        public string LeaseId { get; set; }

        [JsonProperty("version")]
        public DocumentServiceLeaseVersion Version => DocumentServiceLeaseVersion.PartitionKeyRangeBasedLease;

        [JsonIgnore]
        public override string Id => this.LeaseId;

        [JsonProperty("_etag")]
        public string ETag { get; set; }

        [JsonProperty("LeaseToken")]
        public string LeaseToken { get; set; }

        [JsonProperty("PartitionId", NullValueHandling = NullValueHandling.Ignore)]
        private string PartitionId
        {
            get
            {
                if (this.isMigratingFromV2)
                {
                    // If the user migrated the lease from V2 schema, we maintain the PartitionId property for backward compatibility
                    return this.LeaseToken;
                }

                return null;
            }
            set
            {
                this.LeaseToken = value;
                this.isMigratingFromV2 = true;
            }
        }

        [JsonIgnore]
        public override string CurrentLeaseToken => this.LeaseToken;

        [JsonProperty("FeedRange", NullValueHandling = NullValueHandling.Ignore)]
        public override FeedRangeInternal FeedRange { get; set; }

        [JsonProperty("Owner")]
        public override string Owner { get; set; }

        [JsonProperty("ContinuationToken")]
        public override string ContinuationToken { get; set; }

        [JsonIgnore]
        public override DateTime Timestamp
        {
            get => this.ExplicitTimestamp ?? UnixStartTime.AddSeconds(this.TS);
            set => this.ExplicitTimestamp = value;
        }

        [JsonIgnore]
        public override string ConcurrencyToken => this.ETag;

        [JsonProperty("properties")]
        public override Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();

        [JsonProperty("timestamp")]
        private DateTime? ExplicitTimestamp { get; set; }

        [JsonProperty("_ts")]
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