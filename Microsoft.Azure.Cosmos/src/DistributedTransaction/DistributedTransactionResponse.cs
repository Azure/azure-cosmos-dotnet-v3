namespace Microsoft.Azure.Cosmos.DistributedTransaction
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Represents the response for a distributed transaction operation.
    /// </summary>
    public class DistributedTransactionResponse : IDisposable
    {
        /// <summary>
        /// Disposes the current DistributedTransactionResponse.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
        }
    }
}
