//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;

    [JsonConverter(typeof(JsonStringEnumConverter<PartitionKeyRangeStatus>))]
    internal enum PartitionKeyRangeStatus
    {
        Invalid,
        
        [EnumMember(Value = "online")]
        Online,
        
        [EnumMember(Value = "splitting")]
        Splitting,

        [EnumMember(Value = "offline")]
        Offline,

        [EnumMember(Value = "split")]
        Split
    }
}