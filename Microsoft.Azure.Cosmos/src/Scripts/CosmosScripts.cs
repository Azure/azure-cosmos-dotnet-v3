//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Scripts
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents script operations on an Azure Cosmos container.
    /// </summary>
    /// <seealso cref="CosmosStoredProcedureSettings"/>
    /// <seealso cref="CosmosTriggerSettings"/>
    /// <seealso cref="CosmosUserDefinedFunctionSettings"/>
    public abstract class CosmosScripts
    {
        /// <summary>
        /// Creates a stored procedure as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="id">The identifier of the Stored Procedure to create.</param>
        /// <param name="body">The JavaScript function that is the body of the stored procedure</param>
        /// <param name="requestOptions">(Optional) The options for the stored procedure request <see cref="CosmosRequestOptions"/></param>
        /// <param name="cancellation">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The <see cref="CosmosStoredProcedureSettings"/> that was created contained within a <see cref="Task"/> object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">If either <paramref name="id"/> or <paramref name="body"/> is not set.</exception>
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
        ///         <term>409</term><description>Conflict - This means a <see cref="CosmosStoredProcedureSettings"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        ///     <item>
        ///         <term>413</term><description>RequestEntityTooLarge - This means the body of the <see cref="CosmosStoredProcedureSettings"/> you tried to create was too large.</description>
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
        /// CosmosScripts scripts = this.container.GetScripts();
        /// CosmosStoredProcedure cosmosStoredProcedure = await scripts.CreateStoredProceducreAsync(
        ///         id: "appendString",
        ///         body: sprocBody);
        /// 
        /// // Execute the stored procedure
        /// CosmosItemResponse<string> sprocResponse = await scripts.ExecuteStoredProcedureAsync<string, string>(testPartitionId, "appendString", "Item as a string: ");
        /// Console.WriteLine("sprocResponse.Resource");
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<CosmosStoredProcedureResponse> CreateStoredProcedureAsync(
                    string id,
                    string body,
                    CosmosRequestOptions requestOptions = null,
                    CancellationToken cancellation = default(CancellationToken));

        /// <summary>
        /// Gets an iterator to go through all the stored procedures for the container
        /// </summary>
        /// <param name="maxItemCount">(Optional) The max item count to return as part of the query</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <example>
        /// Get an iterator for all the stored procedures under the cosmos container
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosScripts scripts = this.container.GetScripts();
        /// CosmosResultSetIterator<CosmosStoredProcedure> setIterator = scripts.GetStoredProcedureIterator();
        /// while (setIterator.HasMoreResults)
        /// {
        ///     foreach(CosmosStoredProcedure storedProcedure in await setIterator.FetchNextSetAsync())
        ///     {
        ///          Console.WriteLine(storedProcedure.Id); 
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract CosmosFeedIterator<CosmosStoredProcedureSettings> GetStoredProcedureIterator(
            int? maxItemCount = null,
            string continuationToken = null);

        /// <summary>
        /// Reads a <see cref="CosmosStoredProcedureSettings"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="id">The identifier of the Stored Procedure to read.</param>
        /// <param name="requestOptions">(Optional) The options for the stored procedure request <see cref="CosmosStoredProcedureRequestOptions"/></param>
        /// <param name="cancellation">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="CosmosStoredProcedureSettings"/>.
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
        ///  <example>
        ///  This reads an existing stored procedure.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosScripts scripts = this.container.GetScripts();
        /// CosmosStoredProcedure storedProcedure = await scripts.ReadStoredProcedureAsync("ExistingId");
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<CosmosStoredProcedureResponse> ReadStoredProcedureAsync(
            string id,
            CosmosRequestOptions requestOptions = null,
            CancellationToken cancellation = default(CancellationToken));

        /// <summary>
        /// Replaces a <see cref="CosmosStoredProcedureSettings"/> in the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="id">The identifier of the Stored Procedure to replace.</param>
        /// <param name="body">The JavaScript function to replace the existing resource with.</param>
        /// <param name="requestOptions">(Optional) The options for the stored procedure request <see cref="CosmosStoredProcedureRequestOptions"/></param>
        /// <param name="cancellation">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="CosmosStoredProcedureSettings"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="id"/>, <paramref name="body"/> are not set.</exception>
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
        /// CosmosScripts scripts = this.container.GetScripts();
        /// CosmosResponseMessage response = await scripts.ReplaceStoredProcedureAsync("testTriggerId", body);
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<CosmosStoredProcedureResponse> ReplaceStoredProcedureAsync(
            string id,
            string body,
            CosmosRequestOptions requestOptions = null,
            CancellationToken cancellation = default(CancellationToken));

        /// <summary>
        /// Delete a <see cref="CosmosStoredProcedureSettings"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="id">The identifier of the Stored Procedure to delete.</param>
        /// <param name="requestOptions">(Optional) The options for the stored procedure request <see cref="CosmosStoredProcedureRequestOptions"/></param>
        /// <param name="cancellation">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="CosmosResponseMessage"/> which will contain the response to the request issued.</returns>
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
        /// CosmosScripts scripts = this.container.GetScripts();
        /// CosmosResponseMessage response = await scripts.DeleteStoredProcedureAsync("taxUdfId");
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<CosmosStoredProcedureResponse> DeleteStoredProcedureAsync(
            string id,
            CosmosRequestOptions requestOptions = null,
            CancellationToken cancellation = default(CancellationToken));

        /// <summary>
        /// Executes a stored procedure against a container as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <typeparam name="TInput">The input type that is JSON serializable.</typeparam>
        /// <typeparam name="TOutput">The return type that is JSON serializable.</typeparam>
        /// <param name="partitionKey">The partition key for the item. <see cref="Microsoft.Azure.Documents.PartitionKey"/></param>
        /// <param name="id">The identifier of the Stored Procedure to execute.</param>
        /// <param name="input">The JSON serializable input parameters.</param>
        /// <param name="requestOptions">(Optional) The options for the stored procedure request <see cref="CosmosStoredProcedureRequestOptions"/></param>
        /// <param name="cancellation">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The task object representing the service response for the asynchronous operation which would contain any response set in the stored procedure.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="id"/> or <paramref name="partitionKey"/>  are not set.</exception>
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
        /// CosmosScripts scripts = this.container.GetScripts();
        /// CosmosStoredProcedure cosmosStoredProcedure = await scripts.CreateStoredProcedureAsync(
        ///         id: "appendString",
        ///         body: sprocBody);
        /// 
        /// // Execute the stored procedure
        /// CosmosItemResponse<string> sprocResponse = await scripts.ExecuteAsync<string, string>(testPartitionId, "Item as a string: ");
        /// Console.WriteLine("sprocResponse.Resource");
        /// /// ]]>
        /// </code>
        /// </example>
        public abstract Task<CosmosStoredProcedureExecuteResponse<TOutput>> ExecuteStoredProcedureAsync<TInput, TOutput>(
            object partitionKey,
            string id,
            TInput input,
            CosmosStoredProcedureRequestOptions requestOptions = null,
            CancellationToken cancellation = default(CancellationToken));

        /// <summary>
        /// Creates a trigger as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="triggerSettings">The <see cref="CosmosTriggerSettings"/> object.</param>
        /// <param name="requestOptions">(Optional) The options for the stored procedure request <see cref="CosmosRequestOptions"/></param>
        /// <param name="cancellation">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A task object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="triggerSettings"/> is not set.</exception>
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
        ///         <term>409</term><description>Conflict - This means a <see cref="CosmosTriggerSettings"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        ///     <item>
        ///         <term>413</term><description>RequestEntityTooLarge - This means the body of the <see cref="CosmosTriggerSettings"/> you tried to create was too large.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///  This creates a trigger then uses the trigger in a create item.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosScripts scripts = this.container.GetScripts();
        /// CosmosTrigger cosmosTrigger = await scripts.CreateTriggerAsync(
        ///     new CosmosTriggerSettings
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
        /// CosmosItemRequestOptions options = new CosmosItemRequestOptions()
        /// {
        ///     PreTriggers = new List<string>() { cosmosTrigger.Id },
        /// };
        ///
        /// // Create a new item with trigger set in the request options
        /// CosmosItemResponse<dynamic> createdItem = await this.container.Items.CreateItemAsync<dynamic>(item.status, item, options);
        /// double itemTax = createdItem.Resource.tax;
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<CosmosTriggerResponse> CreateTriggerAsync(
            CosmosTriggerSettings triggerSettings,
            CosmosRequestOptions requestOptions = null,
            CancellationToken cancellation = default(CancellationToken));

        /// <summary>
        /// Gets an iterator to go through all the triggers for the container
        /// </summary>
        /// <param name="maxItemCount">(Optional) The max item count to return as part of the query</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <example>
        /// Get an iterator for all the triggers under the cosmos container
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosScripts scripts = this.container.GetScripts();
        /// CosmosResultSetIterator<CosmosTriggerSettings> setIterator = scripts.Triggers.GetTriggerIterator();
        /// while (setIterator.HasMoreResults)
        /// {
        ///     foreach(CosmosTriggerSettings settings in await setIterator.FetchNextSetAsync())
        ///     {
        ///          Console.WriteLine(settings.Id); 
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract CosmosFeedIterator<CosmosTriggerSettings> GetTriggerIterator(
            int? maxItemCount = null,
            string continuationToken = null);

        /// <summary>
        /// Reads a <see cref="CosmosTriggerSettings"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="id">The id of the trigger to read.</param>
        /// <param name="requestOptions">(Optional) The options for the trigger request <see cref="CosmosRequestOptions"/></param>
        /// <param name="cancellation">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="CosmosTriggerResponse"/> which wraps a <see cref="CosmosTriggerSettings"/> containing the read resource record.
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
        /// CosmosScripts scripts = this.container.GetScripts();
        /// CosmosTriggerResponse response = await scripts.ReadTriggerAsync("ExistingId");
        /// CosmosTriggerSettings settings = response;
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<CosmosTriggerResponse> ReadTriggerAsync(
            string id,
            CosmosRequestOptions requestOptions = null,
            CancellationToken cancellation = default(CancellationToken));

        /// <summary>
        /// Replaces a <see cref="CosmosTriggerSettings"/> in the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="triggerSettings">The <see cref="CosmosTriggerSettings"/> object.</param>
        /// <param name="requestOptions">(Optional) The options for the trigger request <see cref="CosmosRequestOptions"/></param>
        /// <param name="cancellation">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="CosmosTriggerResponse"/> which wraps a <see cref="CosmosTriggerSettings"/> containing the updated resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="triggerSettings"/> is not set.</exception>
        /// <example>
        /// This examples replaces an existing trigger.
        /// <code language="c#">
        /// <![CDATA[
        /// //Updated settings
        /// CosmosTriggerSettings settings = new CosmosTriggerSettings
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
        /// CosmosScripts scripts = this.container.GetScripts();
        /// CosmosTriggerResponse response = await scripts.ReplaceTriggerAsync(CosmosTriggerSettings);
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<CosmosTriggerResponse> ReplaceTriggerAsync(
                    CosmosTriggerSettings triggerSettings,
                    CosmosRequestOptions requestOptions = null,
                    CancellationToken cancellation = default(CancellationToken));

        /// <summary>
        /// Delete a <see cref="CosmosTriggerSettings"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="id">The id of the trigger to delete.</param>
        /// <param name="requestOptions">(Optional) The options for the trigger request <see cref="CosmosRequestOptions"/></param>
        /// <param name="cancellation">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="CosmosTriggerResponse"/> which wraps a <see cref="CosmosTriggerSettings"/> which will contain information about the request issued.</returns>
        /// /// <example>
        /// This examples gets a reference to an existing trigger and deletes it.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosScripts scripts = this.container.GetScripts();
        /// CosmosTriggerResponse response = await scripts.DeleteTriggerAsync("existingId");
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<CosmosTriggerResponse> DeleteTriggerAsync(
            string id,
            CosmosRequestOptions requestOptions = null,
            CancellationToken cancellation = default(CancellationToken));

        /// <summary>
        /// Creates a user defined function as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="userDefinedFunctionSettings">The <see cref="CosmosUserDefinedFunctionSettings"/> object.</param>
        /// <param name="requestOptions">(Optional) The options for the user defined function request <see cref="CosmosRequestOptions"/></param>
        /// <param name="cancellation">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
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
        /// CosmosScripts scripts = this.container.GetScripts();
        /// await scripts.UserDefinedFunctions.CreateUserDefinedFunctionAsync(
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
        public abstract Task<CosmosUserDefinedFunctionResponse> CreateUserDefinedFunctionAsync(
            CosmosUserDefinedFunctionSettings userDefinedFunctionSettings,
            CosmosRequestOptions requestOptions = null,
            CancellationToken cancellation = default(CancellationToken));

        /// <summary>
        /// Gets an iterator to go through all the user defined functions for the container
        /// </summary>
        /// <param name="maxItemCount">(Optional) The max item count to return as part of the query</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <example>
        /// Get an iterator for all the triggers under the cosmos container
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosScripts scripts = this.container.GetScripts();
        /// CosmosResultSetIterator<CosmosUserDefinedFunctionSettings> setIterator = scripts.GetUserDefinedFunctionIterator();
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
        public abstract CosmosFeedIterator<CosmosUserDefinedFunctionSettings> GetUserDefinedFunctionIterator(
            int? maxItemCount = null,
            string continuationToken = null);

        /// <summary>
        /// Reads a <see cref="CosmosUserDefinedFunctionSettings"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="id">The id of the user defined function to read</param>
        /// <param name="requestOptions">(Optional) The options for the user defined function request <see cref="CosmosRequestOptions"/></param>
        /// <param name="cancellation">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="CosmosUserDefinedFunctionResponse"/> which wraps a <see cref="CosmosUserDefinedFunctionSettings"/> containing the read resource record.
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
        /// CosmosScripts scripts = this.container.GetScripts();
        /// CosmosUserDefinedFunctionResponse response = await scripts.ReadUserDefinedFunctionAsync("ExistingId");
        /// CosmosUserDefinedFunctionSettings settings = response;
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<CosmosUserDefinedFunctionResponse> ReadUserDefinedFunctionAsync(
            string id,
            CosmosRequestOptions requestOptions = null,
            CancellationToken cancellation = default(CancellationToken));

        /// <summary>
        /// Replaces a <see cref="CosmosUserDefinedFunctionSettings"/> in the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="userDefinedFunctionSettings">The <see cref="CosmosUserDefinedFunctionSettings"/> object.</param>
        /// <param name="requestOptions">(Optional) The options for the user defined function request <see cref="CosmosRequestOptions"/></param>
        /// <param name="cancellation">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="CosmosUserDefinedFunctionResponse"/> which wraps a <see cref="CosmosUserDefinedFunctionSettings"/> containing the updated resource record.
        /// </returns>
        /// <example>
        /// This examples replaces an existing user defined function.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosScripts scripts = this.container.GetScripts();
        /// //Updated settings
        /// CosmosUserDefinedFunctionSettings settings = new CosmosUserDefinedFunctionSettings
        /// {
        ///     Id = "testUserDefinedFunId",
        ///     Body = "function(amt) { return amt * 0.15; }",
        /// };
        /// 
        /// CosmosUserDefinedFunctionResponse response = await scripts.ReplaceUserDefinedFunctionAsync(settings);
        /// CosmosUserDefinedFunctionSettings settings = response;
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<CosmosUserDefinedFunctionResponse> ReplaceUserDefinedFunctionAsync(
            CosmosUserDefinedFunctionSettings userDefinedFunctionSettings,
            CosmosRequestOptions requestOptions = null,
            CancellationToken cancellation = default(CancellationToken));

        /// <summary>
        /// Delete a <see cref="CosmosUserDefinedFunctionSettings"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="id">The id of the user defined function to delete.</param>
        /// <param name="requestOptions">(Optional) The options for the user defined function request <see cref="CosmosRequestOptions"/></param>
        /// <param name="cancellation">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="CosmosUserDefinedFunctionResponse"/> which wraps a <see cref="CosmosUserDefinedFunctionSettings"/> which will contain information about the request issued.</returns>
        /// <example>
        /// This examples gets a reference to an existing user defined function and deletes it.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosScripts scripts = this.container.GetScripts();
        /// CosmosUserDefinedFunctionResponse response = await this.cosmosContainer.DeleteUserDefinedFunctionAsync("existingId");
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<CosmosUserDefinedFunctionResponse> DeleteUserDefinedFunctionAsync(
            string id,
            CosmosRequestOptions requestOptions = null,
            CancellationToken cancellation = default(CancellationToken));
    }
}
