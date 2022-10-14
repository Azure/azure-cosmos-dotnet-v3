// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    [Flags]
    internal enum SDKSupportedCapabilities : ulong
    {
        None = 0,
        PartitionMerge = 1 << 0,
    }
}
