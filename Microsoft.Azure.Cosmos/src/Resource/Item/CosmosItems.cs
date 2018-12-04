//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Linq;

    /// <summary>
    /// Used to perform operations on items. There are two different types of operations.
    /// 1. The object operations where it serializes and deserializes the item on request/response
    /// 2. The stream response which takes a Stream containing a JSON serialized object and returns a response containing a Stream
    /// </summary>
    public class CosmosItems
    {
        /// <summary>
        /// Cache the full URI segment without the last resource id.
        /// This allows only a single con-cat operation instead of building the full URI string each time.
        /// </summary>
        private string cachedUriSegmentWithoutId { get; }
        private CosmosJsonSerializer cosmosJsonSerializer { get; }
        private CosmosClient client { get; }

        /// <summary>
        /// Create a <see cref="CosmosItems"/>
        /// </summary>
        /// <param name="container">The cosmos container</param>
        protected internal CosmosItems(CosmosContainer container)
        {
            this.client = container.Client;
            this.container = container;
            this.cosmosJsonSerializer = this.container.Client.CosmosJsonSerializer;
            this.cachedUriSegmentWithoutId = GetResourceSegmentUriWithoutId();
        }

        internal readonly CosmosContainer container;

        /// <summary>
        /// Creates a Item as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <param name="partitionKey">The partition key for the item. <see cref="PartitionKey"/></param>
        /// <param name="streamPayload">A <see cref="Stream"/> containing the payload.</param>
        /// <param name="requestOptions">(Optional) The options for the item request <see cref="CosmosItemRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The <see cref="CosmosResponseMessage"/> that was created contained within a <see cref="System.Threading.Tasks.Task"/> object representing the service response for the asynchronous operation.</returns>
        /// <exception>The Stream operation only throws on client side exceptions. This is to increase performance and prevent the overhead of throwing exceptions. Check the HTTP status code on the response to check if the operation failed.
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the document supplied.</description>
        ///     </item>
        ///     <item>
        ///         <term>403</term><description>Forbidden - This likely means the collection in to which you were trying to create the document is full.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a item with an id matching the id field in the streamPayload already existed</description>
        ///     </item>
        ///     <item>
        ///         <term>413</term><description>RequestEntityTooLarge - This means the item exceeds the current max entity size. Consult documentation for limits and quotas.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// This example creates an item in a Cosmos container.
        /// <code language="c#">
        /// <![CDATA[
        /// //Create the object in Cosmos
        /// using (CosmosResponseMessage response = await this.Container.Items.CreateItemStreamAsync(partitionKey: "streamPartitionKey", streamPayload: stream))
        /// {
        ///     if (!response.IsSuccessStatusCode)
        ///     {
        ///         //Handle and log exception
        ///         return;
        ///     }
        ///     
        ///     using (Stream responseStream = await response.ReadBodyAsync())
        ///     {
        ///         //Read or do other operations with the stream
        ///         using (StreamReader streamReader = new StreamReader(responseStream))
        ///         {
        ///             string responseContentAsString = await streamReader.ReadToEndAsync();
        ///         }
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public virtual Task<CosmosResponseMessage> CreateItemStreamAsync(
                    object partitionKey,
                    Stream streamPayload,
                    CosmosItemRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            return ProcessItemStreamAsync(
                partitionKey,
                null,
                streamPayload,
                OperationType.Create,
                requestOptions,
                cancellationToken);
        }

        /// <summary>
        /// Creates a item as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <param name="partitionKey">The partition key for the item. <see cref="PartitionKey"/></param>
        /// <param name="item">A JSON serializable object that must contain an id property. <see cref="CosmosJsonSerializer"/> to implement a custom serializer</param>
        /// <param name="requestOptions">(Optional) The options for the item request <see cref="CosmosItemRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The <see cref="CosmosItemResponse{T}"/> that was created contained within a <see cref="System.Threading.Tasks.Task"/> object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occurred during async processing. Look within InnerExceptions to find the actual exception(s)</exception>
        /// <exception cref="CosmosException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the document supplied. </description>
        ///     </item>
        ///     <item>
        ///         <term>403</term><description>Forbidden - This likely means the collection in to which you were trying to create the document is full.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a item with an id matching the id field of <paramref name="item"/> already existed</description>
        ///     </item>
        ///     <item>
        ///         <term>413</term><description>RequestEntityTooLarge - This means the item exceeds the current max entity size. Consult documentation for limits and quotas.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// public class ToDoActivity{
        ///     public string id {get; set;}
        ///     public string status {get; set;}
        /// }
        /// 
        /// ToDoActivity test = new ToDoActivity()
        /// {
        ///    id = Guid.NewGuid().ToString(),
        ///    status = "InProgress"
        /// };
        ///
        /// CosmosItemResponse item = this.cosmosContainer.Items.CreateItemAsync<ToDoActivity>(test.status, tests);
        /// ]]>
        /// </code>
        /// </example>
        public virtual Task<CosmosItemResponse<T>> CreateItemAsync<T>(
            object partitionKey,
            T item,
            CosmosItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return ProcessItemAsync<T>(
                partitionKey,
                null,
                item,
                OperationType.Create,
                requestOptions,
                cancellationToken);
        }

        /// <summary>
        /// Reads a item from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="partitionKey">The partition key for the item. <see cref="PartitionKey"/></param>
        /// <param name="id">The cosmos item id</param>
        /// <param name="requestOptions">(Optional) The options for the item request <see cref="CosmosItemRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="CosmosResponseMessage"/> which wraps a <see cref="Stream"/> containing the read resource record.
        /// </returns>
        /// <exception>The Stream operation only throws on client side exceptions. This is to increase performance and prevent the overhead of throwing exceptions. Check the HTTP status code on the response to check if the operation failed.
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to read did not exist.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// Read a response as a stream.
        /// <code language="c#">
        /// <![CDATA[
        /// using(CosmosResponseMessage response = this.cosmosContainer.Items.ReadItemStreamAsync("partitionKey", "id"))
        /// {
        ///     if (!response.IsSuccessStatusCode)
        ///     {
        ///         //Handle and log exception
        ///         return;
        ///     }
        ///     
        ///     using(Stream stream = response.ReadBodyAsync())
        ///     {
        ///         //Read or do other operations with the stream
        ///         using (StreamReader streamReader = new StreamReader(stream))
        ///         {
        ///             string content =  streamReader.ReadToEndAsync();
        ///         }
        ///     }
        /// }
        /// 
        /// ]]>
        /// </code>
        /// </example>
        public virtual Task<CosmosResponseMessage> ReadItemStreamAsync(
                    object partitionKey,
                    string id,
                    CosmosItemRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            return ProcessItemStreamAsync(
                partitionKey,
                id,
                null,
                OperationType.Read,
                requestOptions,
                cancellationToken);
        }

        /// <summary>
        /// Reads a item from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="partitionKey">The partition key for the item. <see cref="PartitionKey"/></param>
        /// <param name="id">The cosmos item id</param>
        /// <param name="requestOptions">(Optional) The options for the item request <see cref="CosmosItemRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="CosmosItemResponse{T}"/> which wraps the read resource record.
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
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// public class ToDoActivity{
        ///     public string id {get; set;}
        ///     public string status {get; set;}
        /// }
        /// 
        /// ToDoActivity toDoActivity = this.cosmosContainer.Items.ReadItemAsync<ToDoActivity>("partitionKey", "id");
        /// 
        /// ]]>
        /// </code>
        /// </example>
        public virtual Task<CosmosItemResponse<T>> ReadItemAsync<T>(
            object partitionKey,
            string id,
            CosmosItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return ProcessItemAsync<T>(
                partitionKey,
                id,
                OperationType.Read,
                requestOptions,
                cancellationToken);
        }

        /// <summary>
        /// Upserts an item stream as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <param name="partitionKey">The partition key for the item. <see cref="PartitionKey"/></param>
        /// <param name="streamPayload">A <see cref="Stream"/> containing the payload.</param>
        /// <param name="requestOptions">(Optional) The options for the item request <see cref="CosmosItemRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="CosmosResponseMessage"/> which wraps a <see cref="Stream"/> containing the read resource record.
        /// </returns>
        /// <exception>The Stream operation only throws on client side exceptions. This is to increase performance and prevent the overhead of throwing exceptions. Check the HTTP status code on the response to check if the operation failed.
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to read did not exist.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// Upsert a Stream containing the item to Cosmos
        /// <code language="c#">
        /// <![CDATA[
        /// using(CosmosResponseMessage response = this.cosmosContainer.Items.UpsertItemStreamAsync(partitionKey: "itemPartitionKey", streamPayload: stream))
        /// {
        ///     if (!response.IsSuccessStatusCode)
        ///     {
        ///         //Handle and log exception
        ///         return;
        ///     }
        ///     
        ///     using(Stream stream = response.ReadBodyAsync())
        ///     {
        ///         //Read or do other operations with the stream
        ///         using (StreamReader  streamReader = new StreamReader(stream))
        ///         {
        ///             string content =  streamReader.ReadToEndAsync();
        ///         }
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public virtual Task<CosmosResponseMessage> UpsertItemStreamAsync(
                    object partitionKey,
                    Stream streamPayload,
                    CosmosItemRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            return ProcessItemStreamAsync(
                partitionKey,
                null,
                streamPayload,
                OperationType.Upsert,
                requestOptions,
                cancellationToken);
        }

        /// <summary>
        /// Upserts an item as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <param name="partitionKey">The partition key for the item. <see cref="PartitionKey"/></param>
        /// <param name="item">A JSON serializable object that must contain an id property. <see cref="CosmosJsonSerializer"/> to implement a custom serializer</param>
        /// <param name="requestOptions">(Optional) The options for the item request <see cref="CosmosItemRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The <see cref="CosmosItemResponse{T}"/> that was upserted contained within a <see cref="System.Threading.Tasks.Task"/> object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occurred during async processing. Look within InnerExceptions to find the actual exception(s)</exception>
        /// <exception cref="CosmosException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the document supplied.</description>
        ///     </item>
        ///     <item>
        ///         <term>403</term><description>Forbidden - This likely means the collection in to which you were trying to upsert the document is full.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a item with an id matching the id field of <paramref name="item"/> already existed</description>
        ///     </item>
        ///     <item>
        ///         <term>413</term><description>RequestEntityTooLarge - This means the item exceeds the current max entity size. Consult documentation for limits and quotas.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// public class ToDoActivity{
        ///     public string id {get; set;}
        ///     public string status {get; set;}
        /// }
        /// 
        /// ToDoActivity test = new ToDoActivity()
        /// {
        ///    id = Guid.NewGuid().ToString(),
        ///    status = "InProgress"
        /// };
        ///
        /// CosmosItemResponse<ToDoActivity> item = await this.cosmosContainer.Items.UpsertAsync<ToDoActivity>(test.status, test);
        /// ]]>
        /// </code>
        /// </example>
        public virtual Task<CosmosItemResponse<T>> UpsertItemAsync<T>(
            object partitionKey,
            T item,
            CosmosItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return ProcessItemAsync<T>(
                partitionKey,
                null,
                item,
                OperationType.Upsert,
                requestOptions,
                cancellationToken);
        }

        /// <summary>
        /// Replaces a item in the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="partitionKey">The partition key for the item. <see cref="PartitionKey"/></param>
        /// <param name="id">The cosmos item id</param>
        /// <param name="streamPayload">A <see cref="Stream"/> containing the payload.</param>
        /// <param name="requestOptions">(Optional) The options for the item request <see cref="CosmosItemRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="CosmosResponseMessage"/> which wraps a <see cref="Stream"/> containing the replace resource record.
        /// </returns>
        /// <exception>The Stream operation only throws on client side exceptions. This is to increase performance and prevent the overhead of throwing exceptions. Check the HTTP status code on the response to check if the operation failed.
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
        /// Replace an item in Cosmos
        /// <code language="c#">
        /// <![CDATA[
        /// using(CosmosResponseMessage response = this.cosmosContainer.Items.ReplaceItemStreamAsync(partitionKey: "itemPartitionKey", id: "itemId", streamPayload: stream))
        /// {
        ///     if (!response.IsSuccessStatusCode)
        ///     {
        ///         //Handle and log exception
        ///         return;
        ///     }
        ///     
        ///     using(Stream stream = response.ReadBodyAsync())
        ///     {
        ///         //Read or do other operations with the stream
        ///         using (StreamReader streamReader = new StreamReader(stream))
        ///         {
        ///             string content =  streamReader.ReadToEndAsync();
        ///         }
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public virtual Task<CosmosResponseMessage> ReplaceItemStreamAsync(
                    object partitionKey,
                    string id,
                    Stream streamPayload,
                    CosmosItemRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            return ProcessItemStreamAsync(
                partitionKey,
                id,
                streamPayload,
                OperationType.Replace,
                requestOptions,
                cancellationToken);
        }

        /// <summary>
        /// Replaces a item in the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="partitionKey">The partition key for the item. <see cref="PartitionKey"/></param>
        /// <param name="id">The cosmos item id</param>
        /// <param name="item">A JSON serializable object that must contain an id property. <see cref="CosmosJsonSerializer"/> to implement a custom serializer</param>
        /// <param name="requestOptions">(Optional) The options for the item request <see cref="CosmosItemRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="CosmosItemResponse{T}"/> which wraps the updated resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If either <paramref name="item"/> is not set.</exception>
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
        /// <code language="c#">
        /// <![CDATA[
        /// public class ToDoActivity{
        ///     public string id {get; set;}
        ///     public string status {get; set;}
        /// }
        /// 
        /// ToDoActivity test = new ToDoActivity()
        /// {
        ///    id = Guid.NewGuid().ToString(),
        ///    status = "InProgress"
        /// };
        ///
        /// CosmosItemResponse item = await this.cosmosContainer.Items.ReplaceItemAsync<ToDoActivity>(test.status, test.id, test);
        /// ]]>
        /// </code>
        /// </example>
        public virtual Task<CosmosItemResponse<T>> ReplaceItemAsync<T>(
            object partitionKey,
            string id,
            T item,
            CosmosItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return ProcessItemAsync<T>(
                partitionKey,
                id,
                item,
                OperationType.Replace,
                requestOptions,
                cancellationToken);
        }

        /// <summary>
        /// Delete a item from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="partitionKey">The partition key for the item. <see cref="PartitionKey"/></param>
        /// <param name="id">The cosmos item id</param>
        /// <param name="requestOptions">(Optional) The options for the item request <see cref="CosmosItemRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="CosmosResponseMessage"/> which wraps a <see cref="Stream"/> containing the delete resource record.
        /// </returns>
        /// <exception>The Stream operation only throws on client side exceptions. This is to increase performance and prevent the overhead of throwing exceptions. Check the HTTP status code on the response to check if the operation failed.
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
        /// Delete an item from Cosmos
        /// <code language="c#">
        /// <![CDATA[
        /// using(CosmosResponseMessage response = this.cosmosContainer.Items.DeleteItemStreamAsync(partitionKey: "itemPartitionKey", id: "itemId"))
        /// {
        ///     if (!response.IsSuccessStatusCode)
        ///     {
        ///         //Handle and log exception
        ///         return;
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public virtual Task<CosmosResponseMessage> DeleteItemStreamAsync(
                    object partitionKey,
                    string id,
                    CosmosItemRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            return ProcessItemStreamAsync(
                partitionKey,
                id,
                null,
                OperationType.Delete,
                requestOptions,
                cancellationToken);
        }

        /// <summary>
        /// Delete a item from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="partitionKey">The partition key for the item. <see cref="PartitionKey"/></param>
        /// <param name="id">The cosmos item id</param>
        /// <param name="requestOptions">(Optional) The options for the item request <see cref="CosmosItemRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="CosmosItemResponse{T}"/> which will contain information about the request issued.</returns>
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
        /// <code language="c#">
        /// <![CDATA[
        /// public class ToDoActivity{
        ///     public string id {get; set;}
        ///     public string status {get; set;}
        /// }
        /// 
        /// CosmosItemResponse item = await this.cosmosContainer.Items.DeleteItemAsync<ToDoActivity>("partitionKey", "id");
        /// ]]>
        /// </code>
        /// </example>
        public virtual Task<CosmosItemResponse<T>> DeleteItemAsync<T>(
            object partitionKey,
            string id,
            CosmosItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return ProcessItemAsync<T>(
                partitionKey,
                id,
                OperationType.Delete,
                requestOptions,
                cancellationToken);
        }

        /// <summary>
        /// Gets an iterator to go through all the items for the container
        /// </summary>
        /// <param name="maxItemCount">(Optional) The max item count to return as part of the query</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <example>
        /// Get an iterator for all the items under the cosmos container
        /// <code language="c#">
        /// <![CDATA[
        /// public class ToDoActivity{
        ///     public string id {get; set;}
        ///     public string status {get; set;}
        /// }
        /// 
        /// CosmosResultSetIterator<ToDoActivity> setIterator = this.cosmosContainer.Items.GetItemIterator<ToDoActivity>();
        /// while (setIterator.HasMoreResults)
        /// {
        ///     foreach(ToDoActivity item in await setIterator.FetchNextSetAsync())
        ///     {
        ///          Console.WriteLine(item.id); 
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public virtual CosmosResultSetIterator<T> GetItemIterator<T>(
            int? maxItemCount = null,
            string continuationToken = null)
        {
            return new CosmosDefaultResultSetIterator<T>(
                maxItemCount, 
                continuationToken, 
                null, 
                ItemFeedRequestExecutor<T>);
        }

        /// <summary>
        /// Gets an iterator to go through all the items for the container as the original CosmosResponseMessage
        /// </summary>
        /// <param name="maxItemCount">(Optional) The max item count to return as part of the query</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request <see cref="CosmosQueryRequestOptions"/></param>
        /// <example>
        /// Get an iterator for all the items under the cosmos container
        /// <code language="c#">
        /// <![CDATA[
        /// public class ToDoActivity{
        ///     public string id {get; set;}
        ///     public string status {get; set;}
        /// }
        /// 
        /// CosmosResultSetIterator setIterator = this.Container.Items.GetItemStreamIterator();
        /// while (setIterator.HasMoreResults)
        /// {
        ///     using (CosmosResponseMessage iterator = await setIterator.FetchNextSetAsync())
        ///     {
        ///         using (StreamReader sr = new StreamReader(iterator.Content))
        ///         {
        ///             string content = await sr.ReadToEndAsync();
        ///         }
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public virtual CosmosResultSetIterator GetItemStreamIterator(
            int? maxItemCount = null,
            string continuationToken = null,
            CosmosItemRequestOptions requestOptions = null)
        {
            return new CosmosDefaultResultSetStreamIterator(maxItemCount, continuationToken, requestOptions, ItemStreamFeedRequestExecutor);
        }

        /// <summary>
        ///  This method creates a query for items under a container in an Azure Cosmos database using a SQL statement with parameterized values. It returns a CosmosResultSetStreamIterator.
        ///  For more information on preparing SQL statements with parameterized values, please see <see cref="CosmosSqlQueryDefinition"/>.
        /// </summary>
        /// <param name="sqlQueryDefinition">The cosmos SQL query definition.</param>
        /// <param name="partitionKey">The partition key for the item. <see cref="PartitionKey"/></param>
        /// <param name="maxItemCount">(Optional) The max item count to return as part of the query</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request <see cref="CosmosQueryRequestOptions"/></param>
        /// <remarks>
        /// Query as a stream only supports single partition queries 
        /// </remarks>
        /// <example>
        /// Create a query to get all the ToDoActivity that have a cost greater than 9000 for the specified partition
        /// <code language="c#">
        /// <![CDATA[
        /// public class ToDoActivity{
        ///     public string id {get; set;}
        ///     public string status {get; set;}
        ///     public int cost {get; set;}
        /// }
        /// 
        /// CosmosSqlQueryDefinition sqlQuery = new CosmosSqlQueryDefinition("select * from ToDos t where t.cost > @expensive").UseParameter("@expensive", 9000);
        /// CosmosResultSetIterator setIterator = this.Container.Items.CreateItemQueryAsStream(
        ///     sqlQueryDefinition: sqlQuery, 
        ///     partitionKey: "Error");
        ///     
        /// while (setIterator.HasMoreResults)
        /// {
        ///     using (CosmosResponseMessage response = await setIterator.FetchNextSetAsync())
        ///     {
        ///         using (StreamReader sr = new StreamReader(response.Content))
        ///         using (JsonTextReader jtr = new JsonTextReader(sr))
        ///         {
        ///             JObject result = JObject.Load(jtr);
        ///         }
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public virtual CosmosResultSetIterator CreateItemQueryAsStream(
            CosmosSqlQueryDefinition sqlQueryDefinition,
            object partitionKey,
            int? maxItemCount = null,
            string continuationToken = null,
            CosmosQueryRequestOptions requestOptions = null)
        {
            requestOptions = requestOptions ?? new CosmosQueryRequestOptions();
            Tuple<object, SqlQuerySpec> cxt = new Tuple<object, SqlQuerySpec>(partitionKey, sqlQueryDefinition.ToSqlQuerySpec());

            return new CosmosDefaultResultSetStreamIterator(
                maxItemCount,
                continuationToken, 
                requestOptions, 
                FeedOrQueryRequestExecutor,
                cxt);
        }

        /// <summary>
        ///  This method creates a query for items under a container in an Azure Cosmos database using a SQL statement with parameterized values. It returns a CosmosResultSetStreamIterator.
        ///  For more information on preparing SQL statements with parameterized values, please see <see cref="CosmosSqlQueryDefinition"/>.
        /// </summary>
        /// <param name="sqlQueryText">The cosmos SQL query string.</param>
        /// <param name="partitionKey">The partition key for the item. <see cref="PartitionKey"/></param>
        /// <param name="maxItemCount">(Optional) The max item count to return as part of the query</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request <see cref="CosmosQueryRequestOptions"/></param>
        /// <remarks>
        /// Query as a stream only supports single partition queries 
        /// </remarks>
        /// <example>
        /// Create a query to get all the ToDoActivity that have a cost greater than 9000 for the specified partition
        /// <code language="c#">
        /// <![CDATA[
        /// public class ToDoActivity{
        ///     public string id {get; set;}
        ///     public string status {get; set;}
        ///     public int cost {get; set;}
        /// }
        /// 
        /// CosmosResultSetIterator setIterator = this.Container.Items.CreateItemQueryAsStream(
        ///     sqlQueryText: "select * from ToDos t where t.cost > 9000", 
        ///     partitionKey: "Error");
        ///     
        /// while (setIterator.HasMoreResults)
        /// {
        ///     using (CosmosResponseMessage response = await setIterator.FetchNextSetAsync())
        ///     {
        ///         using (StreamReader sr = new StreamReader(response.Content))
        ///         using (JsonTextReader jtr = new JsonTextReader(sr))
        ///         {
        ///             JObject result = JObject.Load(jtr);
        ///         }
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public virtual CosmosResultSetIterator CreateItemQueryAsStream(
            string sqlQueryText,
            object partitionKey,
            int? maxItemCount = null,
            string continuationToken = null,
            CosmosQueryRequestOptions requestOptions = null)
        {
            return CreateItemQueryAsStream(
                new CosmosSqlQueryDefinition(sqlQueryText),
                partitionKey,
                maxItemCount,
                continuationToken,
                requestOptions);
        }

        /// <summary>
        ///  This method creates a query for items under a container in an Azure Cosmos database using a SQL statement with parameterized values. It returns a CosmosResultSetIterator.
        ///  For more information on preparing SQL statements with parameterized values, please see <see cref="CosmosSqlQueryDefinition"/>.
        /// </summary>
        /// <param name="sqlQueryDefinition">The cosmos SQL query definition.</param>
        /// <param name="partitionKey">The partition key for the item. <see cref="PartitionKey"/></param>
        /// <param name="maxItemCount">(Optional) The max item count to return as part of the query</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request <see cref="CosmosQueryRequestOptions"/></param>
        /// <example>
        /// Create a query to get all the ToDoActivity that have a cost greater than 9000
        /// <code language="c#">
        /// <![CDATA[
        /// public class ToDoActivity{
        ///     public string id {get; set;}
        ///     public string status {get; set;}
        ///     public int cost {get; set;}
        /// }
        /// 
        /// CosmosSqlQueryDefinition sqlQuery = new CosmosSqlQueryDefinition("select * from ToDos t where t.cost > @expensive").UseParameter("@expensive", 9000);
        /// CosmosResultSetIterator<ToDoActivity> setIterator = this.Container.Items.CreateItemQuery<ToDoActivity>(
        ///     sqlQueryDefinition: sqlQuery, 
        ///     partitionKey: "Error");
        ///     
        /// while (setIterator.HasMoreResults)
        /// {
        ///     foreach(var item in await setIterator.FetchNextSetAsync()){
        ///     {
        ///         Console.WriteLine(item.cost); 
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public virtual CosmosResultSetIterator<T> CreateItemQuery<T>(
            CosmosSqlQueryDefinition sqlQueryDefinition,
            object partitionKey,
            int? maxItemCount = null,
            string continuationToken = null,
            CosmosQueryRequestOptions requestOptions = null)
        {
            CosmosQueryRequestOptions options = requestOptions ?? new CosmosQueryRequestOptions();
            if (partitionKey != null)
            {
                PartitionKey pk = new PartitionKey(partitionKey);
                options.PartitionKey = pk;
            }

            options.EnableCrossPartitionQuery = false;

            return new CosmosDefaultResultSetIterator<T>(
                maxItemCount,
                continuationToken,
                options,
                NextResultSetAsync<T>,
                sqlQueryDefinition.ToSqlQuerySpec());
        }

        /// <summary>
        ///  This method creates a query for items under a container in an Azure Cosmos database using a SQL statement with parameterized values. It returns a CosmosResultSetIterator.
        ///  For more information on preparing SQL statements with parameterized values, please see <see cref="CosmosSqlQueryDefinition"/>.
        /// </summary>
        /// <param name="sqlQueryText">The cosmos SQL query text.</param>
        /// <param name="partitionKey">The partition key for the item. <see cref="PartitionKey"/></param>
        /// <param name="maxItemCount">(Optional) The max item count to return as part of the query</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request <see cref="CosmosQueryRequestOptions"/></param>
        /// <example>
        /// Create a query to get all the ToDoActivity that have a cost greater than 9000
        /// <code language="c#">
        /// <![CDATA[
        /// public class ToDoActivity{
        ///     public string id {get; set;}
        ///     public string status {get; set;}
        ///     public int cost {get; set;}
        /// }
        /// 
        /// CosmosResultSetIterator<ToDoActivity> setIterator = this.Container.Items.CreateItemQuery<ToDoActivity>(
        ///     sqlQueryText: "select * from ToDos t where t.cost > 9000", 
        ///     partitionKey: "Error");
        ///     
        /// while (setIterator.HasMoreResults)
        /// {
        ///     foreach(var item in await setIterator.FetchNextSetAsync()){
        ///     {
        ///         Console.WriteLine(item.cost); 
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public virtual CosmosResultSetIterator<T> CreateItemQuery<T>(
            string sqlQueryText,
            object partitionKey,
            int? maxItemCount = null,
            string continuationToken = null,
            CosmosQueryRequestOptions requestOptions = null)
        {
            return CreateItemQuery<T>(
                new CosmosSqlQueryDefinition(sqlQueryText),
                partitionKey,
                maxItemCount,
                continuationToken,
                requestOptions);
        }

        /// <summary>
        ///  This method creates a query for items under a container in an Azure Cosmos database using a SQL statement with parameterized values. It returns a CosmosResultSetIterator.
        ///  For more information on preparing SQL statements with parameterized values, please see <see cref="CosmosSqlQueryDefinition"/>.
        /// </summary>
        /// <param name="sqlQueryDefinition">The cosmos SQL query definition.</param>
        /// <param name="maxConcurrency">The number of concurrent operations run client side during parallel query execution in the Azure Cosmos DB service.</param>
        /// <param name="maxItemCount">(Optional) The max item count to return as part of the query</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request <see cref="CosmosQueryRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <example>
        /// Create a query to get all the ToDoActivity that have a cost greater than 9000
        /// <code language="c#">
        /// <![CDATA[
        /// public class ToDoActivity{
        ///     public string id {get; set;}
        ///     public string status {get; set;}
        ///     public int cost {get; set;}
        /// }
        /// 
        /// CosmosSqlQueryDefinition sqlQuery = new CosmosSqlQueryDefinition("select * from ToDos t where t.cost > @expensive").UseParameter("@expensive", 9000);
        /// CosmosResultSetIterator<ToDoActivity> setIterator = this.Container.Items.CreateItemQuery<ToDoActivity>(
        ///     sqlQuery,
        ///     maxConcurrency: 2);
        ///     
        /// while (setIterator.HasMoreResults)
        /// {
        ///     foreach(var item in await setIterator.FetchNextSetAsync()){
        ///     {
        ///         Console.WriteLine(item.cost); 
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public virtual CosmosResultSetIterator<T> CreateItemQuery<T>(
            CosmosSqlQueryDefinition sqlQueryDefinition,
            int maxConcurrency,
            int? maxItemCount = null,
            string continuationToken = null,
            CosmosQueryRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            CosmosQueryRequestOptions options = requestOptions ?? new CosmosQueryRequestOptions();
            options.maxConcurrency = maxConcurrency;
            options.EnableCrossPartitionQuery = true;

            return new CosmosDefaultResultSetIterator<T>(
                maxItemCount,
                continuationToken,
                options,
                NextResultSetAsync<T>,
                sqlQueryDefinition.ToSqlQuerySpec());
        }

        /// <summary>
        ///  This method creates a query for items under a container in an Azure Cosmos database using a SQL statement with parameterized values. It returns a CosmosResultSetIterator.
        ///  For more information on preparing SQL statements with parameterized values, please see <see cref="CosmosSqlQueryDefinition"/>.
        /// </summary>
        /// <param name="sqlQueryText">The cosmos SQL query text.</param>
        /// <param name="maxConcurrency">The number of concurrent operations run client side during parallel query execution in the Azure Cosmos DB service.</param>
        /// <param name="maxItemCount">(Optional) The max item count to return as part of the query</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request <see cref="CosmosQueryRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <example>
        /// Create a query to get all the ToDoActivity that have a cost greater than 9000
        /// <code language="c#">
        /// <![CDATA[
        /// public class ToDoActivity{
        ///     public string id {get; set;}
        ///     public string status {get; set;}
        ///     public int cost {get; set;}
        /// }
        /// 
        /// CosmosResultSetIterator<ToDoActivity> setIterator = this.Container.Items.CreateItemQuery<ToDoActivity>(
        ///     "select * from ToDos t where t.cost > 9000",
        ///     maxConcurrency: 2);
        ///     
        /// while (setIterator.HasMoreResults)
        /// {
        ///     foreach(var item in await setIterator.FetchNextSetAsync()){
        ///     {
        ///         Console.WriteLine(item.cost); 
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public virtual CosmosResultSetIterator<T> CreateItemQuery<T>(
            string sqlQueryText,
            int maxConcurrency,
            int? maxItemCount = null,
            string continuationToken = null,
            CosmosQueryRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return CreateItemQuery<T>(
                new CosmosSqlQueryDefinition(sqlQueryText),
                maxConcurrency,
                maxItemCount,
                continuationToken,
                requestOptions,
                cancellationToken);
        }

        internal async Task<CosmosQueryResponse<T>> NextResultSetAsync<T>(
            int? maxItemCount,
            string continuationToken,
            CosmosRequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            CosmosQueryRequestOptions cosmosQueryRequestOptions = options as CosmosQueryRequestOptions ?? new CosmosQueryRequestOptions();
            FeedOptions feedOptions = cosmosQueryRequestOptions.ToFeedOptions();
            feedOptions.RequestContinuation = continuationToken;
            feedOptions.MaxItemCount = maxItemCount;

            IDocumentQuery<T> documentClientResult = this.client.DocumentClient.CreateDocumentQuery<T>(
                collectionLink: this.container.Link,
                feedOptions: feedOptions,
                querySpec: state as SqlQuerySpec).AsDocumentQuery();

            try
            {
                FeedResponse<T> feedResponse = await documentClientResult.ExecuteNextAsync<T>(cancellationToken);
                return CosmosQueryResponse<T>.CreateResponse<T>(feedResponse, feedResponse.ResponseContinuation, documentClientResult.HasMoreResults);
            }
            catch (DocumentClientException exception)
            {
                throw new CosmosException(
                    message: exception.Message,
                    statusCode: exception.StatusCode.HasValue ? exception.StatusCode.Value : HttpStatusCode.InternalServerError,
                    subStatusCode: (int)exception.GetSubStatus(),
                    activityId: exception.ActivityId,
                    requestCharge: exception.RequestCharge);
            }
        }

        /// <summary>
        /// Process item operations that do not have an input object (Read/Delete).
        /// </summary>
        internal virtual Task<CosmosItemResponse<T>> ProcessItemAsync<T>(
            object partitionKey,
            string itemId,
            OperationType operationType,
            CosmosRequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            Uri resourceUri = GetResourceUri(requestOptions, operationType, itemId);
            return ExecUtils.ProcessResourceOperationAsync<CosmosItemResponse<T>>(
                this.container.Database.Client,
                resourceUri,
                ResourceType.Document,
                operationType,
                requestOptions,
                partitionKey,
                null,
                null,
                response => this.client.ResponseFactory.CreateItemResponse<T>(response),
                cancellationToken);
        }

        /// <summary>
        /// Process item operations that have an input object that need to be serialized
        /// </summary>
        internal virtual Task<CosmosItemResponse<T>> ProcessItemAsync<T>(
            object partitionKey,
            string itemId,
            T item,
            OperationType operationType,
            CosmosRequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            CosmosItems.ValidatePartitionKey(partitionKey, requestOptions);
            Uri resourceUri = GetResourceUri(requestOptions, operationType, itemId);
            return ExecUtils.ProcessResourceOperationAsync<CosmosItemResponse<T>>(
                this.container.Database.Client,
                resourceUri,
                ResourceType.Document,
                operationType,
                requestOptions,
                partitionKey,
                this.cosmosJsonSerializer.ToStream(item),
                null,
                response => this.client.ResponseFactory.CreateItemResponse<T>(response),
                cancellationToken);
        }

        internal virtual Task<CosmosResponseMessage> ProcessItemStreamAsync(
            object partitionKey,
            string itemId,
            Stream streamPayload,
            OperationType operationType,
            CosmosRequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            CosmosItems.ValidatePartitionKey(partitionKey, requestOptions);
            Uri resourceUri = GetResourceUri(requestOptions, operationType, itemId);

            return ExecUtils.ProcessResourceOperationAsync<CosmosResponseMessage>(
                this.container.Database.Client,
                resourceUri,
                ResourceType.Document,
                operationType,
                requestOptions,
                partitionKey,
                streamPayload,
                null,
                response => response,
                cancellationToken);
        }

        private Task<CosmosResponseMessage> ItemStreamFeedRequestExecutor(
            int? maxItemCount,
            string continuationToken,
            CosmosRequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            Uri resourceUri = this.container.LinkUri;
            return ExecUtils.ProcessResourceOperationAsync<CosmosResponseMessage>(
                client: this.container.Database.Client,
                resourceUri: resourceUri,
                resourceType: ResourceType.Document,
                operationType: OperationType.ReadFeed,
                requestOptions: options,
                requestEnricher: request =>
                {
                    CosmosQueryRequestOptions.FillContinuationToken(request, continuationToken);
                    CosmosQueryRequestOptions.FillMaxItemCount(request, maxItemCount);
                },
                responseCreator: response => response,
                partitionKey: null,
                streamPayload: null,
                cancellationToken: cancellationToken);
        }

        private Task<CosmosQueryResponse<T>> ItemFeedRequestExecutor<T>(
            int? maxItemCount,
           string continuationToken,
           CosmosRequestOptions options,
           object state,
           CancellationToken cancellationToken)
        {
            Uri resourceUri = this.container.LinkUri;
            return ExecUtils.ProcessResourceOperationAsync<CosmosQueryResponse<T>>(
                client: this.container.Database.Client,
                resourceUri: resourceUri,
                resourceType: ResourceType.Document,
                operationType: OperationType.ReadFeed,
                requestOptions: options,
                requestEnricher: request =>
                {
                    CosmosQueryRequestOptions.FillContinuationToken(request, continuationToken);
                    CosmosQueryRequestOptions.FillMaxItemCount(request, maxItemCount);
                },
                responseCreator: response => this.client.ResponseFactory.CreateResultSetQueryResponse<T>(response),
                partitionKey: null,
                streamPayload: null,
                cancellationToken: cancellationToken);
        }

        private Task<CosmosResponseMessage> FeedOrQueryRequestExecutor(
            int? maxItemCount,
            string continuationToken,
            CosmosRequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            Uri resourceUri = this.container.LinkUri;
            Tuple<object, SqlQuerySpec> cxt = (Tuple<object, SqlQuerySpec>)state;
            object partitonKey = cxt.Item1;
            SqlQuerySpec querySpec = cxt.Item2;
            if (partitonKey == null)
            {
                throw new NotImplementedException(nameof(partitonKey));
            }

            if (querySpec == null)
            {
                throw new NotImplementedException(nameof(querySpec));
            }

            OperationType queryOperationType = this.client.CosmosConfiguration.ConnectionMode == ConnectionMode.Direct ? OperationType.Query : OperationType.SqlQuery;
            Stream streamPayload = this.cosmosJsonSerializer.ToStream(querySpec);
            return ExecUtils.ProcessResourceOperationAsync<CosmosResponseMessage>(
                client: this.container.Database.Client,
                resourceUri: resourceUri,
                resourceType: ResourceType.Document,
                operationType: queryOperationType,
                requestOptions: options,
                requestEnricher: request =>
                {
                    CosmosQueryRequestOptions.FillContinuationToken(request, continuationToken);
                    CosmosQueryRequestOptions.FillMaxItemCount(request, maxItemCount);
                },
                responseCreator: response => response,
                partitionKey: partitonKey,
                streamPayload: streamPayload,
                cancellationToken: cancellationToken);
        }

        internal Uri GetResourceUri(CosmosRequestOptions requestOptions, OperationType operationType, string itemId)
        {
            if (requestOptions != null && requestOptions.TryGetResourceUri(out Uri resourceUri))
            {
                return resourceUri;
            }

            switch (operationType)
            {
                case OperationType.Create:
                case OperationType.Upsert:
                    return this.container.LinkUri;

                default:
                    return ContcatCachedUriWithId(itemId);
            }
        }

        /// <summary>
        /// Throw an exception if the partition key is null or empty string
        /// </summary>
        internal static void ValidatePartitionKey(object partitionKey, CosmosRequestOptions requestOptions)
        {
            if (partitionKey != null && (partitionKey is string && !string.IsNullOrEmpty((string)partitionKey)))
            {
                return;
            }

            if (requestOptions.Properties.TryGetValue(
                WFConstants.BackendHeaders.EffectivePartitionKeyString,
                out object partitionKeyValue) && partitionKeyValue != null)
            {
                return;
            }

            throw new ArgumentNullException(nameof(partitionKey));
        }

        /// <summary>
        /// Gets the full resource segment URI without the last id.
        /// </summary>
        /// <returns>Example: /dbs/*/colls/*/{this.pathSegment}/ </returns>
        private string GetResourceSegmentUriWithoutId()
        {
            // StringBuilder is roughly 2x faster than string.Format
            StringBuilder stringBuilder = new StringBuilder(this.container.Link.Length +
                                                            Paths.DocumentsPathSegment.Length + 2);
            stringBuilder.Append(this.container.Link);
            stringBuilder.Append("/");
            stringBuilder.Append(Paths.DocumentsPathSegment);
            stringBuilder.Append("/");
            return stringBuilder.ToString();
        }

        /// <summary>
        /// Gets the full resource URI using the cached resource URI segment 
        /// </summary>
        /// <param name="resourceId">The resource id</param>
        /// <returns>
        /// A document link in the format of {CachedUriSegmentWithoutId}/{0}/ with {0} being a Uri escaped version of the <paramref name="resourceId"/>
        /// </returns>
        /// <remarks>Would be used when creating an <see cref="Attachment"/>, or when replacing or deleting a item in Azure Cosmos DB.</remarks>
        /// <seealso cref="Uri.EscapeUriString"/>
        private Uri ContcatCachedUriWithId(string resourceId)
        {
            return new Uri(this.cachedUriSegmentWithoutId + Uri.EscapeUriString(resourceId), UriKind.Relative);
        }
    }
}
