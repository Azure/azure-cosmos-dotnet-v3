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
        /// Scripts scripts = this.container.GetScripts();
        /// StoredProcedureProperties storedProcedure = new StoredProcedureProperties(id, sprocBody);
        /// CosmosStoredProcedure cosmosStoredProcedure = await scripts.CreateStoredProcedureAsync(storedProcedure);
        /// 
        /// // Execute the stored procedure
        /// CosmosItemResponse<string> sprocResponse = await scripts.ExecuteStoredProcedureAsync<string, string>(testPartitionId, "appendString", "Item as a string: ");
        /// Console.WriteLine("sprocResponse.Resource");
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<StoredProcedureResponse> CreateStoredProcedureAsync(
                    StoredProcedureProperties storedProcedureProperties,
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Gets an iterator to go through all the stored procedures for the container
        /// </summary>
        /// <param name="maxItemCount">(Optional) The max item count to return as part of the query</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <returns>An iterator to read through the existing stored procedures.</returns>
        /// <example>
        /// Get an iterator for all the stored procedures under the cosmos container
        /// <code language="c#">
        /// <![CDATA[
        /// Scripts scripts = this.container.GetScripts();
        /// FeedIterator<StoredProcedureProperties> setIterator = scripts.GetStoredProcedureIterator();
        /// while (setIterator.HasMoreResults)
        /// {
        ///     foreach(StoredProcedureProperties storedProcedure in await setIterator.FetchNextSetAsync())
        ///     {
        ///          Console.WriteLine(storedProcedure.Id); 
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract FeedIterator<StoredProcedureProperties> GetStoredProceduresIterator(
            int? maxItemCount = null,
            string continuationToken = null);

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
        /// Scripts scripts = this.container.GetScripts();
        /// CosmosStoredProcedure storedProcedure = await scripts.ReadStoredProcedureAsync("ExistingId");
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<StoredProcedureResponse> ReadStoredProcedureAsync(
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
        /// Scripts scripts = this.container.GetScripts();
        /// CosmosResponseMessage response = await scripts.ReplaceStoredProcedureAsync("testTriggerId", body);
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<StoredProcedureResponse> ReplaceStoredProcedureAsync(
            StoredProcedureProperties storedProcedureProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Delete a <see cref="StoredProcedureProperties"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="id">The identifier of the Stored Procedure to delete.</param>
        /// <param name="requestOptions">(Optional) The options for the stored procedure request <see cref="StoredProcedureRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
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
        /// Scripts scripts = this.container.GetScripts();
        /// CosmosResponseMessage response = await scripts.DeleteStoredProcedureAsync("taxUdfId");
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<StoredProcedureResponse> DeleteStoredProcedureAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Executes a stored procedure against a container as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <typeparam name="TInput">The input type that is JSON serializable.</typeparam>
        /// <typeparam name="TOutput">The return type that is JSON serializable.</typeparam>
        /// <param name="partitionKey">The partition key for the item. <see cref="Microsoft.Azure.Documents.PartitionKey"/></param>
        /// <param name="storedProcedureId">The identifier of the Stored Procedure to execute.</param>
        /// <param name="input">The JSON serializable input parameters.</param>
        /// <param name="requestOptions">(Optional) The options for the stored procedure request <see cref="StoredProcedureRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The task object representing the service response for the asynchronous operation which would contain any response set in the stored procedure.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="storedProcedureId"/> or <paramref name="partitionKey"/>  are not set.</exception>
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
        /// Scripts scripts = this.container.GetScripts();
        /// CosmosStoredProcedure cosmosStoredProcedure = await scripts.CreateStoredProcedureAsync(
        ///         id: "appendString",
        ///         body: sprocBody);
        /// 
        /// // Execute the stored procedure
        /// StoredProcedureExecuteResponse<string> sprocResponse = await scripts.ExecuteStoredProcedureAsync<string, string>(testPartitionId, "Item as a string: ");
        /// Console.WriteLine(sprocResponse.Resource);
        /// /// ]]>
        /// </code>
        /// </example>
        public abstract Task<StoredProcedureExecuteResponse<TOutput>> ExecuteStoredProcedureAsync<TInput, TOutput>(
            PartitionKey partitionKey,
            string storedProcedureId,
            TInput input,
            StoredProcedureRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Executes a stored procedure against a container as an asynchronous operation in the Azure Cosmos service and obtains a Stream as response.
        /// </summary>
        /// <param name="partitionKey">The partition key for the item. <see cref="Microsoft.Azure.Documents.PartitionKey"/></param>
        /// <param name="storedProcedureId">The identifier of the Stored Procedure to execute.</param>
        /// <param name="streamPayload">The stream representing the input for the stored procedure.</param>
        /// <param name="requestOptions">(Optional) The options for the stored procedure request <see cref="StoredProcedureRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The task object representing the service response for the asynchronous operation which would contain any response set in the stored procedure.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="storedProcedureId"/> or <paramref name="partitionKey"/>  are not set.</exception>
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
        /// Scripts scripts = this.container.GetScripts();
        /// CosmosStoredProcedure cosmosStoredProcedure = await scripts.CreateStoredProcedureAsync(
        ///         id: "appendString",
        ///         body: sprocBody);
        /// 
        /// // Execute the stored procedure
        /// CosmosResponseMessage sprocResponse = await scripts.ExecuteStoredProcedureStreamAsync(testPartitionId, streamPayload: stream);
        /// using (StreamReader sr = new StreamReader(sprocResponse.Content))
        /// {
        ///     string stringResponse = await sr.ReadToEndAsync();
        ///     Console.WriteLine(stringResponse);
        ///  }
        /// 
        /// /// ]]>
        /// </code>
        /// </example>
        public abstract Task<CosmosResponseMessage> ExecuteStoredProcedureStreamAsync(
            PartitionKey partitionKey,
            string storedProcedureId,
            Stream streamPayload,
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
        /// Scripts scripts = this.container.GetScripts();
        /// CosmosTrigger cosmosTrigger = await scripts.CreateTriggerAsync(
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
        ///     PreTriggers = new List<string>() { cosmosTrigger.Id },
        /// };
        ///
        /// // Create a new item with trigger set in the request options
        /// CosmosItemResponse<dynamic> createdItem = await this.container.Items.CreateItemAsync<dynamic>(item.status, item, options);
        /// double itemTax = createdItem.Resource.tax;
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<TriggerResponse> CreateTriggerAsync(
            TriggerProperties triggerProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Gets an iterator to go through all the triggers for the container
        /// </summary>
        /// <returns>An iterator to read through the existing triggers.</returns>
        /// <param name="maxItemCount">(Optional) The max item count to return as part of the query</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <example>
        /// Get an iterator for all the triggers under the cosmos container
        /// <code language="c#">
        /// <![CDATA[
        /// Scripts scripts = this.container.GetScripts();
        /// FeedIterator<TriggerProperties> setIterator = scripts.Triggers.GetTriggerIterator();
        /// while (setIterator.HasMoreResults)
        /// {
        ///     foreach(TriggerProperties triggerProperties in await setIterator.FetchNextSetAsync())
        ///     {
        ///          Console.WriteLine(triggerProperties.Id); 
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract FeedIterator<TriggerProperties> GetTriggersIterator(
            int? maxItemCount = null,
            string continuationToken = null);

        /// <summary>
        /// Reads a <see cref="TriggerProperties"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="id">The id of the trigger to read.</param>
        /// <param name="requestOptions">(Optional) The options for the trigger request <see cref="RequestOptions"/></param>
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
        /// Scripts scripts = this.container.GetScripts();
        /// TriggerResponse response = await scripts.ReadTriggerAsync("ExistingId");
        /// TriggerProperties triggerProperties = response;
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<TriggerResponse> ReadTriggerAsync(
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
        /// Scripts scripts = this.container.GetScripts();
        /// TriggerResponse response = await scripts.ReplaceTriggerAsync(triggerSettigs);
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<TriggerResponse> ReplaceTriggerAsync(
                    TriggerProperties triggerProperties,
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Delete a <see cref="TriggerProperties"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="id">The id of the trigger to delete.</param>
        /// <param name="requestOptions">(Optional) The options for the trigger request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="TriggerResponse"/> which wraps a <see cref="TriggerProperties"/> which will contain information about the request issued.</returns>
        /// /// <example>
        /// This examples gets a reference to an existing trigger and deletes it.
        /// <code language="c#">
        /// <![CDATA[
        /// Scripts scripts = this.container.GetScripts();
        /// TriggerResponse response = await scripts.DeleteTriggerAsync("existingId");
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<TriggerResponse> DeleteTriggerAsync(
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
        /// Scripts scripts = this.container.GetScripts();
        /// await scripts.UserDefinedFunctions.CreateUserDefinedFunctionAsync(
        ///     new UserDefinedFunctionProperties 
        ///     { 
        ///         Id = "calculateTax", 
        ///         Body = @"function(amt) { return amt * 0.05; }" 
        ///     });
        ///
        /// QueryDefinition sqlQuery = new QueryDefinition(
        ///     "SELECT VALUE udf.calculateTax(t.cost) FROM toDoActivity t where t.cost > @expensive and t.status = @status")
        ///     .UseParameter("@expensive", 9000)
        ///     .UseParameter("@status", "Done");
        ///
        /// FeedIterator<double> setIterator = this.container.Items.GetItemsQueryIterator<double>(
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
        public abstract Task<UserDefinedFunctionResponse> CreateUserDefinedFunctionAsync(
            UserDefinedFunctionProperties userDefinedFunctionProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Gets an iterator to go through all the user defined functions for the container
        /// </summary>
        /// <param name="maxItemCount">(Optional) The max item count to return as part of the query</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <returns>An iterator to read through the existing user defined functions.</returns>
        /// <example>
        /// Get an iterator for all the triggers under the cosmos container
        /// <code language="c#">
        /// <![CDATA[
        /// Scripts scripts = this.container.GetScripts();
        /// FeedIterator<UserDefinedFunctionProperties> setIterator = scripts.GetUserDefinedFunctionIterator();
        /// while (setIterator.HasMoreResults)
        /// {
        ///     foreach(UserDefinedFunctionProperties usdfProperties in await setIterator.FetchNextSetAsync())
        ///     {
        ///          Console.WriteLine(udfProperties.Id); 
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract FeedIterator<UserDefinedFunctionProperties> GetUserDefinedFunctionsIterator(
            int? maxItemCount = null,
            string continuationToken = null);

        /// <summary>
        /// Reads a <see cref="UserDefinedFunctionProperties"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="id">The id of the user defined function to read</param>
        /// <param name="requestOptions">(Optional) The options for the user defined function request <see cref="RequestOptions"/></param>
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
        /// Scripts scripts = this.container.GetScripts();
        /// UserDefinedFunctionResponse response = await scripts.ReadUserDefinedFunctionAsync("ExistingId");
        /// UserDefinedFunctionProperties udfProperties = response;
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<UserDefinedFunctionResponse> ReadUserDefinedFunctionAsync(
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
        /// A <see cref="Task"/> containing a <see cref="UserDefinedFunctionResponse"/> which wraps a <see cref="UserDefinedFunctionProperties"/> containing the updated resource record.
        /// </returns>
        /// <example>
        /// This examples replaces an existing user defined function.
        /// <code language="c#">
        /// <![CDATA[
        /// Scripts scripts = this.container.GetScripts();
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
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Delete a <see cref="UserDefinedFunctionProperties"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="id">The id of the user defined function to delete.</param>
        /// <param name="requestOptions">(Optional) The options for the user defined function request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="UserDefinedFunctionResponse"/> which wraps a <see cref="UserDefinedFunctionProperties"/> which will contain information about the request issued.</returns>
        /// <example>
        /// This examples gets a reference to an existing user defined function and deletes it.
        /// <code language="c#">
        /// <![CDATA[
        /// Scripts scripts = this.container.GetScripts();
        /// UserDefinedFunctionResponse response = await this.container.DeleteUserDefinedFunctionAsync("existingId");
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<UserDefinedFunctionResponse> DeleteUserDefinedFunctionAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));
    }
}
