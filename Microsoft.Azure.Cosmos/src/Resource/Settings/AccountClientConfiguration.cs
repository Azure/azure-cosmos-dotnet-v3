//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Newtonsoft.Json;

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
    }

    /// <summary>
    /// Client Telemetry Configuration
    /// </summary>
    public sealed class ClientTelemetryConfiguration
    {
        [JsonProperty(PropertyName = "isEnabled")]
        internal bool IsEnabled { get; set; }

        [JsonProperty(PropertyName = "endpoint")]
        internal string Endpoint { get; set; }
    }
}
