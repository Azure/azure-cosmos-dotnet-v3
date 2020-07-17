//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    internal class PatchOperationCore : PatchOperation
    {
        internal PatchOperationCore(
            PatchOperationType operationType,
            string path)
            : base(operationType, path)
        {
        }
    }
}
