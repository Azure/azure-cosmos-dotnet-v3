//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using Newtonsoft.Json;

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
