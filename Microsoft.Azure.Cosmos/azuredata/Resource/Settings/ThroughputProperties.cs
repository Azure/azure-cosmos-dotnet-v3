// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Represents a throughput of the resources in the Azure Cosmos DB service.
    /// It is the standard pricing for the resource in the Azure Cosmos DB service.
    /// </summary>
    /// <remarks>
    /// It contains provisioned container throughput in measurement of request units per second in the Azure Cosmos service.
    /// Refer to http://azure.microsoft.com/documentation/articles/documentdb-performance-levels/ for details on provision offer throughput.
    /// </remarks>
    /// <example>
    /// The example below fetch the ThroughputProperties on testContainer.
    /// <code language="c#">
    /// <![CDATA[ 
    /// ThroughputProperties throughputProperties = await testContainer.ReadThroughputAsync().Value;
    /// ]]>
    /// </code>
    /// </example>
    public class ThroughputProperties
    {
        /// <summary>
        /// Gets the entity tag associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The entity tag associated with the resource.
        /// </value>
        /// <remarks>
        /// ETags are used for concurrency checking when updating resources.
        /// </remarks>
        public ETag? ETag { get; internal set; }

        /// <summary>
        /// Gets the last modified time stamp associated with <see cref="DatabaseProperties" /> from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The last modified time stamp associated with the resource.</value>
        public DateTime? LastModified { get; internal set; }

        /// <summary>
        /// Gets the provisioned throughput for a resource in measurement of request units per second in the Azure Cosmos service.
        /// </summary>
        public int? Throughput
        {
            get => this.Content.OfferThroughput;
            private set => this.Content = new OfferContentV2(value.Value);
        }

        /// <summary>
        /// Gets the offer rid.
        /// </summary>
        internal string OfferRID { get; set; }

        /// <summary>
        /// Gets the resource rid.
        /// </summary>
        internal string ResourceRID { get; set; }

        internal OfferContentV2 Content { get; set; }
    }
}
