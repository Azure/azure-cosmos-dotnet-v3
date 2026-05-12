//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Runtime.Serialization;

    /// <summary>
    /// Defines the authentication type used by the Azure Cosmos DB service to call the
    /// embedding service referenced from a <see cref="EmbeddingSource"/>.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
    enum EmbeddingAuthType
    {
        /// <summary>
        /// Authenticate to the embedding service using an API key.
        /// </summary>
        [EnumMember(Value = "ApiKey")]
        ApiKey,

        /// <summary>
        /// Authenticate to the embedding service using Microsoft Entra ID (managed identity / token credential).
        /// </summary>
        [EnumMember(Value = "Entra")]
        Entra,
    }
}
