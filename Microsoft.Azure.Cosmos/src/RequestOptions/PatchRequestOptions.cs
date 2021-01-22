//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Cosmos item request options
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif

    public class PatchRequestOptions : ItemRequestOptions
    {
        /// <summary>
        /// Gets or sets condition to be checked before the patch operations in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The condition to be checked before execution of operations.
        /// </value>
        /// <remarks>
        /// condition only sql statement.
        /// </remarks>
        public string Condition { get; set; }
    }
}