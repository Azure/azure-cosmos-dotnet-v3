﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using Newtonsoft.Json;
    using Util;

    [Serializable]
    internal sealed class Compute
    {
        [JsonConstructor]
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
            this.VMId = $"{VmMetadataApiHandler.HashedVmIdPrefix}{HashingExtension.ComputeHash(vMId)}";
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

        [JsonProperty(PropertyName = "vmId")]
        internal string VMId { get; }
    }

}
