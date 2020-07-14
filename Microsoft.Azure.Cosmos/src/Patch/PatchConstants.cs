//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    internal static class PatchConstants
    {
        public static Dictionary<PatchOperationType, string> PatchOperationTypes = new Dictionary<PatchOperationType, string>
        {
            [PatchOperationType.Add] = "add",
            [PatchOperationType.Remove] = "remove",
            [PatchOperationType.Replace] = "replace",
            [PatchOperationType.Set] = "set",
        };

        public static class PropertyNames
        {
            public const string OperationType = "op";
            public const string Path = "path";
            public const string Value = "value";
        }
    }
}