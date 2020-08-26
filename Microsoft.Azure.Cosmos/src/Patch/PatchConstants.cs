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
        }

        public static string ToEnumMemberString(this PatchOperationType patchOperationType)
        {
            return patchOperationType switch
            {
                PatchOperationType.Add => PatchConstants.OperationTypeNames.Add,
                PatchOperationType.Remove => PatchConstants.OperationTypeNames.Remove,
                PatchOperationType.Replace => PatchConstants.OperationTypeNames.Replace,
                PatchOperationType.Set => PatchConstants.OperationTypeNames.Set,
                _ => throw new ArgumentException($"Unknown Patch operation type '{patchOperationType}'."),
            };
        }
    }
}