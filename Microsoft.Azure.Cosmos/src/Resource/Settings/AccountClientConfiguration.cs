//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Represents a <see cref="AccountClientConfiguration"/>. A AccountClientConfiguration is the client related configuration for a specific Account in the Azure Cosmos DB service.
    /// Sample Response:
    /// {"clientTelemetryConfiguration":{"isEnabled":false,"endpoint":null}}
    /// </summary>
    internal sealed class AccountClientConfiguration
    {
        /// <summary>
        /// Gets the client telemetry configuration.
        /// </summary>
        [JsonProperty(PropertyName = "clientTelemetryConfiguration")]
        public ClientTelemetryConfiguration ClientTelemetryConfiguration { get; internal set; }

        /// <summary>
        /// This contains additional values for scenarios where the SDK is not aware of new fields. 
        /// This ensures that if resource is read and updated none of the fields will be lost in the process.
        /// </summary>
        [JsonExtensionData]
        internal IDictionary<string, JToken> AdditionalProperties { get; private set; }
    }

    /// <summary>
    /// Client Telemetry Configuration
    /// </summary>
    internal sealed class ClientTelemetryConfiguration
    {
        [JsonProperty(PropertyName = "isEnabled")]
        internal bool IsEnabled { get; set; }

        [JsonProperty(PropertyName = "endpoint")]
        internal string Endpoint { get; set; }

        /// <summary>
        /// This contains additional values for scenarios where the SDK is not aware of new fields. 
        /// This ensures that if resource is read and updated none of the fields will be lost in the process.
        /// </summary>
        [JsonExtensionData]
        internal IDictionary<string, JToken> AdditionalProperties { get; private set; }
    }
}
