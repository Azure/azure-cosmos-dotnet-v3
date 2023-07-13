//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Resource.Settings
{
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal class ClientTelemetryConfiguration
    {
        [JsonProperty(PropertyName = Constants.Properties.ClientTelemetryEnabled)]
        public bool IsEnabled { get; set; }

        [JsonProperty(PropertyName = Constants.Properties.ClientTelemetryEndpoint)]
        public string Endpoint { get; set; }

        /// <summary>
        /// This contains additional values for scenarios where the SDK is not aware of new fields. 
        /// This ensures that if resource is read and updated none of the fields will be lost in the process.
        /// </summary>
        [JsonExtensionData]
        internal IDictionary<string, JToken> AdditionalProperties { get; private set; }
    }
}
