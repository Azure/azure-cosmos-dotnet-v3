//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;

    [Flags]
    internal enum SDKSupportedCapabilities : ulong
    {
        None = 0,
        PartitionMerge = 1 << 0,
        ChangeFeedWithStartTimePostMerge = 1 << 1,
        ThroughputBucketing = 1 << 2,
        IgnoreUnknownRntbdTokens = 1 << 3
    }
}
