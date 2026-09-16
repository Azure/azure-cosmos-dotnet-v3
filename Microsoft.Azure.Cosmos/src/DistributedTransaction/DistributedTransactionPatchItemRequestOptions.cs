// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// <see cref="DistributedTransactionRequestOptions"/> that apply to a patch operation within a
    /// <see cref="DistributedWriteTransaction"/>.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
    class DistributedTransactionPatchItemRequestOptions : DistributedTransactionRequestOptions
    {
        /// <summary>
        /// Gets or sets the condition to be checked before the patch operations in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The condition to be checked before execution of the operations.
        /// </value>
        /// <remarks>
        /// The condition can only be a SQL statement. It creates a conditional SQL argument which is of the format
        /// of a from-clause; the condition has to be within the scope of the document which is supposed to be patched
        /// in the particular operation. If the condition is satisfied (the given document meets the given from-clause
        /// SQL statement), the patch is applied for that operation; otherwise the operation fails with
        /// <see cref="System.Net.HttpStatusCode.PreconditionFailed"/> and the entire distributed transaction is not committed.
        /// </remarks>
        /// <sample>
        /// DistributedTransactionPatchItemRequestOptions requestOptions = new DistributedTransactionPatchItemRequestOptions()
        ///    {
        ///        FilterPredicate = "from c where c.taskNum = 3"
        ///    };
        /// </sample>
        public string FilterPredicate { get; set; }
    }
}
