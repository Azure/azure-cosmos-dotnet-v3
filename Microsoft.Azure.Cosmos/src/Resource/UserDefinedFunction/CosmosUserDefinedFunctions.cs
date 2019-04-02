//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Operations for creating new user defined function, and reading/querying all user defined functions
    ///
    /// <see cref="CosmosUserDefinedFunction"/> for reading, replacing, or deleting an existing user defined functions.
    /// </summary>
    internal class CosmosUserDefinedFunctions
    {
        private readonly CosmosContainerCore container;
        private readonly CosmosClient client;

        /// <summary>
        /// Create a <see cref="CosmosUserDefinedFunctions"/>
        /// </summary>
        /// <param name="container">The <see cref="CosmosContainer"/> the user defined function set is related to.</param>
        protected internal CosmosUserDefinedFunctions(CosmosContainerCore container)
        {
            this.container = container;
            this.client = container.Client;
        }

        /// <summary>
        /// Creates a user defined function as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="userDefinedFunctionSettings">The <see cref="CosmosUserDefinedFunctionSettings"/> object.</param>
        /// <param name="requestOptions">(Optional) The options for the user defined function request <see cref="CosmosRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A task object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="userDefinedFunctionSettings"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occurred during async processing. Look within InnerExceptions to find the actual exception(s)</exception>
        /// <exception cref="CosmosException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a user defined function are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the request supplied. It is likely that an Id was not supplied for the new user defined function or that the Body was malformed.</description>
        ///     </item>
        ///     <item>
        ///         <term>403</term><description>Forbidden - You have reached your quota of user defined functions for the collection supplied. Contact support to have this quota increased.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a <see cref="CosmosUserDefinedFunctionSettings"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        ///     <item>
        ///         <term>413</term><description>RequestEntityTooLarge - This means the body of the <see cref="CosmosUserDefinedFunctionSettings"/> you tried to create was too large.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///  This creates a user defined function then uses the function in an item query.
        /// <code language="c#">
        /// <![CDATA[
        /// 
        /// await this.container.UserDefinedFunctions.CreateUserDefinedFunctionAsync(
        ///     new CosmosUserDefinedFunctionSettings 
        ///     { 
        ///         Id = "calculateTax", 
        ///         Body = @"function(amt) { return amt * 0.05; }" 
        ///     });
        ///
        /// CosmosSqlQueryDefinition sqlQuery = new CosmosSqlQueryDefinition(
        ///     "SELECT VALUE udf.calculateTax(t.cost) FROM toDoActivity t where t.cost > @expensive and t.status = @status")
        ///     .UseParameter("@expensive", 9000)
        ///     .UseParameter("@status", "Done");
        ///
        /// CosmosResultSetIterator<double> setIterator = this.container.Items.CreateItemQuery<double>(
        ///     sqlQueryDefinition: sqlQuery,
        ///     partitionKey: "Done");
        ///
        /// while (setIterator.HasMoreResults)
        /// {
        ///     foreach (var tax in await setIterator.FetchNextSetAsync())
        ///     {
        ///         Console.WriteLine(tax);
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public virtual Task<CosmosUserDefinedFunctionResponse> CreateUserDefinedFunctionAsync(
            CosmosUserDefinedFunctionSettings userDefinedFunctionSettings,
            CosmosRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<CosmosResponseMessage> response = ExecUtils.ProcessResourceOperationStreamAsync(
                this.container.Database.Client,
                this.container.LinkUri,
                ResourceType.UserDefinedFunction,
                OperationType.Create,
                requestOptions,
                partitionKey: null,
                streamPayload: CosmosResource.ToStream(userDefinedFunctionSettings),
                requestEnricher: null,
                cancellationToken: cancellationToken);

            return this.client.ResponseFactory.CreateUserDefinedFunctionResponse(this[userDefinedFunctionSettings.Id], response);
        }

        /// <summary>
        /// Gets an iterator to go through all the user defined functions for the container
        /// </summary>
        /// <param name="maxItemCount">(Optional) The max item count to return as part of the query</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <example>
        /// Get an iterator for all the triggers under the cosmos container
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosResultSetIterator<CosmosUserDefinedFunctionSettings> setIterator = this.container.UserDefinedFunctions.GetUserDefinedFunctionIterator();
        /// while (setIterator.HasMoreResults)
        /// {
        ///     foreach(CosmosUserDefinedFunctionSettings settings in await setIterator.FetchNextSetAsync())
        ///     {
        ///          Console.WriteLine(settings.Id); 
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public CosmosResultSetIterator<CosmosUserDefinedFunctionSettings> GetUserDefinedFunctionIterator(
            int? maxItemCount = null,
            string continuationToken = null)
        {
            return new CosmosDefaultResultSetIterator<CosmosUserDefinedFunctionSettings>(
                maxItemCount,
                continuationToken, 
                null, 
                this.ContainerFeedRequestExecutor);
        }

        /// <summary>
        /// Returns a reference to a user defined functions object. 
        /// </summary>
        /// <param name="id">The cosmos user defined functions id.</param>
        /// <remarks>
        /// Note that the user defined functions must be explicitly created, if it does not already exist, before
        /// you can read from it or write to it.
        /// </remarks>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosUserDefinedFunction userDefinedFunction = this.cosmosContainer.UserDefinedFunction["myUserDefinedFunctionId"];
        /// CosmosUserDefinedFunctionResponse response = await userDefinedFunction.ReadAsync();
        /// ]]>
        /// </code>
        /// </example>
        public CosmosUserDefinedFunction this[string id]
        {
            get { return new CosmosUserDefinedFunction(this.container, id); }
        }

        private Task<CosmosQueryResponse<CosmosUserDefinedFunctionSettings>> ContainerFeedRequestExecutor(
            int? maxItemCount,
            string continuationToken,
            CosmosRequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            Debug.Assert(state == null);

            return ExecUtils.ProcessResourceOperationAsync<CosmosQueryResponse<CosmosUserDefinedFunctionSettings>>(
                this.container.Database.Client,
                this.container.LinkUri,
                ResourceType.UserDefinedFunction,
                OperationType.ReadFeed,
                options,
                request =>
                {
                    CosmosQueryRequestOptions.FillContinuationToken(request, continuationToken);
                    CosmosQueryRequestOptions.FillMaxItemCount(request, maxItemCount);
                },
                response => this.client.ResponseFactory.CreateResultSetQueryResponse<CosmosUserDefinedFunctionSettings>(response),
                cancellationToken);
        }
    }
}
