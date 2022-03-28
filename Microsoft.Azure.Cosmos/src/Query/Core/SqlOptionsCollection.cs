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
    internal sealed class SqlOptionsCollection
    {
        private bool isPassThrough = false;

        /// <summary>
        /// Initialize a new instance of the SqlOptionsCollection class for the Azure Cosmos DB service.
        /// </summary>
        public SqlOptionsCollection()
        {
            this.isPassThrough = false;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.Azure.Documents.SqlOptionsCollection"/> class for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="isPassThrough">Boolean to let the backend know if query is pass through or not.</param>
        public SqlOptionsCollection(bool isPassThrough)
        {
            this.isPassThrough = isPassThrough;
        }

        /// <summary>
        /// Removes existing value of IsPassThrough and sets it to false.
        /// </summary>
        public void ClearIsPassThrough()
        {
            this.isPassThrough = false;
        }

        /// <summary>
        /// Gets or sets the value of isPassThrough.
        /// </summary>
        /// <param name="isPassThrough">The boolean to let the backend know if query is pass through or not.</param>
        public bool this[bool isPassThrough]
        {
            get { return this.isPassThrough; }
            set { this.isPassThrough = isPassThrough; }
        }

        /// <summary>
        /// Set IsPassThrough boolean to the value provided by the user
        /// </summary>
        public void SetIsPassThrough(bool isPassThrough)
        {
            this.isPassThrough = isPassThrough;
        }

        /// <summary>
        /// Get the current boolean value of isPassThrough
        /// </summary>
        public bool GetIsPassThrough()
        {
            return this.isPassThrough;
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
