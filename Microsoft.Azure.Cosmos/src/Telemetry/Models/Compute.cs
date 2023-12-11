//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Models
{
    using System;
    using System.Text.Json.Serialization;

    [Serializable]
    internal sealed class Compute
    {
        public Compute()
        {
        }

        public Compute(
            string vMId,
            string location,
            string sKU,
            string azEnvironment,
            string oSType,
            string vMSize)
        {
            this.Location = location;
            this.SKU = sKU;
            this.AzEnvironment = azEnvironment;
            this.OSType = oSType;
            this.VMSize = vMSize;
            this.VMId = $"{VmMetadataApiHandler.VmIdPrefix}{vMId}";
        }

        [JsonPropertyName("location")]
        public string Location { get; }

        [JsonPropertyName("sku")]
        public string SKU { get; }

        [JsonPropertyName("azEnvironment")]
        public string AzEnvironment { get; }

        [JsonPropertyName("osType")]
        public string OSType { get; }

        [JsonPropertyName("vmSize")]
        public string VMSize { get; }

        [JsonPropertyName("vmId")]
        public string VMId { get; }
    }

}
