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
        /// A distributed transaction spans multiple partitions, so an explicit token applies to one operation
        /// rather than the whole transaction. The token is serialized into the request body alongside the operation.
        /// If no explicit token is supplied and the account's default consistency is Session, the SDK resolves
        /// a cached partition-local token for both read and write operations. Client consistency overrides do not
        /// affect automatic session-token resolution. A required partition-routing lookup failure prevents
        /// the transaction from being sent.
        /// <para>
        /// Obtain the token from a prior <see cref="DistributedTransactionOperationResult.SessionToken"/> or from
        /// the session token surface of another SDK response that targeted the same partition.
        /// Supply a single partition-local vector token; compound tokens and legacy LSN-only tokens are rejected
        /// with an <see cref="System.ArgumentException"/> before the transaction is sent.
        /// </para>
        /// </remarks>
        public string SessionToken { get; set; }
    }
}
