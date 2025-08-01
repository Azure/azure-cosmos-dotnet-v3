//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Models
{
    using System;
    using System.Text.Json.Serialization;

    internal sealed class AzureVMMetadata
    {
        [JsonConstructor]
        public AzureVMMetadata(Compute compute)
        {
            this.Compute = compute;
        }

        [JsonPropertyName("compute")]
        public Compute Compute { get; }
    }
}
