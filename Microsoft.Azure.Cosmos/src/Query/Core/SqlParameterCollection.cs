//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents a collection of parameters associated with <see cref="T:Microsoft.Azure.Documents.SqlQuerySpec"/> for use in the Azure Cosmos DB service.
    /// </summary>
    internal sealed class SqlParameterCollection : IList<SqlParameter>
    {
        private readonly List<SqlParameter> parameters;

        /// <summary>
        /// Initialize a new instance of the SqlParameterCollection class for the Azure Cosmos DB service.
        /// </summary>
        public SqlParameterCollection()
        {
            this.parameters = new List<SqlParameter>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.Azure.Documents.SqlParameterCollection"/> class for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="parameters">The collection of parameters.</param>
        public SqlParameterCollection(IEnumerable<SqlParameter> parameters)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException("parameters");
            }

            this.parameters = new List<SqlParameter>(parameters);
        }

        /// <summary>
        /// Determines the index of a specific item in the Azure Cosmos DB collection.
        /// </summary> 
        /// <param name="item">The item to find.</param>
        /// <returns>The index value for the item.</returns>
        public int IndexOf(SqlParameter item)
        {
            return this.parameters.IndexOf(item);
        }

        /// <summary>
        /// Inserts an item at the specified index in the Azure Cosmos DB collection.
        /// </summary>
        /// <param name="index">The location in the index array in which to start inserting elements.</param>
        /// <param name="item">The item to copy into the index.</param>
        public void Insert(int index, SqlParameter item)
        {
            this.parameters.Insert(index, item);
        }

        /// <summary>
        /// Removes the item at the specified index from the Azure Cosmos DB collection.
        /// </summary>
        /// <param name="index">The location in the index where the item will be removed from.</param>
        public void RemoveAt(int index)
        {
            this.parameters.RemoveAt(index);
        }

        /// <summary>
        /// Gets or sets the element at the specified index in the Azure Cosmos DB collection.
        /// </summary>
        /// <param name="index">The location in the index.</param>
        /// <value>The element at the specified index.</value>
        public SqlParameter this[int index]
        {
            get { return this.parameters[index]; }
            set { this.parameters[index] = value; }
        }

        /// <summary>
        /// Adds an item to the Azure Cosmos DB collection.
        /// </summary>
        /// <param name="item">The item to add to the collection.</param>
        public void Add(SqlParameter item)
        {
            this.parameters.Add(item);
        }

        /// <summary>
        /// Removes all items from the Azure Cosmos DB collection.
        /// </summary>
        public void Clear()
        {
            this.parameters.Clear();
        }

        /// <summary>
        /// Determines whether the Azure Cosmos DB collection contains a specific value.
        /// </summary>
        /// <param name="item">The value to search for.</param>
        /// <returns>true if the collection contains a specific value; otherwise, false.</returns>
        public bool Contains(SqlParameter item)
        {
            return this.parameters.Contains(item);
        }

        /// <summary>
        /// Copies the elements of the Azure Cosmos DB collection to an <see cref="T:System.Array"/>, starting at a particular <see cref="T:System.Array"/> index.</summary>
        /// <param name="array">The array to copy into.</param>
        /// <param name="arrayIndex">The location in the index array in which to start adding elements.</param>
        public void CopyTo(SqlParameter[] array, int arrayIndex)
        {
            this.parameters.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Gets the number of elements contained in the Azure Cosmos DB collection.
        /// </summary>
        /// <value>The number of elements contained in the collection.</value>
        public int Count
        {
            get { return this.parameters.Count; }
        }

        /// <summary>
        /// Gets a value indicating whether the Azure Cosmos DB collection is read-only.
        /// </summary>
        /// <value>true if the collection is read-only; otherwise, false.</value>
        public bool IsReadOnly
        {
            get { return false; }
        }

        /// <summary>
        /// /// Removes the first occurrence of a specific object from the Azure Cosmos DB collection.
        /// </summary>
        /// <param name="item">
        /// The item to remove from the collection.
        /// </param>
        /// <returns>true if the first item was removed; otherwise, false.</returns>
        public bool Remove(SqlParameter item)
        {
            return this.parameters.Remove(item);
        }

        /// <summary>
        /// Returns an enumerator that iterates through the Azure Cosmos DB collection.
        /// </summary>
        /// <returns>An enumerator for the collection.</returns>
        public IEnumerator<SqlParameter> GetEnumerator()
        {
            return this.parameters.GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through the Azure Cosmos DB collection.
        /// </summary>
        /// <returns>An enumerator to iterate through the collection. </returns>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.parameters.GetEnumerator();
        }
    }
}
