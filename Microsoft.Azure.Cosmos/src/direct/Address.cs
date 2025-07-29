//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System.Text.Json.Serialization;

    internal sealed class Address : JsonSerializable
    {
        [JsonPropertyName(Constants.Properties.IsAuxiliary)]
        public bool IsAuxiliary { get; set; }

        [JsonPropertyName(Constants.Properties.IsPrimary)]
        public bool IsPrimary { get; set; }

        [JsonPropertyName(Constants.Properties.Protocol)]
        public string Protocol { get; set; }

        [JsonPropertyName(Constants.Properties.LogicalUri)]
        public string LogicalUri { get; set; }

        [JsonPropertyName(Constants.Properties.PhysicalUri)]
        public string PhysicalUri { get; set; }

        [JsonPropertyName(Constants.Properties.PartitionIndex)]
        public string PartitionIndex { get; set; }

        [JsonPropertyName(Constants.Properties.PartitionKeyRangeId)]
        public string PartitionKeyRangeId { get; set; }
    }
}
