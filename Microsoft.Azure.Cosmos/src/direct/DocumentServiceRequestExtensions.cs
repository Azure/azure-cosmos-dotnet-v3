//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    internal static class DocumentServiceRequestExtensions
    {
        /// <summary>
        /// This method is used to define if a particular response to the current <see cref="DocumentServiceRequest"/> needs to be processed without exceptions based on the status code and substatus code.
        /// </summary>
        /// <param name="request">Current <see cref="DocumentServiceRequest"/> instance.</param>
        /// <param name="statusCode">Status code of the response.</param>
        /// <param name="subStatusCode"><see cref="SubStatusCodes"/> of the response of any.</param>
        /// <returns>Whether the response should be processed without exceptions (true) or not (false).</returns>
        public static bool IsValidStatusCodeForExceptionlessRetry(
            this DocumentServiceRequest request,
            int statusCode,
            SubStatusCodes subStatusCode = SubStatusCodes.Unknown)
        {
            // Only for 404, 409, and 412
            if (request.UseStatusCodeForFailures
                && (statusCode == (int)System.Net.HttpStatusCode.PreconditionFailed
                || statusCode == (int)System.Net.HttpStatusCode.Conflict
                // ReadSessionNotAvailable: fall back to exception based approach for reliability
                || (statusCode == (int)System.Net.HttpStatusCode.NotFound && subStatusCode != SubStatusCodes.ReadSessionNotAvailable)))
            {
                return true;
            }

            // Only for 429
            if (request.UseStatusCodeFor429
                && statusCode == (int)StatusCodes.TooManyRequests)
            {
                return true;
            }

            return false;
        }
    }
}