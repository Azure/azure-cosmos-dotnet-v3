//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Runtime.Serialization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Describes the list of Patch supported operation types.
    /// </summary>
    /// <remarks>
    /// Further enum additions are expected in the future, application should be authored to cover this scenario
    /// </remarks>
    [JsonConverter(typeof(StringEnumConverter))]

    public enum PatchOperationType
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

        /// <summary>
        /// Operation to move a object/value.
        /// </summary>
        [EnumMember(Value = PatchConstants.OperationTypeNames.Move)]
        Move,
    }
}
