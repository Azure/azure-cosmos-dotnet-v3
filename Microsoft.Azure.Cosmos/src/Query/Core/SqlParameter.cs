//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Runtime.Serialization;

    /// <summary>
    /// Represents a parameter associated with <see cref="SqlQuerySpec"/> in the Azure Cosmos DB service.
    /// </summary> 
    /// <remarks>
    /// Azure Cosmos DB SQL parameters are name-value pairs referenced in parameterized queries. 
    /// Unlike in relation SQL databases, they don't have types associated with them.
    /// </remarks>
    [DataContract]
    internal sealed class SqlParameter
    {
        private string name;
        private object value;

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
            this.name = name;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlParameter"/> class with the name and value of the parameter for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="value">The value of the parameter.</param>
        /// <remarks>Names of parameters must begin with '@' and be a valid SQL identifier. The value gets serialized and passed in as JSON to the document query.</remarks>
        public SqlParameter(string name, object value)
        {
            this.name = name;
            this.value = value;
        }

        /// <summary>
        /// Gets or sets the name of the parameter for the Azure Cosmos DB service.
        /// </summary>
        /// <value>The name of the parameter.</value>
        /// <remarks>Names of parameters must begin with '@' and be a valid SQL identifier.</remarks>
        [DataMember(Name = "name")]
        public string Name
        {
            get { return this.name; }
            set { this.name = value; }
        }

        /// <summary>
        /// Gets or sets the value of the parameter for the Azure Cosmos DB service.
        /// </summary>
        /// <value>The value of the parameter.</value>
        /// <remarks>The value gets serialized and passed in as JSON to the document query.</remarks>
        [DataMember(Name = "value")]
        public object Value
        {
            get { return this.value; }
            set { this.value = value; }
        }
    }
}
