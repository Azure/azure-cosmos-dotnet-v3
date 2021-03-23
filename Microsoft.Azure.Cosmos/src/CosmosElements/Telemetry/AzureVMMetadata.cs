//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.CosmosElements.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal class AzureVMMetadata
    {
        public AzureVMMetadata(Compute compute)
        {
            this.Compute = compute;
        }
        internal string Location => this.Compute.Location;
        internal string SKU => this.Compute.SKU;
        internal string AzEnvironment => this.Compute.AzEnvironment;
        internal string OSType => this.Compute.OSType;
        internal string VMSize => this.Compute.VMSize;
        [JsonProperty(PropertyName = "compute")]
        internal Compute Compute { get; }
    }

    internal class Compute
    {
        public Compute(string location, string sKU, string azEnvironment, string oSType, string vMSize)
        {
            this.Location = location;
            this.SKU = sKU;
            this.AzEnvironment = azEnvironment;
            this.OSType = oSType;
            this.VMSize = vMSize;
        }

        [JsonProperty(PropertyName = "location")]
        internal string Location { get; }
        [JsonProperty(PropertyName = "sku")]
        internal string SKU { get; }
        [JsonProperty(PropertyName = "azEnvironment")]
        internal string AzEnvironment { get; }
        [JsonProperty(PropertyName = "osType")]
        internal string OSType { get; }
        [JsonProperty(PropertyName = "vmSize")]
        internal string VMSize { get; }
    }
}
