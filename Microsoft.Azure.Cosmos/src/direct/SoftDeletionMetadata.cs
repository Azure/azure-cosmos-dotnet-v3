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
#pragma warning disable SA1507 // Code should not contain multiple blank lines in a row


        /// <summary>
        /// Property to indicate Database Account Soft Deletion Start Timestamp.
        /// </summary>
        [JsonConverter(typeof(IsoDateTimeConverter))]
#pragma warning restore SA1507 // Code should not contain multiple blank lines in a row
        [JsonProperty(PropertyName = Constants.SoftDeletionMetadataProperties.SoftDeletionStartTimestampUtc)]
        public DateTime SoftDeletionStartTimestampUtc
        {
            get
            {
                return base.GetValue<DateTime>(Constants.SoftDeletionMetadataProperties.SoftDeletionStartTimestampUtc).ToUniversalTime();
            }
            set
            {
                this.SetValue(Constants.SoftDeletionMetadataProperties.SoftDeletionStartTimestampUtc, value.ToUniversalTime());
            }
        }

        /// <summary>
        /// Property to indicate Database Account Soft Deletion Expiration Timestamp.
        /// </summary>
        [JsonConverter(typeof(IsoDateTimeConverter))]
        [JsonProperty(PropertyName = Constants.SoftDeletionMetadataProperties.SoftDeletionResourceExpirationTimestampUtc)]
        public DateTime SoftDeletionResourceExpirationTimestampUtc
        {
            get
            {
                return base.GetValue<DateTime>(Constants.SoftDeletionMetadataProperties.SoftDeletionResourceExpirationTimestampUtc).ToUniversalTime();
            }
            set
            {
                this.SetValue(Constants.SoftDeletionMetadataProperties.SoftDeletionResourceExpirationTimestampUtc, value.ToUniversalTime());
            }
        }
    }
}
