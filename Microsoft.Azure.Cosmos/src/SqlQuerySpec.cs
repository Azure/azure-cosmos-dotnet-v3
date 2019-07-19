//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.Text;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents a SQL query in the Azure Cosmos DB service.
    /// </summary>
    [DataContract]
    internal sealed class SqlQuerySpec
    {
        private SqlParameterCollection parameters;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.Azure.Documents.SqlQuerySpec"/> class for the Azure Cosmos DB service.</summary>
        /// <remarks> 
        /// The default constructor initializes any fields to their default values.
        /// </remarks>
        public SqlQuerySpec()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.Azure.Documents.SqlQuerySpec"/> class for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="queryText">The text of the query.</param>
        public SqlQuerySpec(string queryText)
            : this(queryText, new SqlParameterCollection())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.Azure.Documents.SqlQuerySpec"/> class for the Azure Cosmos DB service.
        /// </summary>
        /// <param name="queryText">The text of the database query.</param>
        /// <param name="parameters">The <see cref="T:Microsoft.Azure.Documents.SqlParameterCollection"/> instance, which represents the collection of query parameters.</param>
        public SqlQuerySpec(string queryText, SqlParameterCollection parameters)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException("parameters");
            }

            this.QueryText = queryText;
            this.parameters = parameters;
        }

        /// <summary>
        /// Gets or sets the text of the Azure Cosmos DB database query.
        /// </summary>
        /// <value>The text of the database query.</value>
        [DataMember(Name = "query")]
        public string QueryText { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="T:Microsoft.Azure.Documents.SqlParameterCollection"/> instance, which represents the collection of Azure Cosmos DB query parameters.
        /// </summary>
        /// <value>The <see cref="T:Microsoft.Azure.Documents.SqlParameterCollection"/> instance.</value>
        [DataMember(Name = "parameters")]
        public SqlParameterCollection Parameters
        {
            get
            {
                return this.parameters;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException("value");
                }

                this.parameters = value;
            }
        }

        /// <summary>
        /// Returns a value that indicates whether the Azure Cosmos DB database <see cref="Parameters"/> property should be serialized.
        /// </summary>
        public bool ShouldSerializeParameters()
        {
            return this.parameters.Count > 0;
        }

        /// <summary>
        /// Convert the SqlQuerySpec to a stream
        /// </summary>
        public Stream ToStream(CosmosClientContext clientContext)
        {
            return SqlQuerySpecToStream.ToStream(
                sqlQuerySpec: this,
                clientContext: clientContext);
        }

        /// <summary>
        /// Private class to serialize the SqlQuerySpec
        /// </summary>
        private static class SqlQuerySpecToStream
        {
            private static readonly byte[] startOfQuery = Encoding.UTF8.GetBytes("{\"query\":\"");
            private static readonly byte[] endOfQueryNoParameters = Encoding.UTF8.GetBytes("\"}");
            private static readonly byte[] startParametersArray = Encoding.UTF8.GetBytes("\",\"parameters\":[{\"name\":\"");
            private static readonly byte[] middleOfParameter = Encoding.UTF8.GetBytes("\",\"value\":");
            private static readonly byte[] endAndStartNewParameter = Encoding.UTF8.GetBytes("},{\"name\":\"");
            private static readonly byte[] endOfParameterArray = Encoding.UTF8.GetBytes("}]}");

            /// <summary>
            /// Convert the SqlQuerySpec to stream. If the user specified a custom serializer this will
            /// convert the parameter properties via the custom serializer ensuring that any custom
            /// conversion logic is preserved.
            /// </summary>
            /// <param name="sqlQuerySpec">The SQL query spec to serialize</param>
            /// <param name="clientContext">The user serializer</param>
            /// <returns>A stream containing the serialized SqlQuerySpec</returns>
            public static Stream ToStream(
                SqlQuerySpec sqlQuerySpec,
                CosmosClientContext clientContext)
            {
                CosmosSerializer cosmosSerializer = clientContext.CosmosSerializer;

                // If there is no customer serializer then just use the default.
                if (clientContext.PropertiesSerializer == cosmosSerializer)
                {
                    return clientContext.PropertiesSerializer.ToStream<SqlQuerySpec>(sqlQuerySpec);
                }

                // An example of the JSON that the stream will contain:
                // "{\"query\":\"Select* from something\",\"parameters\":[{\"name\":\"@parameter\",\"value\":\"myvalue\"}]}";
                Stream memoryStream = new MemoryStream();

                memoryStream.Write(SqlQuerySpecToStream.startOfQuery, 0, startOfQuery.Length);
                byte[] queryText = Encoding.UTF8.GetBytes(sqlQuerySpec.QueryText);
                memoryStream.Write(queryText, 0, queryText.Length);

                int parameterCount = sqlQuerySpec.Parameters?.Count ?? 0;
                if (parameterCount == 0)
                {
                    memoryStream.Write(endOfQueryNoParameters, 0, endOfQueryNoParameters.Length);
                    memoryStream.Position = 0;
                    return memoryStream;
                }

                memoryStream.Write(startParametersArray, 0, startParametersArray.Length);
                for (int i = 0; i < parameterCount; i++)
                {
                    var paramater = sqlQuerySpec.Parameters[i];
                    byte[] name = Encoding.UTF8.GetBytes(paramater.Name);
                    memoryStream.Write(name, 0, name.Length);
                    memoryStream.Write(middleOfParameter, 0, middleOfParameter.Length);
                    using (Stream valueStream = cosmosSerializer.ToStream(paramater.Value))
                    {
                        valueStream.CopyTo(memoryStream);
                    }

                    if (i + 1 == parameterCount)
                    {
                        memoryStream.Write(endOfParameterArray, 0, endOfParameterArray.Length);
                    }
                    else
                    {
                        memoryStream.Write(endAndStartNewParameter, 0, endAndStartNewParameter.Length);
                    }
                }

                memoryStream.Position = 0;
                return memoryStream;
            }
        }
    }
}
