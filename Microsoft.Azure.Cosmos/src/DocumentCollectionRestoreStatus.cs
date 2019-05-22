//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides status for a documention collection restore.
    /// </summary>
    internal sealed class DocumentCollectionRestoreStatus
    {
        /// <summary>
        /// Gets the <see cref="State"/> from the Azure Cosmos DB service. 
        /// </summary>
        /// <value>
        /// The state of the restore process.
        /// </value>
        public string State
        {
            get;
            internal set;
        }
    }
}