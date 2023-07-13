//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Resource.Settings
{
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal class AccountClientConfigProperties
    {
        [JsonProperty(PropertyName = Constants.Properties.ClientTelemetryConfiguration)]
        public ClientTelemetryConfiguration ClientTelemetryConfiguration { get; set; }
      
        /// <summary>
        /// This contains additional values for scenarios where the SDK is not aware of new fields. 
        /// This ensures that if resource is read and updated none of the fields will be lost in the process.
        /// </summary>
        [JsonExtensionData]
        internal IDictionary<string, JToken> AdditionalProperties { get; private set; }

        internal bool IsClientTelemetryEnabled()
        {
            return this.ClientTelemetryConfiguration.IsEnabled && this.ClientTelemetryConfiguration.Endpoint != null;
        }
    }
}
