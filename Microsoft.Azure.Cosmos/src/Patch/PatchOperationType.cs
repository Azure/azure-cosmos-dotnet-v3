//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Runtime.Serialization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Type of Patch operation.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
#if PREVIEW
    public
#else
    internal
#endif
        enum PatchOperationType
    {
        /// <summary>
        /// Operation to add a value.
        /// </summary>
        [EnumMember(Value = PatchConstants.OperationTypeNames.Add)]
        Add,

        /// <summary>
        /// Operation to remove a value.
        /// </summary>
        [EnumMember(Value = PatchConstants.OperationTypeNames.Remove)]
        Remove,

        /// <summary>
        /// Operation to replace a value.
        /// </summary>
        [EnumMember(Value = PatchConstants.OperationTypeNames.Replace)]
        Replace,

        /// <summary>
        /// Operation to set a value.
        /// </summary>
        [EnumMember(Value = PatchConstants.OperationTypeNames.Set)]
        Set,

        /// <summary>
        /// Operation to increment a value.
        /// </summary>
        [EnumMember(Value = PatchConstants.OperationTypeNames.Increment)]
        Increment,
    }
}
