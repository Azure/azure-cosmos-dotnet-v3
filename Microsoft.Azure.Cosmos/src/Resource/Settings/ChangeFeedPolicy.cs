//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents the change feed policy configuration for a container in the Azure Cosmos DB service.
    /// </summary> 
    /// <example>
    /// The example below creates a new container with a custom change feed policy.
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
    }
}
