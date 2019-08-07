//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.IO;

    /// <summary>
    /// This class provides a way to configure basic
    /// serializer settings.
    /// </summary>
    public enum CosmosNamingPolicy
    {
        /// <summary>
        /// The default naming policy
        /// </summary>
        Default = 0,

        /// <summary>
        /// Naming policy uses Camel Casing
        /// </summary>
        CamelCase = 1,
    }
}
