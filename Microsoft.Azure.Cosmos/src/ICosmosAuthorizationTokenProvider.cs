//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.IO;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    /// <summary>
    /// Interface that provides Authorization token headers for requests given
    /// a particular request.
    /// </summary>
    internal interface ICosmosAuthorizationTokenProvider
    {
        /// <summary>
        /// Generates a Authorization Token for a given resource type, address and action.
        /// </summary>
        string GetUserAuthorizationToken(
            string resourceAddress,
            string resourceType,
            string requestVerb,
            INameValueCollection headers,
            AuthorizationTokenType tokenType,
            out MemoryStream payload);
    }
}
