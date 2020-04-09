// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using Newtonsoft.Json;

    /// <summary>
    /// Represents a throughput of the resources in the Azure Cosmos DB service.
    /// It is the standard pricing for the resource in the Azure Cosmos DB service.
    /// </summary>
    /// <remarks>
    /// It contains provisioned container throughput in measurement of request units per second in the Azure Cosmos service.
    /// Refer to http://azure.microsoft.com/documentation/articles/documentdb-performance-levels/ for details on provision offer throughput.
    /// </remarks>
#if INTERNAL
    public
#else
    internal
#endif
    class AutopilotThroughputProperties : ThroughputProperties
    {
        /// <summary>
        /// Default constructor for serialization
        /// </summary>
        [JsonConstructor]
        private AutopilotThroughputProperties()
        {
        }

        /// <summary>
        /// The AutopilotThroughputProperties constructor
        /// </summary>
        /// <param name="maxThroughput">The maximum throughput the resource can scale to.</param>
        public AutopilotThroughputProperties(int maxThroughput)
        {
            this.MaxThroughput = maxThroughput;
        }

        /// <summary>
        /// The maximum throughput the autopilot will scale to.
        /// </summary>
        [JsonIgnore]
        public int? MaxThroughput
        {
            get => this.Content?.OfferAutopilotSettings?.MaxThroughput;
            private set => this.Content = OfferContentProperties.CreateAutoPilotOfferConent(value.Value);
        }
    }
}