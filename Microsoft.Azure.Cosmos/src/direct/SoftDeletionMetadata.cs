//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Soft Deletion Metadata stored on the DatabaseAccountBackendResource / Database / DocumentCollection.
    /// This contains all the required metadata related to Soft Deletion.
    /// </summary>
    internal sealed class SoftDeletionMetadata : JsonSerializable
    {
        public SoftDeletionMetadata()
        {
        }

        /// <summary>
        /// Property to indicate Database Account is Soft Deleted.
        /// </summary>
        [JsonProperty(PropertyName = Constants.SoftDeletionMetadataProperties.IsSoftDeleted)]
        public bool IsSoftDeleted
        {
            get
            {
                return base.GetValue<bool>(Constants.SoftDeletionMetadataProperties.IsSoftDeleted);
            }
            set
            {
                this.SetValue(Constants.SoftDeletionMetadataProperties.IsSoftDeleted, value);
            }
        }


        /// <summary>
        /// Property to indicate Database Account Soft Deletion Start Timestamp in epoch format.
        /// </summary>
        [JsonProperty(PropertyName = Constants.SoftDeletionMetadataProperties.SoftDeletionStartTimestamp)]
        public long SoftDeletionStartTimestamp
        {
            get
            {
                return base.GetValue<long>(Constants.SoftDeletionMetadataProperties.SoftDeletionStartTimestamp);
            }
            set
            {
                this.SetValue(Constants.SoftDeletionMetadataProperties.SoftDeletionStartTimestamp, value);
            }
        }

        /// <summary>
        /// Property to indicate Database Account Soft Deletion Expiration Timestamp in epoch format.
        /// </summary>
        [JsonProperty(PropertyName = Constants.SoftDeletionMetadataProperties.SoftDeletionResourceExpirationTimestamp)]
        public long SoftDeletionResourceExpirationTimestamp
        {
            get
            {
                return base.GetValue<long>(Constants.SoftDeletionMetadataProperties.SoftDeletionResourceExpirationTimestamp);
            }
            set
            {
                this.SetValue(Constants.SoftDeletionMetadataProperties.SoftDeletionResourceExpirationTimestamp, value);
            }
        }

        public override string ToString()
        {
            return string.Format(
                "IsSoftDeleted: {0}, SoftDeletionStartTimestamp: {1}, SoftDeletionResourceExpirationTimestamp: {2}",
                this.IsSoftDeleted,
                this.SoftDeletionStartTimestamp,
                this.SoftDeletionResourceExpirationTimestamp);
        }
    }
}
