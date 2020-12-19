//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;
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
        ValueTask<string> GetUserAuthorizationTokenAsync(
            string resourceAddress,
            string resourceType,
            string requestVerb,
            INameValueCollection headers,
            AuthorizationTokenType tokenType,
            ITrace trace);
    }
}
