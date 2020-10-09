//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

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
            public const string Increment = "inc";
        }

        public static string ToEnumMemberString(this PatchOperationType patchOperationType)
        {
            switch (patchOperationType)
            {
                case PatchOperationType.Add:
                    return PatchConstants.OperationTypeNames.Add;
                case PatchOperationType.Remove:
                    return PatchConstants.OperationTypeNames.Remove;
                case PatchOperationType.Replace:
                    return PatchConstants.OperationTypeNames.Replace;
                case PatchOperationType.Set:
                    return PatchConstants.OperationTypeNames.Set;
                case PatchOperationType.Increment:
                    return PatchConstants.OperationTypeNames.Increment;
                default:
                    throw new ArgumentException($"Unknown Patch operation type '{patchOperationType}'.");
            }
        }
    }
}