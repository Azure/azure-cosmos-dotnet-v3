//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using System;
    using System.Collections.ObjectModel;
    using System.Globalization;

    /// <summary>
    /// Represents the change feed policy for a collection in the Azure Cosmos DB service.
    /// </summary>
    /// <seealso cref="Microsoft.Azure.Documents.DocumentCollection"/>
    internal sealed class ChangeFeedPolicy : JsonSerializable, ICloneable
    {
        /// <summary>
        /// Gets or sets a value that indicates for how long operation logs have to be retained.
        /// </summary>
        /// <value>
        /// Value is in TimeSpan. Any seconds will be ceiled as 1 minute.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.LogRetentionDuration)]
        public TimeSpan RetentionDuration
        {
            get
            {
                int retentionDurationInMinutes = base.GetValue<int>(Constants.Properties.LogRetentionDuration);
                return TimeSpan.FromMinutes(retentionDurationInMinutes);
            }
            set
            {
                TimeSpan retentionDuration = value;
                int retentionDurationInMinutes = ((int)retentionDuration.TotalMinutes) + (retentionDuration.Seconds > 0 ? 1 : 0);
                base.SetValue(Constants.Properties.LogRetentionDuration, retentionDurationInMinutes);
            }
        }

        /// <summary>
        /// Performs a deep copy of the operation log policy.
        /// </summary>
        /// <returns>
        /// A clone of the operation log policy.
        /// </returns>
        public object Clone()
        {
            ChangeFeedPolicy cloned = new ChangeFeedPolicy()
            {
                RetentionDuration = this.RetentionDuration
            };

            return cloned;
        }
    }
}
