// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Globalization;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

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
    /// ThroughputProperties throughputProperties = await testContainer.ReadThroughputAsync().Resource;
    /// ]]>
    /// </code>
    /// </example>
    public class ThroughputProperties
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ThroughputProperties"/> class for the Azure Cosmos DB service.
        /// </summary>
        public ThroughputProperties()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ThroughputProperties"/> class,
        /// use for mocking in unit testing.
        /// </summary>
        /// <param name="eTag"></param>
        /// <param name="lastModified"></param>
        protected ThroughputProperties(string eTag, DateTime lastModified)
        {
            this.ETag = eTag;
            this.LastModified = lastModified;
        }

        /// <summary>
        /// Gets the entity tag associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The entity tag associated with the resource.
        /// </value>
        /// <remarks>
        /// ETags are used for concurrency checking when updating resources.
        /// </remarks>
        [JsonProperty(PropertyName = Constants.Properties.ETag)]
        public string ETag { get; private set; }

        /// <summary>
        /// Gets the last modified timestamp associated with <see cref="DatabaseProperties" /> from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The last modified timestamp associated with the resource.</value>
        [JsonConverter(typeof(UnixDateTimeConverter))]
        [JsonProperty(PropertyName = Constants.Properties.LastModified)]
        public DateTime LastModified { get; private set; }

        /// <summary>
        /// Gets the provisioned throughput for a resource in measurement of request units per second in the Azure Cosmos service.
        /// </summary>
        public int? Throughput
        {
            get
            {
                return this.Content.OfferThroughput;
            }
            set
            {
                this.Content = new ThroughputContent(value);
            }
        }

        /// <summary>
        /// Gets the offer rid.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.RId)]
        internal string OfferRID { get; private set; }

        /// <summary>
        /// Gets the resource rid.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.OfferResourceId)]
        internal string ResourceRID { get; private set; }

        [JsonProperty(PropertyName = "content", DefaultValueHandling = DefaultValueHandling.Ignore)]
        private ThroughputContent Content { get; set; }

        private sealed class ThroughputContent : JsonSerializable
        {
            internal ThroughputContent()
            {
            }

            internal ThroughputContent(int? offerThroughput)
            {
                this.OfferThroughput = offerThroughput;
            }

            [JsonProperty(PropertyName = "offerThroughput", DefaultValueHandling = DefaultValueHandling.Ignore)]
            internal int? OfferThroughput { get; set; }
        }
    }
}
