//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Runtime.Serialization;

    /// <summary>
    /// /// The operation type of a change feed resource with <see cref="ChangeFeedMode"/> is initialized to <see cref="ChangeFeedMode.FullFidelity"/>. Upsert operations will yield <see cref="Create"/> or <see cref="Replace"/>.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif 
    enum ChangeFeedOperationType
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
