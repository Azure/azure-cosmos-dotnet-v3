//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    internal static class PatchConstants
    {
        public static class PropertyNames
        {
            public const string OperationType = "op";
            public const string Path = "path";
            public const string Value = "value";
        }

        public static class OperationTypeNames
        {
            public const string Add = "add";
            public const string Remove = "remove";
            public const string Replace = "replace";
            public const string Set = "set";
        }
    }
}