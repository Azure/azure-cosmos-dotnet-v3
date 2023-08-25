//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System.Net;

    /// <summary>
    /// Service response that can be evaluated through an IRequestRetryPolicy and <see cref="RequestRetryUtility"/>.
    /// </summary>
    internal interface IRetriableResponse
    {
        /// <summary>
        /// <see cref="HttpStatusCode"/> in the service response.
        /// </summary>
        HttpStatusCode StatusCode { get; }

        /// <summary>
        /// <see cref="SubStatusCodes"/> in the service response.
        /// </summary>
        SubStatusCodes SubStatusCode { get; }
    }
}
