// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Represents a distributed transaction that will be performed across partitions and/or collections. 
    /// </summary>
    public abstract class DistributedTransaction
    {
        /// <summary>
        /// Commits the distributed transaction.
        /// </summary>
        public abstract void CommitTransaction();
    }
}
