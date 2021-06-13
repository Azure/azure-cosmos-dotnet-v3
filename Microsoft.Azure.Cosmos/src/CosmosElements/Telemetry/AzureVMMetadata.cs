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

    internal sealed class AzureVMMetadata
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
}
