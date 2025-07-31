//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy
{
    using System.Text.Json.Serialization;

    [JsonConverter(typeof(JsonStringEnumConverter<SortOrder>))]
    internal enum SortOrder
    {
        Ascending,
        Descending,
    }
}