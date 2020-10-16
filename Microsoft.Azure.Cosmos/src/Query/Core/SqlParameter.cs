//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Represents a parameter associated with <see cref="SqlQuerySpec"/> in the Azure Cosmos DB service.
    /// </summary> 
    /// <remarks>
    /// Azure Cosmos DB SQL parameters are name-value pairs referenced in parameterized queries. 
    /// Unlike in relation SQL databases, they don't have types associated with them.
    /// </remarks>
    [DataContract]
#if INTERNAL
    public
#else
    internal
#endif
        sealed class SqlParameter : IEquatable<SqlParameter>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SqlParameter"/> class for the Azure Cosmos DB service.
        /// </summary>
        public SqlParameter()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlParameter"/> class with the name of the parameter for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="name">The name of the parameter.</param>
        /// <remarks>Names of parameters must begin with '@' and be a valid SQL identifier.</remarks>
        public SqlParameter(string name)
        {
            this.Name = name;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlParameter"/> class with the name and value of the parameter for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="value">The value of the parameter.</param>
        /// <remarks>Names of parameters must begin with '@' and be a valid SQL identifier. The value gets serialized and passed in as JSON to the document query.</remarks>
        public SqlParameter(string name, object value)
        {
            this.Name = name;
            this.Value = value;
        }

        /// <summary>
        /// Gets or sets the name of the parameter for the Azure Cosmos DB service.
        /// </summary>
        /// <value>The name of the parameter.</value>
        /// <remarks>Names of parameters must begin with '@' and be a valid SQL identifier.</remarks>
        [DataMember(Name = "name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the value of the parameter for the Azure Cosmos DB service.
        /// </summary>
        /// <value>The value of the parameter.</value>
        /// <remarks>The value gets serialized and passed in as JSON to the document query.</remarks>
        [DataMember(Name = "value")]
        public object Value { get; set; }

        /// <summary>
        /// Checking for equality between two Sql parameter objects.
        /// </summary>
        /// <param name="other"></param>
        /// <returns>True if objects are equal, false otherwise.</returns>
        public bool Equals(SqlParameter other)
        {
            if (other == null)
            {
                return false;
            }

            if (Object.ReferenceEquals(this, other))
            {
                return true;
            }

            return this.Name == other.Name && this.Value == other.Value;
        }

        /// <summary>
        /// Simple implementation of hash code for SqlParameter class.
        /// </summary>
        /// <returns>Integer representing the hash code.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 233) + (this.Name == null ? 0 : this.Name.GetHashCode());
                hash = (hash * 233) + (this.Value == null ? 0 : this.Value.GetHashCode());
                return hash;
            }
        }
    }
}
