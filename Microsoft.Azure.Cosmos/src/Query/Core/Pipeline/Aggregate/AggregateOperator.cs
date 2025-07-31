//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Aggregate
{
    using System.Text.Json.Serialization;

    [JsonConverter(typeof(JsonStringEnumConverter<AggregateOperator>))]
    internal enum AggregateOperator
    {
        Average,
        Count,
        CountIf,
        MakeList,
        MakeSet,
        Max,
        Min,
        Sum,
    }
}
