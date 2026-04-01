//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    internal interface IDocumentClientRetryPolicy : IRetryPolicy
    {
        /// <summary>
        /// Method that is called before a request is sent to allow the retry policy implementation
        /// to modify the state of the request.
        /// </summary>
        /// <param name="request">The request being sent to the service.</param>
        /// <remarks>
        /// Currently only read operations will invoke this method. There is no scenario for write
        /// operations to modify requests before retrying.
        /// </remarks>
        void OnBeforeSendRequest(DocumentServiceRequest request);

        /// <summary>
        /// Method that is called to determine from the policy that needs to retry on the a particular status code
        /// </summary>
        /// <param name="cosmosResponseMessage"><see cref="ResponseMessage"/> in return of the request</param>
        /// <param name="cancellationToken"></param>
        /// <returns>If the retry needs to be attempted or not</returns>
        Task<ShouldRetryResult> ShouldRetryAsync(ResponseMessage cosmosResponseMessage, CancellationToken cancellationToken);
    }

    internal interface IRetryPolicyFactory
    {
        /// <summary>
        /// Method that is called to get the retry policy for a non-query request.
        /// </summary>
        IDocumentClientRetryPolicy GetRequestPolicy();
    }
}