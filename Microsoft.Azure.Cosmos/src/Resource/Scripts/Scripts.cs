//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Scripts
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents script operations on an Azure Cosmos container.
    /// </summary>
    /// <seealso cref="StoredProcedureProperties"/>
    /// <seealso cref="TriggerProperties"/>
    /// <seealso cref="UserDefinedFunctionProperties"/>
    public abstract class Scripts
    {
        /// <summary>
        /// Creates a stored procedure as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="storedProcedureProperties">The Stored Procedure to create.</param>
        /// <param name="requestOptions">(Optional) The options for the stored procedure request.</param>
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
        /// Scripts scripts = this.container.Scripts;
        /// StoredProcedureProperties storedProcedure = new StoredProcedureProperties(id, sprocBody);
        /// StoredProcedureResponse storedProcedureResponse = await scripts.CreateStoredProcedureAsync(storedProcedure);
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
        public abstract Task<StoredProcedureResponse> CreateStoredProcedureAsync(
                    StoredProcedureProperties storedProcedureProperties,
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default);

        /// <summary>
        /// This method creates a query for stored procedures under a container using a SQL statement. It returns a FeedIterator.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryDefinition">The cosmos SQL query definition.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request.</param>
        /// <returns>An iterator to read through the existing stored procedures.</returns>
        /// <example>
        /// This create the type feed iterator for sproc with queryDefinition as input.
        /// <code language="c#">
        /// <![CDATA[
        /// Scripts scripts = this.container.Scripts;
        /// string queryText = "SELECT * FROM s where s.id like @testId";
        /// QueryDefinition queryDefinition = new QueryDefinition(queryText);
        /// queryDefinition.WithParameter("@testId", "testSprocId");
        /// FeedIterator<StoredProcedureProperties> iter = this.scripts.GetStoredProcedureQueryIterator<StoredProcedureProperties>(queryDefinition);
        /// ]]>
        /// </code>
        /// </example>
        public abstract FeedIterator<T> GetStoredProcedureQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);

        /// <summary>
        /// This method creates a query for stored procedures under a container using a SQL statement. It returns a FeedIterator.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryDefinition">The cosmos SQL query definition.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request.</param>
        /// <returns>An iterator to read through the existing stored procedures.</returns>
        /// <example>
        /// This create the stream feed iterator for sproc with queryDefinition as input.
        /// <code language="c#">
        /// <![CDATA[
        /// Scripts scripts = this.container.Scripts;
        /// string queryText = "SELECT * FROM s where s.id like @testId";
        /// QueryDefinition queryDefinition = new QueryDefinition(queryText);
        /// queryDefinition.WithParameter("@testId", "testSprocId");
        /// FeedIterator iter = this.scripts.GetStoredProcedureQueryStreamIterator(queryDefinition);
        /// ]]>
        /// </code>
        /// </example>
        public abstract FeedIterator GetStoredProcedureQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);

        /// <summary>
        /// This method creates a query for stored procedures under a container using a SQL statement. It returns a FeedIterator.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryText">The cosmos SQL query text.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request.</param>
        /// <returns>An iterator to read through the existing stored procedures.</returns>
        /// <example>
        /// This create the type feed iterator for sproc with queryText as input.
        /// <code language="c#">
        /// <![CDATA[
        /// Scripts scripts = this.container.Scripts;
        /// FeedIterator<StoredProcedureProperties> feedIterator = this.scripts.GetStoredProcedureQueryIterator<StoredProcedureProperties>(
        ///     "SELECT * FROM u where u.id like '%testId%'");
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     foreach (var properties in await feedIterator.ReadNextAsync())
        ///     {
        ///         Console.WriteLine(properties.Id);
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract FeedIterator<T> GetStoredProcedureQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);

        /// <summary>
        /// This method creates a query for stored procedures under a container using a SQL statement. It returns a FeedIterator.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryText">The cosmos SQL query text.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request.</param>
        /// <returns>An iterator to read through the existing stored procedures.</returns>
        /// <example>
        /// This create the stream feed iterator for sproc with queryText as input.
        /// <code language="c#">
        /// <![CDATA[
        /// Scripts scripts = this.container.Scripts;
        /// string queryText = "SELECT * FROM s where s.id like '%testId%'";
        /// FeedIterator feedIterator = this.scripts.GetStoredProcedureQueryStreamIterator(queryText);
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     // Stream iterator returns a response with status for errors
        ///     using(ResponseMessage response = await feedIterator.ReadNextAsync())
        ///     {
        ///         // Handle failure scenario. 
        ///         if(!response.IsSuccessStatusCode)
        ///         {
        ///             // Log the response.Diagnostics and handle the error
        ///         }
        ///
        ///         // Process the response.Content Stream
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract FeedIterator GetStoredProcedureQueryStreamIterator(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);

        /// <summary>
        /// Reads a <see cref="StoredProcedureProperties"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="id">The identifier of the Stored Procedure to read.</param>
        /// <param name="requestOptions">(Optional) The options for the stored procedure request.</param>
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
        /// Scripts scripts = this.container.Scripts;
        /// StoredProcedureResponse storedProcedure = await scripts.ReadStoredProcedureAsync("ExistingId");
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<StoredProcedureResponse> ReadStoredProcedureAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Replaces a <see cref="StoredProcedureProperties"/> in the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="storedProcedureProperties">The Stored Procedure to replace</param>
        /// <param name="requestOptions">(Optional) The options for the stored procedure request.</param>
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
        /// Scripts scripts = this.container.Scripts;
        /// StoredProcedureResponse response = await scripts.ReplaceStoredProcedureAsync(new StoredProcedureProperties("testTriggerId", body));
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<StoredProcedureResponse> ReplaceStoredProcedureAsync(
            StoredProcedureProperties storedProcedureProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete a <see cref="StoredProcedureProperties"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="id">The identifier of the Stored Procedure to delete.</param>
        /// <param name="requestOptions">(Optional) The options for the stored procedure request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="ResponseMessage"/> which will contain the response to the request issued.</returns>
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
        /// Scripts scripts = this.container.Scripts;
        /// StoredProcedureResponse response = await scripts.DeleteStoredProcedureAsync("taxUdfId");
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<StoredProcedureResponse> DeleteStoredProcedureAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a stored procedure against a container as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <typeparam name="TOutput">The return type that is JSON serializable.</typeparam>
        /// <param name="storedProcedureId">The identifier of the Stored Procedure to execute.</param>
        /// <param name="partitionKey">The partition key for the item.</param>
        /// <param name="parameters">(Optional) An array of dynamic objects representing the parameters for the stored procedure.</param>
        /// <param name="requestOptions">(Optional) The options for the stored procedure request.</param>
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
        /// Scripts scripts = this.container.Scripts;
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
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a stored procedure against a container as an asynchronous operation in the Azure Cosmos service and obtains a Stream as response.
        /// </summary>
        /// <param name="storedProcedureId">The identifier of the Stored Procedure to execute.</param>
        /// <param name="partitionKey">The partition key for the item.</param>
        /// <param name="parameters">An array of dynamic objects representing the parameters for the stored procedure. This can be null if no parameters are required.</param>
        /// <param name="requestOptions">(Optional) The options for the stored procedure request.</param>
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
        /// Scripts scripts = this.container.Scripts;
        /// string sprocId = "appendString";
        /// StoredProcedureResponse storedProcedureResponse = await scripts.CreateStoredProcedureAsync(
        ///         sprocId,
        ///         sprocBody);
        /// 
        /// // Execute the stored procedure
        /// ResponseMessage sprocResponse = await scripts.ExecuteStoredProcedureStreamAsync(
        ///                         sprocId,
        ///                         new PartitionKey(testPartitionId),
        ///                         new dynamic[] {"myPrefixString", "myPostfixString"});
        ///                         
        /// using (StreamReader sr = new StreamReader(sprocResponse.Content))
        /// {
        ///     string stringResponse = await sr.ReadToEndAsync();
        ///     Console.WriteLine(stringResponse);
        ///  }
        /// 
        /// /// ]]>
        /// </code>
        /// </example>
        public abstract Task<ResponseMessage> ExecuteStoredProcedureStreamAsync(
            string storedProcedureId,
            PartitionKey partitionKey,
            dynamic[] parameters,
            StoredProcedureRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a stored procedure against a container as an asynchronous operation in the Azure Cosmos service and obtains a Stream as response.
        /// </summary>
        /// <param name="storedProcedureId">The identifier of the Stored Procedure to execute.</param>
        /// <param name="streamPayload">A <see cref="Stream"/> containing the payload which should represent a JSON array or arraylike object of parameters. This is parsed using JSON.parse and Function.apply uses the result to call the stored procedure. This can be null if no parameters are required.</param>
        /// <param name="partitionKey">The partition key for the item.</param>
        /// <param name="requestOptions">(Optional) The options for the stored procedure request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The task object representing the service response for the asynchronous operation which would contain any response set in the stored procedure. The response will contain status code (400) BadRequest if streamPayload represents anything other than a JSON array, object or null.</returns>
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
        /// Scripts scripts = this.container.Scripts;
        /// string sprocId = "appendString";
        /// StoredProcedureResponse storedProcedureResponse = await scripts.CreateStoredProcedureAsync(
        ///         sprocId,
        ///         sprocBody);
        ///
        /// // Serialize the parameters into a stream
        /// string[] parameters = new string[] { "myPrefixString", "myPostfixString" };
        /// byte[] serializedBytes = JsonSerializer.SerializeToUtf8Bytes(parameters);
        /// MemoryStream streamPayload = new MemoryStream(serializedBytes);
        /// 
        /// // Execute the stored procedure
        /// ResponseMessage sprocResponse = await scripts.ExecuteStoredProcedureStreamAsync(
        ///                         sprocId,
        ///                         streamPayload,
        ///                         new PartitionKey(testPartitionId));
        ///                         
        /// using (StreamReader sr = new StreamReader(sprocResponse.Content))
        /// {
        ///     string stringResponse = await sr.ReadToEndAsync();
        ///     Console.WriteLine(stringResponse);
        ///  }
        /// 
        /// /// ]]>
        /// </code>
        /// </example>
        public abstract Task<ResponseMessage> ExecuteStoredProcedureStreamAsync(
            string storedProcedureId,
            Stream streamPayload,
            PartitionKey partitionKey,
            StoredProcedureRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a trigger as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="triggerProperties">The <see cref="TriggerProperties"/> object.</param>
        /// <param name="requestOptions">(Optional) The options for the stored procedure request.</param>
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
        /// Scripts scripts = this.container.Scripts;
        /// TriggerResponse triggerResponse = await scripts.CreateTriggerAsync(
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
        public abstract Task<TriggerResponse> CreateTriggerAsync(
            TriggerProperties triggerProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// This method creates a query for triggers under a container using a SQL statement. It returns a FeedIterator.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryDefinition">The cosmos SQL query definition.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request.</param>
        /// <returns>An iterator to read through the existing stored procedures.</returns>
        /// <example>
        /// This create the type feed iterator for Trigger with queryDefinition as input.
        /// <code language="c#">
        /// <![CDATA[
        /// Scripts scripts = this.container.Scripts;
        /// QueryDefinition queryDefinition = new QueryDefinition("SELECT * FROM t where t.id like @testId")
        ///     .WithParameter("@testId", "testTriggerId");
        /// FeedIterator<TriggerProperties> feedIterator = this.scripts.GetTriggerQueryIterator<TriggerProperties>(queryDefinition);
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     foreach (var properties in await feedIterator.ReadNextAsync())
        ///     {
        ///         Console.WriteLine(properties.Id);
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract FeedIterator<T> GetTriggerQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);

        /// <summary>
        /// This method creates a query for triggers under a container using a SQL statement. It returns a FeedIterator.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryDefinition">The cosmos SQL query definition.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request.</param>
        /// <returns>An iterator to read through the existing stored procedures.</returns>
        /// <example>
        /// This create the stream feed iterator for Trigger with queryDefinition as input.
        /// <code language="c#">
        /// <![CDATA[
        /// Scripts scripts = this.container.Scripts;\
        /// QueryDefinition queryDefinition = new QueryDefinition("SELECT * FROM t where t.id like @testId")
        ///  .WithParameter("@testId", "testTriggerId");
        /// FeedIterator feedIterator = this.scripts.GetTriggerQueryStreamIterator(queryDefinition);
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     // Stream iterator returns a response with status for errors
        ///     using(ResponseMessage response = await feedIterator.ReadNextAsync())
        ///     {
        ///         // Handle failure scenario. 
        ///         if(!response.IsSuccessStatusCode)
        ///         {
        ///             // Log the response.Diagnostics and handle the error
        ///         }
        ///
        ///         // Process the response.Content Stream
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract FeedIterator GetTriggerQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);

        /// <summary>
        /// This method creates a query for triggers under a container using a SQL statement. It returns a FeedIterator.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryText">The cosmos SQL query text.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request.</param>
        /// <returns>An iterator to read through the existing stored procedures.</returns>
        /// <example>
        /// This create the type feed iterator for Trigger with queryText as input.
        /// <code language="c#">
        /// <![CDATA[
        /// Scripts scripts = this.container.Scripts;
        /// FeedIterator<TriggerProperties> feedIterator = this.scripts.GetTriggerQueryIterator<TriggerProperties>(
        ///     "SELECT * FROM t where t.id like '%testId%'");
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     foreach (var properties in await feedIterator.ReadNextAsync())
        ///     {
        ///         Console.WriteLine(properties.Id);
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract FeedIterator<T> GetTriggerQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);

        /// <summary>
        /// This method creates a query for triggers under a container using a SQL statement. It returns a FeedIterator.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryText">The cosmos SQL query text.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request.</param>
        /// <returns>An iterator to read through the existing stored procedures.</returns>
        /// <example>
        /// This create the stream feed iterator for Trigger with queryText as input.
        /// <code language="c#">
        /// <![CDATA[
        /// Scripts scripts = this.container.Scripts;
        /// string queryText = "SELECT * FROM t where t.id like '%testId%'";
        /// FeedIterator iter = this.scripts.GetTriggerQueryStreamIterator(queryText);
        /// ]]>
        /// </code>
        /// </example>
        public abstract FeedIterator GetTriggerQueryStreamIterator(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);

        /// <summary>
        /// Reads a <see cref="TriggerProperties"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="id">The id of the trigger to read.</param>
        /// <param name="requestOptions">(Optional) The options for the trigger request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="TriggerResponse"/> which wraps a <see cref="TriggerProperties"/> containing the read resource record.
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
        /// Scripts scripts = this.container.Scripts;
        /// TriggerResponse response = await scripts.ReadTriggerAsync("ExistingId");
        /// TriggerProperties triggerProperties = response;
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<TriggerResponse> ReadTriggerAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Replaces a <see cref="TriggerProperties"/> in the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="triggerProperties">The <see cref="TriggerProperties"/> object.</param>
        /// <param name="requestOptions">(Optional) The options for the trigger request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="TriggerResponse"/> which wraps a <see cref="TriggerProperties"/> containing the updated resource record.
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
        /// Scripts scripts = this.container.Scripts;
        /// TriggerResponse response = await scripts.ReplaceTriggerAsync(triggerSettigs);
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<TriggerResponse> ReplaceTriggerAsync(
                    TriggerProperties triggerProperties,
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete a <see cref="TriggerProperties"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="id">The id of the trigger to delete.</param>
        /// <param name="requestOptions">(Optional) The options for the trigger request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="TriggerResponse"/> which wraps a <see cref="TriggerProperties"/> which will contain information about the request issued.</returns>
        /// /// <example>
        /// This examples gets a reference to an existing trigger and deletes it.
        /// <code language="c#">
        /// <![CDATA[
        /// Scripts scripts = this.container.Scripts;
        /// TriggerResponse response = await scripts.DeleteTriggerAsync("existingId");
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<TriggerResponse> DeleteTriggerAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a user defined function as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="userDefinedFunctionProperties">The <see cref="UserDefinedFunctionProperties"/> object.</param>
        /// <param name="requestOptions">(Optional) The options for the user defined function request.</param>
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
        /// Scripts scripts = this.container.Scripts;
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
        /// FeedIterator<double> setIterator = this.container.Items.GetItemsQueryIterator<double>(
        ///     sqlQueryDefinition: sqlQuery,
        ///     partitionKey: "Done");
        ///
        /// while (setIterator.HasMoreResults)
        /// {
        ///     foreach (var tax in await setIterator.ReadNextAsync())
        ///     {
        ///         Console.WriteLine(tax);
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<UserDefinedFunctionResponse> CreateUserDefinedFunctionAsync(
            UserDefinedFunctionProperties userDefinedFunctionProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// This method creates a query for user defined functions under a container using a SQL statement. It returns a FeedIterator.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryDefinition">The cosmos SQL query definition.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request.</param>
        /// <returns>An iterator to read through the existing stored procedures.</returns>
        /// <example>
        /// This create the type feed iterator for UDF with queryDefinition as input.
        /// <code language="c#">
        /// <![CDATA[
        /// Scripts scripts = this.container.Scripts;
        /// QueryDefinition queryDefinition = new QueryDefinition("SELECT * FROM u where u.id like @testId")
        ///     .WithParameter("@testId", "testUDFId");
        /// FeedIterator<UserDefinedFunctionProperties> feedIterator = this.scripts.GetUserDefinedFunctionQueryIterator<UserDefinedFunctionProperties>(queryDefinition);
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     foreach (var properties in await feedIterator.ReadNextAsync())
        ///     {
        ///         Console.WriteLine(properties.Id);
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract FeedIterator<T> GetUserDefinedFunctionQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);

        /// <summary>
        /// This method creates a query for user defined functions under a container using a SQL statement. It returns a FeedIterator.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryDefinition">The cosmos SQL query definition.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request.</param>
        /// <returns>An iterator to read through the existing stored procedures.</returns>
        /// <example>
        /// This create the stream feed iterator for UDF with queryDefinition as input.
        /// <code language="c#">
        /// <![CDATA[
        /// Scripts scripts = this.container.Scripts;
        /// QueryDefinition queryDefinition = new QueryDefinition("SELECT * FROM u where u.id like @testId")
        ///   .WithParameter("@testId", "testUdfId");
        /// FeedIterator feedIterator = this.scripts.GetUserDefinedFunctionQueryStreamIterator(queryDefinition);
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     // Stream iterator returns a response with status for errors
        ///     using(ResponseMessage response = await feedIterator.ReadNextAsync())
        ///     {
        ///         // Handle failure scenario. 
        ///         if(!response.IsSuccessStatusCode)
        ///         {
        ///             // Log the response.Diagnostics and handle the error
        ///         }
        ///
        ///         // Process the response.Content Stream
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract FeedIterator GetUserDefinedFunctionQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);

        /// <summary>
        /// This method creates a query for user defined functions under a container using a SQL statement. It returns a FeedIterator.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryText">The cosmos SQL query text.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request.</param>
        /// <returns>An iterator to read through the existing stored procedures.</returns>
        /// <example>
        /// This create the type feed iterator for UDF with queryText as input.
        /// <code language="c#">
        /// <![CDATA[
        /// Scripts scripts = this.container.Scripts;
        /// FeedIterator<UserDefinedFunctionProperties> feedIterator = this.scripts.GetUserDefinedFunctionQueryIterator<UserDefinedFunctionProperties>(
        ///     "SELECT * FROM u where u.id like '%testId%'");
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     foreach (var properties in await feedIterator.ReadNextAsync())
        ///     {
        ///         Console.WriteLine(properties.Id);
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract FeedIterator<T> GetUserDefinedFunctionQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);

        /// <summary>
        /// This method creates a query for user defined functions under a container using a SQL statement. It returns a FeedIterator.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryText">The cosmos SQL query text.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request.</param>
        /// <returns>An iterator to read through the existing stored procedures.</returns>
        /// <example>
        /// This create the stream feed iterator for UDF with queryText as input.
        /// <code language="c#">
        /// <![CDATA[
        /// Scripts scripts = this.container.Scripts;
        /// FeedIterator feedIterator = this.scripts.GetUserDefinedFunctionQueryStreamIterator(
        ///     "SELECT * FROM u where u.id like '%testId%'");
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     // Stream iterator returns a response with status for errors
        ///     using(ResponseMessage response = await feedIterator.ReadNextAsync())
        ///     {
        ///         // Handle failure scenario. 
        ///         if(!response.IsSuccessStatusCode)
        ///         {
        ///             // Log the response.Diagnostics and handle the error
        ///         }
        ///
        ///         // Process the response.Content Stream
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract FeedIterator GetUserDefinedFunctionQueryStreamIterator(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);

        /// <summary>
        /// Reads a <see cref="UserDefinedFunctionProperties"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="id">The id of the user defined function to read</param>
        /// <param name="requestOptions">(Optional) The options for the user defined function request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="UserDefinedFunctionResponse"/> which wraps a <see cref="UserDefinedFunctionProperties"/> containing the read resource record.
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
        /// Scripts scripts = this.container.Scripts;
        /// UserDefinedFunctionResponse response = await scripts.ReadUserDefinedFunctionAsync("ExistingId");
        /// UserDefinedFunctionProperties udfProperties = response;
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<UserDefinedFunctionResponse> ReadUserDefinedFunctionAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Replaces a <see cref="UserDefinedFunctionProperties"/> in the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="userDefinedFunctionProperties">The <see cref="UserDefinedFunctionProperties"/> object.</param>
        /// <param name="requestOptions">(Optional) The options for the user defined function request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="UserDefinedFunctionResponse"/> which wraps a <see cref="UserDefinedFunctionProperties"/> containing the updated resource record.
        /// </returns>
        /// <example>
        /// This examples replaces an existing user defined function.
        /// <code language="c#">
        /// <![CDATA[
        /// Scripts scripts = this.container.Scripts;
        /// UserDefinedFunctionProperties udfProperties = new UserDefinedFunctionProperties
        /// {
        ///     Id = "testUserDefinedFunId",
        ///     Body = "function(amt) { return amt * 0.15; }",
        /// };
        /// 
        /// UserDefinedFunctionResponse response = await scripts.ReplaceUserDefinedFunctionAsync(udfProperties);
        /// UserDefinedFunctionProperties udfProperties = response;
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<UserDefinedFunctionResponse> ReplaceUserDefinedFunctionAsync(
            UserDefinedFunctionProperties userDefinedFunctionProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete a <see cref="UserDefinedFunctionProperties"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="id">The id of the user defined function to delete.</param>
        /// <param name="requestOptions">(Optional) The options for the user defined function request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="UserDefinedFunctionResponse"/> which wraps a <see cref="UserDefinedFunctionProperties"/> which will contain information about the request issued.</returns>
        /// <example>
        /// This examples gets a reference to an existing user defined function and deletes it.
        /// <code language="c#">
        /// <![CDATA[
        /// Scripts scripts = this.container.Scripts;
        /// UserDefinedFunctionResponse response = await this.container.DeleteUserDefinedFunctionAsync("existingId");
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<UserDefinedFunctionResponse> DeleteUserDefinedFunctionAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default);
    }
}
