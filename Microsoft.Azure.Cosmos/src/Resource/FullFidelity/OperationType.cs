//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.FullFidelity
{
    using System.Runtime.Serialization;

    /// <summary>
    /// The operation type of full fidelity metadata.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif 
    enum OperationType
    {
        /// <summary>
        /// The create operation type.
        /// </summary>
        [EnumMember(Value = "create")]
        Create,
        /// <summary>
        /// The replace operation type.
        /// </summary>
        [EnumMember(Value = "replace")]
        Replace,
        /// <summary>
        /// The delete operation type.
        /// </summary>
        [EnumMember(Value = "delete")]
        Delete,
    }
}
