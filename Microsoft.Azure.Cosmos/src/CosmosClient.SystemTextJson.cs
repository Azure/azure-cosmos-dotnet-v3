//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Net;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Text.Json.Serialization.Metadata;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure;
    using global::Azure.Core;
    using Microsoft.Azure.Cosmos.Authorization;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Telemetry.OpenTelemetry;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;
    using ResourceType = Documents.ResourceType;

    /// <summary>
    /// Provides overloads which can be used with System.Text.Json source generation.
    /// </summary>
    public partial class CosmosClient : IDisposable
    {
        /// <summary>
        /// This method creates a query for databases under an Cosmos DB Account using a SQL statement. It returns a FeedIterator.
        /// </summary>
        /// <param name="typeInfo">The type information for the database properties.</param>
        /// <param name="queryText">The cosmos SQL query text.</param>
        /// <param name="continuationToken">The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request.</param>
        /// <returns>An iterator to go through the databases.</returns>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions</exception>
        /// <remarks>
        /// Refer to https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.
        /// <para>
        /// <see cref="Database.ReadAsync(RequestOptions, CancellationToken)" /> is recommended for single database look-up.
        /// </para>
        /// </remarks>
        /// <example>
        /// This create the type feed iterator for database with queryText as input,
        /// <code language="c#">
        /// <![CDATA[
        /// string queryText = "SELECT * FROM c where c.status like 'start%'";
        /// using (FeedIterator<DatabaseProperties> feedIterator = this.users.GetDatabaseQueryIterator<DatabaseProperties>(queryText)
        /// {
        ///     while (feedIterator.HasMoreResults)
        ///     {
        ///         FeedResponse<DatabaseProperties> response = await feedIterator.ReadNextAsync();
        ///         foreach (var database in response)
        ///         {
        ///             Console.WriteLine(database);
        ///         }
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public virtual FeedIterator<T> GetDatabaseQueryIterator<T>(
            JsonTypeInfo<T[]> typeInfo,
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return new FeedIteratorInlineCore<T>(
                this.GetDatabaseQueryIteratorHelper<T>(
                    typeInfo,
                    queryDefinition,
                    continuationToken,
                    requestOptions),
                this.ClientContext);
        }

        private FeedIteratorInternal<T> GetDatabaseQueryIteratorHelper<T>(
            JsonTypeInfo<T[]> typeInfo,
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            if (!(this.GetDatabaseQueryStreamIteratorHelper(
                queryDefinition,
                continuationToken,
                requestOptions) is FeedIteratorInternal databaseStreamIterator))
            {
                throw new InvalidOperationException($"Expected a FeedIteratorInternal.");
            }

            return new FeedIteratorCore<T>(
                    databaseStreamIterator,
                    (response) => this.ClientContext.ResponseFactory.CreateQueryFeedResponse<T>(
                        typeInfo,
                        responseMessage: response,
                        resourceType: ResourceType.Database));
        }
    }
}
