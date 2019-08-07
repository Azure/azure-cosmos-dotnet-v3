//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Determines the naming policy used to convert a string-based name to another format, such as a camel-casing format.
    /// </summary>
    public enum CosmosPropertyNamingPolicy
    {
        /// <summary>
        /// No custom naming policy.
        /// The property name will be the same as the source.
        /// </summary>
        Default = 0,

        /// <summary>
        /// Naming policy uses Camel Casing
        /// </summary>
        CamelCase = 1,
    }
}
