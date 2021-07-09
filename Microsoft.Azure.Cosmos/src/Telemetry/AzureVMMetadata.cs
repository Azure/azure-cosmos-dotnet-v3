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

        [JsonProperty(PropertyName = "compute")]
        internal Compute Compute { get; }
    }
}
