//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Scripts
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents script operations on an Azure Cosmos container.
    /// </summary>
    /// <seealso cref="StoredProcedureProperties"/>
    /// <seealso cref="TriggerProperties"/>
    /// <seealso cref="UserDefinedFunctionProperties"/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "VSTHRD200:Use \"Async\" suffix for async methods", Justification = "AsyncPageable is not considered Async for checkers.")]
    public abstract class CosmosScripts
    {
        /// <summary>
        /// Creates a stored procedure as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="storedProcedureProperties">The Stored Procedure to create</param>
        /// <param name="requestOptions">(Optional) The options for the stored procedure request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The <see cref="StoredProcedureProperties"/> that was created contained within a <see cref="Task"/> object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="storedProcedureProperties"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occurred during async processing. Look within InnerExceptions to find the actual exception(s)</exception>
        /// <exception cref="CosmosException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the request supplied. It is likely that an Id was not supplied for the stored procedure or the Body was malformed.</description>
        ///     </item>
        ///     <item>
        ///         <term>403</term><description>Forbidden - You have reached your quota of stored procedures for the collection supplied. Contact support to have this quota increased.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a <see cref="StoredProcedureProperties"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        ///     <item>
        ///         <term>413</term><description>RequestEntityTooLarge - This means the body of the <see cref="StoredProcedureProperties"/> you tried to create was too large.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///  This creates and executes a stored procedure that appends a string to the first item returned from the query.
        /// <code language="c#">
        /// <![CDATA[
        /// string sprocBody = @"function simple(prefix)
        ///    {
        ///        var collection = getContext().getCollection();
        ///
        ///        // Query documents and take 1st item.
        ///        var isAccepted = collection.queryDocuments(
        ///        collection.getSelfLink(),
        ///        'SELECT * FROM root r',
        ///        function(err, feed, options) {
        ///            if (err)throw err;
        ///
        ///            // Check the feed and if it's empty, set the body to 'no docs found',
        ///            // Otherwise just take 1st element from the feed.
        ///            if (!feed || !feed.length) getContext().getResponse().setBody(""no docs found"");
        ///            else getContext().getResponse().setBody(prefix + JSON.stringify(feed[0]));
        ///        });
        ///
        ///        if (!isAccepted) throw new Error(""The query wasn't accepted by the server. Try again/use continuation token between API and script."");
        ///    }";
        ///    
        /// CosmosScripts scripts = this.container.Scripts;
        /// StoredProcedureProperties storedProcedure = new StoredProcedureProperties(id, sprocBody);
        /// Response<StoredProcedureProperties> storedProcedureResponse = await scripts.CreateStoredProcedureAsync(storedProcedure);
        /// 
        /// // Execute the stored procedure
        /// CosmosItemResponse<string> sprocResponse = await scripts.ExecuteStoredProcedureAsync<string, string>(
        ///                               id, 
        ///                               "Item as a string: ", 
        ///                               new PartitionKey(testPartitionId));
        /// Console.WriteLine("sprocResponse.Resource");
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<Response<StoredProcedureProperties>> CreateStoredProcedureAsync(
                    StoredProcedureProperties storedProcedureProperties,
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// This method creates a query for stored procedures under a container using a SQL statement. It returns an <see cref="AsyncPageable{T}"/>.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryDefinition">The cosmos SQL query definition.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request <see cref="QueryRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>An <see cref="AsyncPageable{T}"/> to read through the existing stored procedures.</returns>
        /// <example>
        /// This creates the enumerable for sproc with queryDefinition as input.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosScripts scripts = this.container.Scripts;
        /// string queryText = "SELECT * FROM s where s.id like @testId";
        /// QueryDefinition queryDefinition = new QueryDefinition(queryText);
        /// queryDefinition.WithParameter("@testId", "testSprocId");
        /// await foreach(StoredProcedureProperties storedProcedure in this.scripts.GetStoredProcedureQueryResultsAsync<StoredProcedureProperties>(queryDefinition))
        /// {
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract AsyncPageable<T> GetStoredProcedureQueryResultsAsync<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// This method creates a query for stored procedures under a container using a SQL statement. It returns an <see cref="IAsyncEnumerable{T}"/>.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryDefinition">The cosmos SQL query definition.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request <see cref="QueryRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>An <see cref="IAsyncEnumerable{T}"/> to read through the existing stored procedures.</returns>
        /// <example>
        /// This creates the enumerable for sproc with queryDefinition as input.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosScripts scripts = this.container.Scripts;
        /// string queryText = "SELECT * FROM s where s.id like @testId";
        /// QueryDefinition queryDefinition = new QueryDefinition(queryText);
        /// queryDefinition.WithParameter("@testId", "testSprocId");
        /// await foreach(Response storedProcedureStream in this.scripts.GetStoredProcedureQueryStreamResultsAsync(queryDefinition))
        /// {
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract IAsyncEnumerable<Response> GetStoredProcedureQueryStreamResultsAsync(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// This method creates a query for stored procedures under a container using a SQL statement. It returns an <see cref="AsyncPageable{T}"/>.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryText">The cosmos SQL query text.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request <see cref="QueryRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>An <see cref="AsyncPageable{T}"/> to read through the existing stored procedures.</returns>
        /// <example>
        /// This creates the enumerable for sproc with queryText as input.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosScripts scripts = this.container.Scripts;
        /// string queryText = "SELECT * FROM s where s.id like '%testId%'";
        /// await foreach(StoredProcedureProperties storedProcedure in this.scripts.GetStoredProcedureQueryResultsAsync<StoredProcedureProperties>(queryText))
        /// {
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract AsyncPageable<T> GetStoredProcedureQueryResultsAsync<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// This method creates a query for stored procedures under a container using a SQL statement. It returns an <see cref="IAsyncEnumerable{Response}"/>.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryText">The cosmos SQL query text.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request <see cref="QueryRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>An <see cref="IAsyncEnumerable{Response}"/> to read through the existing stored procedures.</returns>
        /// <example>
        /// This creates the enumerable for sproc with queryText as input.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosScripts scripts = this.container.Scripts;
        /// string queryText = "SELECT * FROM s where s.id like '%testId%'";
        /// await foreach(Response storedProcedureStream in this.scripts.GetStoredProcedureQueryStreamResultsAsync(queryText))
        /// {
        /// };
        /// ]]>
        /// </code>
        /// </example>
        public abstract IAsyncEnumerable<Response> GetStoredProcedureQueryStreamResultsAsync(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Reads a <see cref="StoredProcedureProperties"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="id">The identifier of the Stored Procedure to read.</param>
        /// <param name="requestOptions">(Optional) The options for the stored procedure request <see cref="StoredProcedureRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="StoredProcedureProperties"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="id"/> is not set.</exception>
        /// <exception cref="CosmosException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to read did not exist.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///  This reads an existing stored procedure.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosScripts scripts = this.container.Scripts;
        /// Response<StoredProcedureProperties> storedProcedure = await scripts.ReadStoredProcedureAsync("ExistingId");
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<Response<StoredProcedureProperties>> ReadStoredProcedureAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Replaces a <see cref="StoredProcedureProperties"/> in the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="storedProcedureProperties">The Stored Procedure to replace</param>
        /// <param name="requestOptions">(Optional) The options for the stored procedure request <see cref="StoredProcedureRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="StoredProcedureProperties"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="storedProcedureProperties"/> is not set.</exception>
        /// <exception cref="CosmosException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to delete did not exist.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// This examples replaces an existing stored procedure.
        /// <code language="c#">
        /// <![CDATA[
        /// //Updated body
        /// string body = @"function AddTax() {
        ///     var item = getContext().getRequest().getBody();
        ///
        ///     // Validate/calculate the tax.
        ///     item.tax = item.cost* .15;
        ///
        ///     // Update the request -- this is what is going to be inserted.
        ///     getContext().getRequest().setBody(item);
        /// }";
        /// 
        /// CosmosScripts scripts = this.container.Scripts;
        /// Response<StoredProcedureProperties> response = await scripts.ReplaceStoredProcedureAsync(new StoredProcedureProperties("testTriggerId", body));
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<Response<StoredProcedureProperties>> ReplaceStoredProcedureAsync(
            StoredProcedureProperties storedProcedureProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Delete a <see cref="StoredProcedureProperties"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="id">The identifier of the Stored Procedure to delete.</param>
        /// <param name="requestOptions">(Optional) The options for the stored procedure request <see cref="StoredProcedureRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="Response"/> which will contain the response to the request issued.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="id"/> are not set.</exception>
        /// <exception cref="CosmosException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to delete did not exist.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// This examples gets a reference to an existing stored procedure and deletes it.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosScripts scripts = this.container.Scripts;
        /// Response<StoredProcedureProperties> response = await scripts.DeleteStoredProcedureAsync("taxUdfId");
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<Response<StoredProcedureProperties>> DeleteStoredProcedureAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Executes a stored procedure against a container as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <typeparam name="TOutput">The return type that is JSON serializable.</typeparam>
        /// <param name="storedProcedureId">The identifier of the Stored Procedure to execute.</param>
        /// <param name="partitionKey">The partition key for the item. <see cref="Cosmos.PartitionKey"/></param>
        /// <param name="parameters">(Optional) An array of dynamic objects representing the parameters for the stored procedure.</param>
        /// <param name="requestOptions">(Optional) The options for the stored procedure request <see cref="StoredProcedureRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The task object representing the service response for the asynchronous operation which would contain any response set in the stored procedure.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="storedProcedureId"/> or <paramref name="partitionKey"/>  are not set.</exception>
        /// <example>
        ///  This creates and executes a stored procedure that appends a string to the first item returned from the query.
        /// <code language="c#">
        /// <![CDATA[
        /// string sprocBody = @"function simple(prefix, postfix)
        ///    {
        ///        var collection = getContext().getCollection();
        ///
        ///        // Query documents and take 1st item.
        ///        var isAccepted = collection.queryDocuments(
        ///        collection.getSelfLink(),
        ///        'SELECT * FROM root r',
        ///        function(err, feed, options) {
        ///            if (err)throw err;
        ///
        ///            // Check the feed and if it's empty, set the body to 'no docs found',
        ///            // Otherwise just take 1st element from the feed.
        ///            if (!feed || !feed.length) getContext().getResponse().setBody(""no docs found"");
        ///            else getContext().getResponse().setBody(prefix + JSON.stringify(feed[0]) + postfix);
        ///        });
        ///
        ///        if (!isAccepted) throw new Error(""The query wasn't accepted by the server. Try again/use continuation token between API and script."");
        ///    }";
        ///    
        /// CosmosScripts scripts = this.container.Scripts;
        /// string sprocId = "appendString";
        /// StoredProcedureResponse storedProcedureResponse = await scripts.CreateStoredProcedureAsync(
        ///         sprocId,
        ///         sprocBody);
        /// 
        /// // Execute the stored procedure
        /// StoredProcedureExecuteResponse<string> sprocResponse = await scripts.ExecuteStoredProcedureAsync<string>(
        ///                         sprocId,
        ///                         new PartitionKey(testPartitionId),
        ///                         new dynamic[] {"myPrefixString", "myPostfixString"});
        ///                         
        /// Console.WriteLine(sprocResponse.Resource);
        /// /// ]]>
        /// </code>
        /// </example>
        public abstract Task<StoredProcedureExecuteResponse<TOutput>> ExecuteStoredProcedureAsync<TOutput>(
            string storedProcedureId,
            PartitionKey partitionKey,
            dynamic[] parameters,
            StoredProcedureRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Executes a stored procedure against a container as an asynchronous operation in the Azure Cosmos service and obtains a Stream as response.
        /// </summary>
        /// <param name="storedProcedureId">The identifier of the Stored Procedure to execute.</param>
        /// <param name="partitionKey">The partition key for the item. <see cref="Cosmos.PartitionKey"/></param>
        /// <param name="parameters">(Optional) An array of dynamic objects representing the parameters for the stored procedure.</param>
        /// <param name="requestOptions">(Optional) The options for the stored procedure request <see cref="StoredProcedureRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The task object representing the service response for the asynchronous operation which would contain any response set in the stored procedure.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="storedProcedureId"/> or <paramref name="partitionKey"/>  are not set.</exception>
        /// <example>
        ///  This creates and executes a stored procedure that appends a string to the first item returned from the query.
        /// <code language="c#">
        /// <![CDATA[
        /// string sprocBody = @"function simple(prefix, postfix)
        ///    {
        ///        var collection = getContext().getCollection();
        ///
        ///        // Query documents and take 1st item.
        ///        var isAccepted = collection.queryDocuments(
        ///        collection.getSelfLink(),
        ///        'SELECT * FROM root r',
        ///        function(err, feed, options) {
        ///            if (err)throw err;
        ///
        ///            // Check the feed and if it's empty, set the body to 'no docs found',
        ///            // Otherwise just take 1st element from the feed.
        ///            if (!feed || !feed.length) getContext().getResponse().setBody(""no docs found"");
        ///            else getContext().getResponse().setBody(prefix + JSON.stringify(feed[0]) + postfix);
        ///        });
        ///
        ///        if (!isAccepted) throw new Error(""The query wasn't accepted by the server. Try again/use continuation token between API and script."");
        ///    }";
        ///    
        /// CosmosScripts scripts = this.container.Scripts;
        /// string sprocId = "appendString";
        /// Response<StoredProcedureProperties> storedProcedureResponse = await scripts.CreateStoredProcedureAsync(
        ///         sprocId,
        ///         sprocBody);
        /// 
        /// // Execute the stored procedure
        /// Response sprocResponse = await scripts.ExecuteStoredProcedureStreamAsync(
        ///                         sprocId,
        ///                         new PartitionKey(testPartitionId),
        ///                         new dynamic[] {"myPrefixString", "myPostfixString"});
        ///                         
        /// using (StreamReader sr = new StreamReader(sprocResponse.ContentStream))
        /// {
        ///     string stringResponse = await sr.ReadToEndAsync();
        ///     Console.WriteLine(stringResponse);
        ///  }
        /// 
        /// /// ]]>
        /// </code>
        /// </example>
        public abstract Task<Response> ExecuteStoredProcedureStreamAsync(
            string storedProcedureId,
            PartitionKey partitionKey,
            dynamic[] parameters,
            StoredProcedureRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Creates a trigger as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="triggerProperties">The <see cref="TriggerProperties"/> object.</param>
        /// <param name="requestOptions">(Optional) The options for the stored procedure request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A task object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="triggerProperties"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occurred during async processing. Look within InnerExceptions to find the actual exception(s)</exception>
        /// <exception cref="CosmosException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the request supplied. It is likely that an Id was not supplied for the new trigger or that the Body was malformed.</description>
        ///     </item>
        ///     <item>
        ///         <term>403</term><description>Forbidden - You have reached your quota of triggers for the collection supplied. Contact support to have this quota increased.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a <see cref="TriggerProperties"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        ///     <item>
        ///         <term>413</term><description>RequestEntityTooLarge - This means the body of the <see cref="TriggerProperties"/> you tried to create was too large.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///  This creates a trigger then uses the trigger in a create item.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosScripts scripts = this.container.Scripts;
        /// Response<TriggerProperties> triggerResponse = await scripts.CreateTriggerAsync(
        ///     new TriggerProperties
        ///     {
        ///         Id = "addTax",
        ///         Body = @"function AddTax() {
        ///             var item = getContext().getRequest().getBody();
        ///
        ///             // calculate the tax.
        ///             item.tax = item.cost * .15;
        ///
        ///             // Update the request -- this is what is going to be inserted.
        ///             getContext().getRequest().setBody(item);
        ///         }",
        ///         TriggerOperation = TriggerOperation.All,
        ///         TriggerType = TriggerType.Pre
        ///     });
        ///
        /// ItemRequestOptions options = new ItemRequestOptions()
        /// {
        ///     PreTriggers = new List<string>() { triggerResponse.Id },
        /// };
        ///
        /// // Create a new item with trigger set in the request options
        /// ItemResponse<dynamic> createdItem = await this.container.Items.CreateItemAsync<dynamic>(item.status, item, options);
        /// double itemTax = createdItem.Resource.tax;
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<Response<TriggerProperties>> CreateTriggerAsync(
            TriggerProperties triggerProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// This method creates a query for triggers under a container using a SQL statement. It returns an <see cref="AsyncPageable{T}"/>.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryDefinition">The cosmos SQL query definition.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request <see cref="QueryRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>An <see cref="AsyncPageable{T}"/> to read through the existing triggers.</returns>
        /// <example>
        /// This creates the enumerable for Trigger with queryDefinition as input.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosScripts scripts = this.container.Scripts;
        /// string queryText = "SELECT * FROM t where t.id like @testId";
        /// QueryDefinition queryDefinition = new QueryDefinition(queryText);
        /// queryDefinition.WithParameter("@testId", "testTriggerId");
        /// await forach(TriggerProperties trigger in this.scripts.GetTriggerQueryResultsAsync<TriggerProperties>(queryDefinition))
        /// {
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract AsyncPageable<T> GetTriggerQueryResultsAsync<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// This method creates a query for triggers under a container using a SQL statement. It returns an <see cref="IAsyncEnumerable{Response}"/>.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryDefinition">The cosmos SQL query definition.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request <see cref="QueryRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>An <see cref="IAsyncEnumerable{Response}"/> to read through the existing triggers.</returns>
        /// <example>
        /// This creates the enumerable for Triggers with queryDefinition as input.
        /// <code language="c#">
        /// <![CDATA[
        /// Scripts scripts = this.container.Scripts;
        /// string queryText = "SELECT * FROM t where t.id like @testId";
        /// QueryDefinition queryDefinition = new QueryDefinition(queryText);
        /// queryDefinition.WithParameter("@testId", "testTriggerId");
        /// await foreach(Response triggerStream in this.scripts.GetTriggerQueryStreamResultsAsync(queryDefinition))
        /// {
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract IAsyncEnumerable<Response> GetTriggerQueryStreamResultsAsync(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// This method creates a query for triggers under a container using a SQL statement. It returns an <see cref="AsyncPageable{T}"/>.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryText">The cosmos SQL query text.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request <see cref="QueryRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>An <see cref="AsyncPageable{T}"/> to read through the existing triggers.</returns>
        /// <example>
        /// This creates the enumerable for Trigger with queryText as input.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosScripts scripts = this.container.Scripts;
        /// string queryText = "SELECT * FROM t where t.id like '%testId%'";
        /// await foreach(TriggerProperties trigger in this.scripts.GetTriggerQueryResultsAsync<TriggerProperties>(queryText))
        /// {
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract AsyncPageable<T> GetTriggerQueryResultsAsync<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// This method creates a query for triggers under a container using a SQL statement. It returns an <see cref="IAsyncEnumerable{Response}"/>.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryText">The cosmos SQL query text.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request <see cref="QueryRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>An <see cref="IAsyncEnumerable{Response}"/> to read through the existing triggers.</returns>
        /// <example>
        /// This creates the enumerable for Trigger with queryText as input.
        /// <code language="c#">
        /// <![CDATA[
        /// Scripts scripts = this.container.Scripts;
        /// string queryText = "SELECT * FROM t where t.id like '%testId%'";
        /// await foreach(Response triggerStream in this.scripts.GetTriggerQueryStreamResultsAsync(queryText))
        /// {
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract IAsyncEnumerable<Response> GetTriggerQueryStreamResultsAsync(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Reads a <see cref="TriggerProperties"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="id">The id of the trigger to read.</param>
        /// <param name="requestOptions">(Optional) The options for the trigger request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="Response{T}"/> which wraps a <see cref="TriggerProperties"/> containing the read resource record.
        /// </returns>
        /// <exception cref="CosmosException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///  This reads an existing trigger
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosScripts scripts = this.container.Scripts;
        /// Response<TriggerProperties> response = await scripts.ReadTriggerAsync("ExistingId");
        /// TriggerProperties triggerProperties = response;
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<Response<TriggerProperties>> ReadTriggerAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Replaces a <see cref="TriggerProperties"/> in the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="triggerProperties">The <see cref="TriggerProperties"/> object.</param>
        /// <param name="requestOptions">(Optional) The options for the trigger request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="Response{T}"/> which wraps a <see cref="TriggerProperties"/> containing the updated resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="triggerProperties"/> is not set.</exception>
        /// <example>
        /// This examples replaces an existing trigger.
        /// <code language="c#">
        /// <![CDATA[
        /// TriggerProperties triggerProperties = new TriggerProperties
        /// {
        ///     Id = "testTriggerId",
        ///     Body = @"function AddTax() {
        ///         var item = getContext().getRequest().getBody();
        ///
        ///         // Validate/calculate the tax.
        ///         item.tax = item.cost* .15;
        ///
        ///         // Update the request -- this is what is going to be inserted.
        ///         getContext().getRequest().setBody(item);
        ///     }",
        ///     TriggerOperation = TriggerOperation.All,
        ///     TriggerType = TriggerType.Post
        /// };
        /// 
        /// CosmosScripts scripts = this.container.Scripts;
        /// Response<TriggerProperties> response = await scripts.ReplaceTriggerAsync(triggerSettigs);
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<Response<TriggerProperties>> ReplaceTriggerAsync(
                    TriggerProperties triggerProperties,
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Delete a <see cref="TriggerProperties"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="id">The id of the trigger to delete.</param>
        /// <param name="requestOptions">(Optional) The options for the trigger request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="Response{T}"/> which wraps a <see cref="TriggerProperties"/> which will contain information about the request issued.</returns>
        /// /// <example>
        /// This examples gets a reference to an existing trigger and deletes it.
        /// <code language="c#">
        /// <![CDATA[
        /// Scripts scripts = this.container.Scripts;
        /// Response<TriggerProperties> response = await scripts.DeleteTriggerAsync("existingId");
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<Response<TriggerProperties>> DeleteTriggerAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Creates a user defined function as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="userDefinedFunctionProperties">The <see cref="UserDefinedFunctionProperties"/> object.</param>
        /// <param name="requestOptions">(Optional) The options for the user defined function request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A task object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="userDefinedFunctionProperties"/> is not set.</exception>
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
        ///         <term>409</term><description>Conflict - This means a <see cref="UserDefinedFunctionProperties"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        ///     <item>
        ///         <term>413</term><description>RequestEntityTooLarge - This means the body of the <see cref="UserDefinedFunctionProperties"/> you tried to create was too large.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///  This creates a user defined function then uses the function in an item query.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosScripts scripts = this.container.Scripts;
        /// await scripts.UserDefinedFunctions.CreateUserDefinedFunctionAsync(
        ///     new UserDefinedFunctionProperties 
        ///     { 
        ///         Id = "calculateTax", 
        ///         Body = @"function(amt) { return amt * 0.05; }" 
        ///     });
        ///
        /// QueryDefinition sqlQuery = new QueryDefinition(
        ///     "SELECT VALUE udf.calculateTax(t.cost) FROM toDoActivity t where t.cost > @expensive and t.status = @status")
        ///     .WithParameter("@expensive", 9000)
        ///     .WithParameter("@status", "Done");
        ///
        /// await foreach (double tax in this.container.Items.GetItemsQueryResultsAsync<double>(
        ///     sqlQueryDefinition: sqlQuery,
        ///     partitionKey: "Done"))
        /// {
        ///     Console.WriteLine(tax);
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<Response<UserDefinedFunctionProperties>> CreateUserDefinedFunctionAsync(
            UserDefinedFunctionProperties userDefinedFunctionProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// This method creates a query for user defined functions under a container using a SQL statement. It returns an <see cref="AsyncPageable{T}"/>.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryDefinition">The cosmos SQL query definition.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request <see cref="QueryRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>An <see cref="AsyncPageable{T}"/> to read through the existing user defined functions.</returns>
        /// <example>
        /// This creates the enumerable for UDF with queryDefinition as input.
        /// <code language="c#">
        /// <![CDATA[
        /// Scripts scripts = this.container.Scripts;
        /// string queryText = "SELECT * FROM u where u.id like @testId";
        /// QueryDefinition queryDefinition = new QueryDefinition(queryText);
        /// queryDefinition.WithParameter("@testId", "testUDFId");
        /// await foreach(UserDefinedFunctionProperties udf in this.scripts.GetUserDefinedFunctionQueryResultsAsync<UserDefinedFunctionProperties>(queryDefinition))
        /// {
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract AsyncPageable<T> GetUserDefinedFunctionQueryResultsAsync<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// This method creates a query for user defined functions under a container using a SQL statement. It returns an <see cref="IAsyncEnumerable{Response}"/>.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryDefinition">The cosmos SQL query definition.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request <see cref="QueryRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>An <see cref="IAsyncEnumerable{Response}"/> to read through the existing user defined functions.</returns>
        /// <example>
        /// This creates the enumerable for UDF with queryDefinition as input.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosScripts scripts = this.container.Scripts;
        /// string queryText = "SELECT * FROM u where u.id like @testId";
        /// QueryDefinition queryDefinition = new QueryDefinition(queryText);
        /// queryDefinition.WithParameter("@testId", "testUdfId");
        /// await foreach(Response udfStream in this.scripts.GetUserDefinedFunctionQueryStreamResultsAsync(queryDefinition))
        /// {
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract IAsyncEnumerable<Response> GetUserDefinedFunctionQueryStreamResultsAsync(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// This method creates a query for user defined functions under a container using a SQL statement. It returns an <see cref="AsyncPageable{T}"/>.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryText">The cosmos SQL query text.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request <see cref="QueryRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>An <see cref="AsyncPageable{T}"/> to read through the existing user defined functions.</returns>
        /// <example>
        /// This creates the enumerable for UDF with queryText as input.
        /// <code language="c#">
        /// <![CDATA[
        /// Scripts scripts = this.container.Scripts;
        /// QueryDefinition queryDefinition = new QueryDefinition("SELECT * FROM u where u.id like '%testId%'");
        /// await foreach(UserDefinedFunctionProperties udf in this.scripts.GetUserDefinedFunctionQueryResultsAsync<UserDefinedFunctionProperties>(queryDefinition))
        /// {
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract AsyncPageable<T> GetUserDefinedFunctionQueryResultsAsync<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// This method creates a query for user defined functions under a container using a SQL statement. It returns an <see cref="IAsyncEnumerable{Response}"/>.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryText">The cosmos SQL query text.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request <see cref="QueryRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>An <see cref="IAsyncEnumerable{Response}"/> to read through the existing user defined functions.</returns>
        /// <example>
        /// This creates the enumerable for UDF with queryText as input.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosScripts scripts = this.container.Scripts;
        /// QueryDefinition queryDefinition = new QueryDefinition("SELECT * FROM u where u.id like '%testId%'");
        /// await foreach(Response udfStream in this.scripts.GetUserDefinedFunctionQueryStreamResultsAsync(queryDefinition))
        /// {
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract IAsyncEnumerable<Response> GetUserDefinedFunctionQueryStreamResultsAsync(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Reads a <see cref="UserDefinedFunctionProperties"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="id">The id of the user defined function to read</param>
        /// <param name="requestOptions">(Optional) The options for the user defined function request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="Response{T}"/> which wraps a <see cref="UserDefinedFunctionProperties"/> containing the read resource record.
        /// </returns>
        /// <exception cref="CosmosException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to read did not exist.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///  This reads an existing user defined function.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosScripts scripts = this.container.Scripts;
        /// Response<UserDefinedFunctionProperties> response = await scripts.ReadUserDefinedFunctionAsync("ExistingId");
        /// UserDefinedFunctionProperties udfProperties = response;
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<Response<UserDefinedFunctionProperties>> ReadUserDefinedFunctionAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Replaces a <see cref="UserDefinedFunctionProperties"/> in the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="userDefinedFunctionProperties">The <see cref="UserDefinedFunctionProperties"/> object.</param>
        /// <param name="requestOptions">(Optional) The options for the user defined function request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="Response{T}"/> which wraps a <see cref="UserDefinedFunctionProperties"/> containing the updated resource record.
        /// </returns>
        /// <example>
        /// This examples replaces an existing user defined function.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosScripts scripts = this.container.Scripts;
        /// UserDefinedFunctionProperties udfProperties = new UserDefinedFunctionProperties
        /// {
        ///     Id = "testUserDefinedFunId",
        ///     Body = "function(amt) { return amt * 0.15; }",
        /// };
        /// 
        /// Response<UserDefinedFunctionProperties> response = await scripts.ReplaceUserDefinedFunctionAsync(udfProperties);
        /// UserDefinedFunctionProperties udfProperties = response;
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<Response<UserDefinedFunctionProperties>> ReplaceUserDefinedFunctionAsync(
            UserDefinedFunctionProperties userDefinedFunctionProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Delete a <see cref="UserDefinedFunctionProperties"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="id">The id of the user defined function to delete.</param>
        /// <param name="requestOptions">(Optional) The options for the user defined function request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="Response{T}"/> which wraps a <see cref="UserDefinedFunctionProperties"/> which will contain information about the request issued.</returns>
        /// <example>
        /// This examples gets a reference to an existing user defined function and deletes it.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosScripts scripts = this.container.Scripts;
        /// Response<UserDefinedFunctionProperties> response = await this.container.DeleteUserDefinedFunctionAsync("existingId");
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<Response<UserDefinedFunctionProperties>> DeleteUserDefinedFunctionAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));
    }
}
