//-----------------------------------------------------------------------
// <copyright file="ItemType.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    /// <summary>
    /// Enum of all item types that are returned by queries.
    /// </summary>
    internal enum ItemType
    {
        /// <summary>
        /// NoValue / Undefined item type.
        /// </summary>
        NoValue = 0x0,

        /// <summary>
        /// Null item type.
        /// </summary>
        Null = 0x1,

        /// <summary>
        /// Boolean item type.
        /// </summary>
        Bool = 0x2,

        /// <summary>
        /// Number item type.
        /// </summary>
        Number = 0x4,

        /// <summary>
        /// String item type.
        /// </summary>
        String = 0x5
    }
}
