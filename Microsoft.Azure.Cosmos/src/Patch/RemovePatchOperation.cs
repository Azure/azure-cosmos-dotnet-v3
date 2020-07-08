//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    internal sealed class RemovePatchOperation : PatchOperation
    {
        public RemovePatchOperation(
            string path)
            : base(PatchOperationType.Remove, path)
        {
        }
    }
}
