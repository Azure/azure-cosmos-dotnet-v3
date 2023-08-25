﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Newtonsoft.Json;

    /// <summary>
    /// Lease implementation for EPK based leases.
    /// </summary>
    [Serializable]
    internal sealed class DocumentServiceLeaseCoreEpk : DocumentServiceLease
    {
        private static readonly DateTime UnixStartTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        public DocumentServiceLeaseCoreEpk()
        {
        }

        [JsonProperty(IdPropertyName)]
        public string LeaseId { get; set; }

        [JsonProperty(LeasePartitionKeyPropertyName, NullValueHandling = NullValueHandling.Ignore)]
        public string LeasePartitionKey { get; set; }

        [JsonIgnore]
        public override string PartitionKey => this.LeasePartitionKey;

        [JsonProperty("version")]
        public DocumentServiceLeaseVersion Version => DocumentServiceLeaseVersion.EPKRangeBasedLease;

        [JsonIgnore]
        public override string Id => this.LeaseId;

        [JsonProperty("_etag")]
        public string ETag { get; set; }

        [JsonProperty("LeaseToken")]
        public string LeaseToken { get; set; }

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