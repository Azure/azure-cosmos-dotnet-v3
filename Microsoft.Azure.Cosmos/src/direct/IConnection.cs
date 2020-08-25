//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Threading.Tasks;

    internal interface IConnection
    {
        /// <summary>
        /// Issue a request and read the response on a connection
        /// </summary>
        /// <param name="request">The request to send</param>
        /// <param name="physicalAddress">
        ///     With connection pooling is used, it is assumed that host:port of the Uri used to open the connection
        ///     matches host:port of the this parameter. However, the path of the Uri need not match the 
        /// </param>
        /// <param name="resourceOperation">Resource and Operation Type for the request</param>
        /// <param name="activityId">Guid activityId for the request</param>
        /// <returns>The response from the server</returns>
        Task<StoreResponse> RequestAsync(
            DocumentServiceRequest request,
            Uri physicalAddress,
            ResourceOperation resourceOperation,
            Guid activityId);

        /// <summary>
        /// Close the connection and Dispose underlying disposable resources.
        /// </summary>
        void Close();

        /// <summary>
        /// Checks whether the connection has been unused for longer than its expiration time. To be
        /// used as a heuristic to reduce network failures while issuing requests on pooled connections.
        /// </summary>
        /// <returns>true if connection has expired, false otherwise</returns>
        bool HasExpired();

        /// <summary>
        /// Checks whether the connection is currently readable and writable. To be used as a heuristic
        /// to reduce network failures while issuing requests on pooled connections.
        /// </summary>
        /// <returns></returns>
        bool ConfirmOpen();

        /// <summary>
        /// The poolKey passed in to the IConnectionDispenser which created this IConnection
        /// </summary>
        string PoolKey
        {
            get;
        }
    }
}
