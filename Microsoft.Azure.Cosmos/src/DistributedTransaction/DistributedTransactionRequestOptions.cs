// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// <see cref="RequestOptions"/> that apply to an operation within a <see cref="DistributedWriteTransaction"/>
    /// or <see cref="DistributedReadTransaction"/>.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
    class DistributedTransactionRequestOptions : RequestOptions
    {
        /// <summary>
        /// Gets or sets the session token for this individual operation.
        /// </summary>
        /// <value>
        /// An opaque session token previously returned by the SDK (for example, from
        /// <see cref="DistributedTransactionOperationResult.SessionToken"/>); do not parse or construct manually.
        /// </value>
        /// <remarks>
        /// Because a distributed transaction spans multiple partitions, each operation must supply its own session token
        /// rather than a single commit-level token.  The token is serialized into the request body alongside the operation
        /// and used by the server to enforce read-your-own-writes consistency for that specific partition.
        /// <para>
        /// Obtain the token from a prior <see cref="DistributedTransactionOperationResult.SessionToken"/> or from
        /// the session token surface of another SDK response that targeted the same partition.
        /// </para>
        /// </remarks>
        public string SessionToken { get; set; }
    }
}
