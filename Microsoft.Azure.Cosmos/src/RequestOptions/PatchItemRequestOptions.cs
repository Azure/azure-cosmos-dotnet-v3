//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Cosmos Patch request options
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
    sealed class PatchItemRequestOptions : ItemRequestOptions
    {
        /// <summary>
        /// Gets or sets condition to be checked before the patch operations in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The condition to be checked before execution of operations.
        /// </value>
        /// <remarks>
        /// Condition can only be a from-clause of a sql statement.
        /// Creates a conditional SQL argument which is of format "FROM X where CONDITION",
        /// the condition has to be within the scope of the document which is supposed to be patched in the particular request.
        /// If the condition is satisfied the patch transaction will take place otherwise it will be returned with precondition failed.
        /// </remarks>
        /// <sample>
        /// PatchItemRequestOptions requestOptions = new PatchItemRequestOptions()
        ///    {
        ///        FilterPredicate = "from c where c.taskNum = 3"
        ///    };
        /// </sample>
        public string FilterPredicate { get; set; }
    }
}