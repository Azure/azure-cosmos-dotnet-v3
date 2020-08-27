//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Runtime.Serialization;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    [JsonConverter(typeof(StringEnumConverter))]
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