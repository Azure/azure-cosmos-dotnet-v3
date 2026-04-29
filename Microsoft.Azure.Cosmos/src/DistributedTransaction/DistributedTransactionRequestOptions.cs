// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// <see cref="RequestOptions"/> that apply to an operation within a <see cref="DistributedWriteTransaction"/>.
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif
    class DistributedTransactionRequestOptions : RequestOptions
    {
    }
}
