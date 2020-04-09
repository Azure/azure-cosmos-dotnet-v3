// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Runtime.CompilerServices;
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
        /// Default constructor used for serialization and unit tests
        /// </summary>
        public ThroughputProperties()
        {
        }

        /// <summary>
        /// Create a instance for fixed throughput
        /// </summary>
        private ThroughputProperties(OfferContentProperties offerContentProperties)
        {
            this.OfferVersion = Constants.Offers.OfferVersion_V2;
            this.Content = offerContentProperties;
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
        [JsonProperty(PropertyName = Constants.Properties.ETag, NullValueHandling = NullValueHandling.Ignore)]
        public string ETag { get; private set; }

        /// <summary>
        /// Gets the last modified time stamp associated with <see cref="DatabaseProperties" /> from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The last modified time stamp associated with the resource.</value>
        [JsonConverter(typeof(UnixDateTimeConverter))]
        [JsonProperty(PropertyName = Constants.Properties.LastModified, NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? LastModified { get; private set; }

        /// <summary>
        /// Gets the provisioned throughput for a resource in measurement of request units per second in the Azure Cosmos service.
        /// </summary>
        [JsonIgnore]
        public int? Throughput
        {
            get => this.Content?.OfferThroughput;
            private set => this.Content = OfferContentProperties.CreateFixedOfferConent(value.Value);
        }

        /// <summary>
        /// The maximum throughput the autopilot will scale to.
        /// </summary>
        [JsonIgnore]
#if INTERNAL
        public
#else
        internal
#endif
        int? MaxThroughput
        {
            get => this.Content?.OfferAutopilotSettings?.MaxThroughput;
        }

        /// <summary>
        /// The amount to increment if the maximum RUs is getting throttled.
        /// </summary>
        [JsonIgnore]
#if INTERNAL
        public
#else
        internal
#endif
        int? AutoUpgradeMaxThroughputIncrementPercentage
        {
            get => this.Content?.OfferAutopilotSettings?.AutopilotAutoUpgradeProperties?.ThroughputProperties?.IncrementPercent;
        }

        /// <summary>
        /// The Throughput properties for autoscale provisioned throughput offering
        /// </summary>
        /// <param name="maxThroughput">The maximum throughput the resource can scale to.</param>
#if INTERNAL
        public
#else
        internal
#endif
        static ThroughputProperties CreateAutoScaleProvionedThroughput(int maxThroughput)
        {
           return new ThroughputProperties(OfferContentProperties.CreateAutoscaleOfferConent(maxThroughput));
        }

        /// <summary>
        /// The Throughput properties for autoscale provisioned throughput offering
        /// </summary>
        /// <param name="throughput">The current provisioned throughput for the resource.</param>
#if INTERNAL
        public
#else
        internal
#endif
        static ThroughputProperties CreateFixedThroughput(int throughput)
        {
            return new ThroughputProperties(OfferContentProperties.CreateFixedOfferConent(throughput));
        }

        /// <summary>
        /// The Throughput properties for autoscale provisioned throughput offering
        /// </summary>
        /// <param name="startingMaxThroughput">The maximum throughput the resource can scale to.</param>
        /// <param name="autoUpgradeMaxThroughputIncrementPercentage">The percentage to increase the maximum value if the maximum is being throttled.</param>
#if INTERNAL
        public
#else
        internal
#endif
        static ThroughputProperties CreateAutoScaleProvionedThroughput(
            int startingMaxThroughput,
            int autoUpgradeMaxThroughputIncrementPercentage)
        {
            return new ThroughputProperties(OfferContentProperties.CreateAutoPilotOfferConent(
                startingMaxThroughput,
                autoUpgradeMaxThroughputIncrementPercentage));
        }

        /// <summary>
        /// The AutopilotThroughputProperties constructor
        /// </summary>
        /// <param name="startingMaxThroughput">The maximum throughput the resource can scale to.</param>
        /// <param name="autoUpgradeMaxThroughputIncrementPercentage">This scales the maximum throughput by the percentage if maximum throughput is not enough</param>
        private ThroughputProperties(
            int startingMaxThroughput,
            int autoUpgradeMaxThroughputIncrementPercentage)
        {
            this.Content = OfferContentProperties.CreateAutoPilotOfferConent(
                startingMaxThroughput,
                autoUpgradeMaxThroughputIncrementPercentage);
        }

        /// <summary>
        /// Gets the self-link associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The self-link associated with the resource.</value> 
        /// <remarks>
        /// A self-link is a static addressable Uri for each resource within a database account and follows the Azure Cosmos DB resource model.
        /// E.g. a self-link for a document could be dbs/db_resourceid/colls/coll_resourceid/documents/doc_resourceid
        /// </remarks>
        [JsonProperty(PropertyName = Constants.Properties.SelfLink, NullValueHandling = NullValueHandling.Ignore)]
        public string SelfLink { get; private set; }

        /// <summary>
        /// Gets the offer rid.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.RId, NullValueHandling = NullValueHandling.Ignore)]
        internal string OfferRID { get; private set; }

        /// <summary>
        /// Gets the resource rid.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.OfferResourceId, NullValueHandling = NullValueHandling.Ignore)]
        internal string ResourceRID { get; private set; }

        [JsonProperty(PropertyName = "content", DefaultValueHandling = DefaultValueHandling.Ignore)]
        internal OfferContentProperties Content { get; set; }

        /// <summary>
        /// Gets the version of this offer resource in the Azure Cosmos DB service.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.OfferVersion, DefaultValueHandling = DefaultValueHandling.Ignore)]
        internal string OfferVersion { get; private set; } 
    }
}
