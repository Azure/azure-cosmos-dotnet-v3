//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Threading.Tasks;
    internal interface IConnectionDispenser
    {
        /// <summary>
        /// Returns an IConnection which is ready to fulfill requests.
        /// </summary>
        /// <param name="activityId">Guid used for correlation between client and service</param>
        /// <param name="fullUri">The URI of the target server. Only the host and port are used for establishing the connection</param>
        /// <param name="poolKey">A string uniquely identifying the group of connections being pooled. "host:port" is currently used</param>
        /// <returns>An IConnection which has had any initialization handshakes performed, so that it is ready for requests</returns>
        Task<IConnection> OpenNewConnection(Guid activityId, Uri fullUri, string poolKey);
    }
}
