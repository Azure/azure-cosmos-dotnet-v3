//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens
{
    internal interface IPartitionedToken
    {
        Documents.Routing.Range<string> PartitionRange { get; }
    }
}
