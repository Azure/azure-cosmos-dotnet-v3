//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Newtonsoft.Json;

    internal sealed class RemovePatchOperation : PatchOperation
    {
        public RemovePatchOperation(
            string path)
            : base(PatchOperationType.Remove, path)
        {
        }
    }
}
