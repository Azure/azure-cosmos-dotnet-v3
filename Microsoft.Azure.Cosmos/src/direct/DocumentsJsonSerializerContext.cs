//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Documents.Routing;

namespace Microsoft.Azure.Documents
{
    [JsonSerializable(typeof(PartitionKeyInternal))]
    internal partial class DocumentsJsonSerializerContext : JsonSerializerContext
    {
    }
}
