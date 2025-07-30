//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System.Text.Json.Serialization;

    public sealed class Address : PlainResource
    {
        [JsonPropertyName(Constants.Properties.IsAuxiliary)]
        public bool IsAuxiliary { get; init; }

        [JsonPropertyName(Constants.Properties.IsPrimary)]
        public bool IsPrimary { get; init; }

        [JsonPropertyName(Constants.Properties.Protocol)]
        public string Protocol { get; init; }

        [JsonPropertyName(Constants.Properties.LogicalUri)]
        public string LogicalUri { get; init; }

        [JsonPropertyName(Constants.Properties.PhysicalUri)]
        public string PhysicalUri { get; init; }

        [JsonPropertyName(Constants.Properties.PartitionIndex)]
        public string PartitionIndex { get; init; }

        [JsonPropertyName(Constants.Properties.PartitionKeyRangeId)]
        public string PartitionKeyRangeId { get; init; }
    }
}
