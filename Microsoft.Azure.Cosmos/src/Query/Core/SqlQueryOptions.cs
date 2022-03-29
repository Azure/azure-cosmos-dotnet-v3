//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents a collection of options associated with <see cref="T:Microsoft.Azure.Documents.SqlQuerySpec"/> for use in the Azure Cosmos DB service.
    /// </summary>
    internal sealed class SqlQueryOptions
    {
        private bool isPassThrough;

        public SqlQueryOptions()
        {
            this.isPassThrough = false;
        }

        /// <summary>
        /// Gets or sets the value of isPassThrough.
        /// </summary>
        /// <value>The value for whether a query will short circuit and go straight to the Backend or not</value>
        public bool IsPassThrough
        {
            get { return this.isPassThrough; }
            set { this.isPassThrough = value; }
        }
       
        /// <summary>
        /// Gets a value indicating whether the Azure Cosmos DB collection is read-only.
        /// </summary>
        /// <value>true if the collection is read-only; otherwise, false.</value>
        public bool IsReadOnly
        {
            get { return false; }
        }
    }
}
