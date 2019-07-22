//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.Text;

    /// <summary>
    /// Represents a SQL query in the Azure Cosmos DB service.
    /// </summary>
    [DataContract]
    internal sealed class SqlQuerySpec
    {
        private string queryText;
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

            this.queryText = queryText;
            this.parameters = parameters;
        }

        /// <summary>
        /// Gets or sets the text of the Azure Cosmos DB database query.
        /// </summary>
        /// <value>The text of the database query.</value>
        [DataMember(Name = "query")]
        public string QueryText
        {
            get { return this.queryText; }
            set { this.queryText = value; }
        }

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
            private static readonly byte[] startOfQuery = Encoding.UTF8.GetBytes("{\"query\":");   
            private static readonly byte[] startParametersArray = Encoding.UTF8.GetBytes(",\"parameters\":[");
            private static readonly byte[] parameterNameKey = Encoding.UTF8.GetBytes("{\"name\":");
            private static readonly byte[] parameterValueKey = Encoding.UTF8.GetBytes(",\"value\":");
            private static readonly byte[] doubleQuote = Encoding.UTF8.GetBytes("\"");
            private static readonly byte[] comma = Encoding.UTF8.GetBytes(",");
            private static readonly byte[] endObject = Encoding.UTF8.GetBytes("}");
            private static readonly byte[] endArray = Encoding.UTF8.GetBytes("]");

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
                // If there is no custom serializer then just use the default.
                if (object.ReferenceEquals(clientContext.PropertiesSerializer, clientContext.CosmosSerializer))
                {
                    return clientContext.PropertiesSerializer.ToStream<SqlQuerySpec>(sqlQuerySpec);
                }

                // An example of the JSON that the stream will contain:
                // "{"query":"Select* from something","parameters":[{"name":"@parameter","value":"myvalue"}]}";
                MemoryStream memoryStream = new MemoryStream();

                // Write JSON '{"query":'
                Write(startOfQuery, memoryStream);

                // Write JSON '"QueryText"'
                WriteStringText(sqlQuerySpec.queryText, memoryStream);

                // Write the parameters
                WriteParametersToStream(
                    memoryStream,
                    sqlQuerySpec,
                    clientContext);

                // End the JSON object '}'
                Write(endObject, memoryStream);

                // Reset the position so reading starts at the beginning of the stream
                memoryStream.Position = 0;
                return memoryStream;
            }

            /// <summary>
            ///  Write JSON '"QueryText"'
            /// </summary>
            private static void WriteStringText(
                string inputText,
                MemoryStream memoryStream)
            {
                // Write JSON '"'
                Write(doubleQuote, memoryStream);

                // Write query text
                byte[] inputToWrite = Encoding.UTF8.GetBytes(inputText);
                Write(inputToWrite, memoryStream);

                // Write JSON '"'
                Write(doubleQuote, memoryStream);
            }

            /// <summary>
            /// Write a JSON object ',"parameters":[{"name":"parameterName","value":valueStream}]'
            /// </summary>
            private static void WriteParametersToStream(
                MemoryStream memoryStream,
                SqlQuerySpec sqlQuerySpec,
                CosmosClientContext clientContext)
            {
                // No parameters to write
                if (sqlQuerySpec.parameters == null || !sqlQuerySpec.parameters.Any())
                {
                    return;
                }

                // Write JSON ',"parameters":['
                Write(startParametersArray, memoryStream);

                int totalParameterCount = sqlQuerySpec.Parameters.Count;
                int lastPos = totalParameterCount - 1;
                for (int i = 0; i < totalParameterCount; i++)
                {
                    SqlParameter sqlParameter = sqlQuerySpec.Parameters[i];
                    using (Stream valueStream = clientContext.CosmosSerializer.ToStream(sqlParameter.Value))
                    {
                        // Write JSON '{"name":"parameterName","value":valueStream}'
                        WriteParameter(sqlParameter.Name, valueStream, memoryStream);
                    }

                    // Separate each object in the array with a comma
                    if (i != lastPos)
                    {
                        Write(comma, memoryStream);
                    }
                }

                // End the Array ']'
                Write(endArray, memoryStream);
            }

            /// <summary>
            /// Write a JSON object '{"name":parameterName,"value":valueStream}'
            /// </summary>
            private static void WriteParameter(string parameterName, Stream valueStream, MemoryStream memoryStream)
            {
                // {"name":
                Write(parameterNameKey, memoryStream);
                WriteStringText(parameterName, memoryStream);

                // ,"value":
                Write(parameterValueKey, memoryStream);
                valueStream.CopyTo(memoryStream);

                // }
                Write(endObject, memoryStream);
            }

            private static void Write(byte[] input, MemoryStream memoryStream)
            {
                memoryStream.Write(input, 0, input.Length);
            }
        }
    }
}
