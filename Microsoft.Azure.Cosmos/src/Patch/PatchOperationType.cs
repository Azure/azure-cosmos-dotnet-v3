//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Runtime.Serialization;

    internal enum PatchOperationType
    {
        [EnumMember(Value = "add")]
        Add,

        [EnumMember(Value = "remove")]
        Remove,

        [EnumMember(Value = "replace")]
        Replace,

        [EnumMember(Value = "set")]
        Set,
    }
}
