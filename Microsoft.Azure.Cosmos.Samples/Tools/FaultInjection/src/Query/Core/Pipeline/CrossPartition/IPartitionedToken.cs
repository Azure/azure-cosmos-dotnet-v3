//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition
{
    internal interface IPartitionedToken
    {
        Documents.Routing.Range<string> Range { get; }
    }
}
