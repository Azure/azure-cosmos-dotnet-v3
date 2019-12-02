//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if AZURECORE
namespace Azure.Cosmos.Serialization
#else
namespace Microsoft.Azure.Cosmos
#endif
{
    /// <summary>
    /// Determines the naming policy used to convert a string-based name to another format, such as a camel-casing where the first letter is lower case.
    /// </summary>
    public enum CosmosPropertyNamingPolicy
    {
        /// <summary>
        /// No custom naming policy.
        /// The property name will be the same as the source.
        /// </summary>
        Default = 0,

        /// <summary>
        /// First letter in the property name is lower case. 
        /// </summary>
        CamelCase = 1,
    }
}
