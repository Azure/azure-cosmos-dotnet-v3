﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Represents the change feed policy configuration for a container in the Azure Cosmos DB service.
    /// </summary> 
    /// <example>
    /// The example below creates a new container with a custom change feed policy for full fidelity change feed with a retention window of 5 minutes - so intermediary snapshots of changes as well as deleted documents would be
    /// available for processing for 5 minutes before they vanish. 
    /// Processing the change feed with <see cref="ChangeFeedMode.FullFidelity"/> will only be able within this retention window - if you attempt to process a change feed after more
    /// than the retention window (5 minutes in this sample) an error (Status Code 400) will be returned. 
    /// It would still be possible to process changes using <see cref="ChangeFeedMode.Incremental"/> mode even when configuring a full fidelity change
    /// feed policy with retention window on the container and when using Incremental mode it doesn't matter whether your are out of the retention window or not.
    /// <code language="c#">
    /// <![CDATA[
    ///     ContainerProperties containerProperties = new ContainerProperties("MyCollection", "/country");
    ///     containerProperties.ChangeFeedPolicy.FullFidelityRetention = TimeSpan.FromMinutes(5);
    ///     
    ///     CosmosContainerResponse containerCreateResponse = await client.GetDatabase("dbName").CreateContainerAsync(containerProperties, 5000);
    ///     ContainerProperties createdContainerProperties = containerCreateResponse.Container;
    /// ]]>
    /// </code>
    /// </example>
    /// <seealso cref="ContainerProperties"/>
#if PREVIEW
    public
#else
    internal
#endif
    sealed class ChangeFeedPolicy
    {
        [JsonProperty(PropertyName = Constants.Properties.LogRetentionDuration)]
        private int retentionDurationInMinutes = 0;

        /// <summary>
        /// Gets or sets a value that indicates for how long operation logs have to be retained.
        /// </summary>
        /// <remarks>
        /// Minimum granularity supported is minutes.
        /// </remarks>
        /// <value>
        /// Value is in TimeSpan.
        /// </value>
        [JsonIgnore]
        public TimeSpan FullFidelityRetention
        {
            get => TimeSpan.FromMinutes(this.retentionDurationInMinutes);
            set
            {
                if (value.Seconds > 0
                    || value.Milliseconds > 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(this.FullFidelityRetention), "Retention's granularity is minutes.");
                }

                if (value.TotalMilliseconds < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(this.FullFidelityRetention), "Retention cannot be negative.");
                }

                this.retentionDurationInMinutes = (int)value.TotalMinutes;
            }
        }

        /// <summary>
        /// Disables the retention log.
        /// </summary>
        public static TimeSpan FullFidelityNoRetention => TimeSpan.Zero;

        /// <summary>
        /// This contains additional values for scenarios where the SDK is not aware of new fields. 
        /// This ensures that if resource is read and updated none of the fields will be lost in the process.
        /// </summary>
        [JsonExtensionData]
        internal IDictionary<string, JToken> AdditionalProperties { get; private set; }

    }
}
