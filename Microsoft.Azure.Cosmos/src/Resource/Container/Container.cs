//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Operations for reading, replacing, or deleting a specific, existing container or item in a container by id.
    /// There are two different types of operations.
    /// 1. The object operations where it serializes and deserializes the item on request/response
    /// 2. The stream response which takes a Stream containing a JSON serialized object and returns a response containing a Stream
    /// <see cref="Cosmos.Database"/> for creating new containers, and reading/querying all containers;
    /// </summary>
    /// <remarks>
    ///  Note: all these operations make calls against a fixed budget.
    ///  You should design your system such that these calls scale sub linearly with your application.
    ///  For instance, do not call `container.readAsync()` before every single `item.read()` call, to ensure the cosmosContainer exists;
    ///  do this once on application start up.
    /// </remarks>
    public abstract class Container
    {
        /// <summary>
        /// The Id of the Cosmos container
        /// </summary>
        public abstract string Id { get; }

        /// <summary>
        /// Returns the conflicts
        /// </summary>
        public abstract Conflicts Conflicts { get; }

        /// <summary>
        /// Returns the scripts
        /// </summary>
        public abstract Scripts.Scripts Scripts { get; }

        /// <summary>
        /// Reads a <see cref="ContainerProperties"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the container request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="ContainerResponse"/> which wraps a <see cref="ContainerProperties"/> containing the read resource record.
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
        /// <code language="c#">
        /// <![CDATA[
        /// Container container = this.database.GetContainer("containerId");
        /// ContainerProperties containerProperties = await container.ReadContainerAsync();
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<ContainerResponse> ReadContainerAsync(
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Reads a <see cref="ContainerProperties"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the container request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="ResponseMessage"/> containing the read resource record.
        /// </returns>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// Container container = this.database.GetContainer("containerId");
        /// ResponseMessage response = await container.ReadContainerStreamAsync();
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<ResponseMessage> ReadContainerStreamAsync(
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Replace a <see cref="ContainerProperties"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="containerProperties">The <see cref="ContainerProperties"/> object.</param>
        /// <param name="requestOptions">(Optional) The options for the container request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="ContainerResponse"/> which wraps a <see cref="ContainerProperties"/> containing the replace resource record.
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
        /// Update the container to disable automatic indexing
        /// <code language="c#">
        /// <![CDATA[
        /// ContainerProperties containerProperties = containerReadResponse;
        /// containerProperties.IndexingPolicy.Automatic = false;
        /// ContainerResponse response = await container.ReplaceContainerAsync(containerProperties);
        /// ContainerProperties replacedProperties = response;
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<ContainerResponse> ReplaceContainerAsync(
            ContainerProperties containerProperties,
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Replace a <see cref="ContainerProperties"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="containerProperties">The <see cref="ContainerProperties"/>.</param>
        /// <param name="requestOptions">(Optional) The options for the container request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="ResponseMessage"/> containing the replace resource record.
        /// </returns>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// ContainerProperties containerProperties = containerReadResponse;
        /// containerProperties.IndexingPolicy.Automatic = false;
        /// ResponseMessage response = await container.ReplaceContainerStreamAsync(containerProperties);
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<ResponseMessage> ReplaceContainerStreamAsync(
            ContainerProperties containerProperties,
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Delete a <see cref="ContainerProperties"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the container request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="ContainerResponse"/> which will contain information about the request issued.</returns>
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
        /// Container container = this.database.GetContainer("containerId");
        /// ContainerResponse response = await container.DeleteContainerAsync();
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<ContainerResponse> DeleteContainerAsync(
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Delete a <see cref="ContainerProperties"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the container request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// Container container = this.database.GetContainer("containerId");
        /// ResponseMessage response = await container.DeleteContainerStreamAsync();
        /// ]]>
        /// </code>
        /// </example>
        /// <returns>A <see cref="Task"/> containing a <see cref="ResponseMessage"/> which will contain information about the request issued.</returns>
        public abstract Task<ResponseMessage> DeleteContainerStreamAsync(
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Gets container throughput in measurement of request units per second in the Azure Cosmos service.
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>Provisioned throughput in request units per second</returns>
        /// <value>
        /// The provisioned throughput for this container.
        /// </value>
        /// <remarks>
        /// <para>
        /// Null value indicates a container with no throughput provisioned.
        /// </para>
        /// </remarks>
        /// <example>
        /// The following example shows how to get the throughput.
        /// <code language="c#">
        /// <![CDATA[
        /// int? throughput = await container.ReadThroughputAsync();
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units"/>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/set-throughput#set-throughput-on-a-database"/>
        public abstract Task<int?> ReadThroughputAsync(
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Gets container throughput in measurement of request units per second in the Azure Cosmos service.
        /// </summary>
        /// <param name="requestOptions">The options for the throughput request.<see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The throughput response</returns>
        /// <value>
        /// The provisioned throughput for this container.
        /// </value>
        /// <remarks>
        /// <para>
        /// Refer to http://azure.microsoft.com/documentation/articles/documentdb-performance-levels/ for details on provision offer throughput.
        /// </para>
        /// </remarks>
        /// <example>
        /// The following example shows how to get the throughput
        /// <code language="c#">
        /// <![CDATA[
        /// RequestOptions requestOptions = new RequestOptions();
        /// ThroughputProperties throughputProperties = await container.ReadThroughputAsync(requestOptions);
        /// Console.WriteLine($"Throughput: {throughputProperties?.Throughput}");
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// The following example shows how to get throughput, MinThroughput and is replace in progress
        /// <code language="c#">
        /// <![CDATA[
        /// RequestOptions requestOptions = new RequestOptions();
        /// ThroughputResponse response = await container.ReadThroughputAsync(requestOptions);
        /// Console.WriteLine($"Throughput: {response.Resource?.Throughput}");
        /// Console.WriteLine($"MinThroughput: {response.MinThroughput}");
        /// Console.WriteLine($"IsReplacePending: {response.IsReplacePending}");
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<ThroughputResponse> ReadThroughputAsync(
            RequestOptions requestOptions,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Sets throughput provisioned for a container in measurement of request units per second in the Azure Cosmos service.
        /// </summary>
        /// <param name="throughput">The cosmos container throughput, expressed in Request Units per second.</param>
        /// <param name="requestOptions">(Optional) The options for the throughput request.<see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The throughput response.</returns>
        /// <value>
        /// The provisioned throughput for this container.
        /// </value>
        /// <example>
        /// The following example shows how to get the throughput.
        /// <code language="c#">
        /// <![CDATA[
        /// ThroughputResponse throughput = await this.cosmosContainer.ReplaceThroughputAsync(400);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units"/> for details on provision throughput.
        /// </remarks>
        public abstract Task<ThroughputResponse> ReplaceThroughputAsync(
            int throughput,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Creates a Item as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <param name="streamPayload">A <see cref="Stream"/> containing the payload.</param>
        /// <param name="partitionKey">The partition key for the item. <see cref="PartitionKey"/></param>
        /// <param name="requestOptions">(Optional) The options for the item request <see cref="ItemRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The <see cref="ResponseMessage"/> that was created contained within a <see cref="System.Threading.Tasks.Task"/> object representing the service response for the asynchronous operation.</returns>
        /// <exception>
        /// The Stream operation only throws on client side exceptions. This is to increase performance and prevent the overhead of throwing exceptions. Check the HTTP status code on the response to check if the operation failed.
        /// </exception>
        /// <example>
        /// This example creates an item in a Cosmos container.
        /// <code language="c#">
        /// <![CDATA[
        /// //Create the object in Cosmos
        /// using (ResponseMessage response = await this.Container.CreateItemStreamAsync(partitionKey: new PartitionKey("streamPartitionKey"), streamPayload: stream))
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
        public abstract Task<ResponseMessage> CreateItemStreamAsync(
                    Stream streamPayload,
                    PartitionKey partitionKey,
                    ItemRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Creates a item as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <param name="item">A JSON serializable object that must contain an id property. <see cref="CosmosSerializer"/> to implement a custom serializer</param>
        /// <param name="partitionKey">Partition key for the item. If not specified will be populated by extracting from {T}</param>
        /// <param name="requestOptions">(Optional) The options for the item request <see cref="ItemRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The <see cref="ItemResponse{T}"/> that was created contained within a <see cref="System.Threading.Tasks.Task"/> object representing the service response for the asynchronous operation.</returns>
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
        /// ItemResponse item = await this.container.CreateItemAsync<ToDoActivity>(tests, new PartitionKey(test.status));
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<ItemResponse<T>> CreateItemAsync<T>(
            T item,
            PartitionKey? partitionKey = null,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Reads a item from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="id">The cosmos item id</param>
        /// <param name="partitionKey">The partition key for the item. <see cref="PartitionKey"/></param>
        /// <param name="requestOptions">(Optional) The options for the item request <see cref="ItemRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="ResponseMessage"/> which wraps a <see cref="Stream"/> containing the read resource record.
        /// </returns>
        /// <exception>
        /// The Stream operation only throws on client side exceptions. This is to increase performance and prevent the overhead of throwing exceptions. Check the HTTP status code on the response to check if the operation failed.
        /// </exception>
        /// <example>
        /// Read a response as a stream.
        /// <code language="c#">
        /// <![CDATA[
        /// using(ResponseMessage response = await this.container.ReadItemStreamAsync("id", new PartitionKey("partitionKey")))
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
        public abstract Task<ResponseMessage> ReadItemStreamAsync(
                    string id,
                    PartitionKey partitionKey,
                    ItemRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Reads a item from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="id">The cosmos item id</param>
        /// <param name="partitionKey">The partition key for the item. <see cref="PartitionKey"/></param>
        /// <param name="requestOptions">(Optional) The options for the item request <see cref="ItemRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="ItemResponse{T}"/> which wraps the read resource record.
        /// </returns>
        /// <remarks>
        /// Items contain meta data that can be obtained by mapping these meta data attributes to properties in <typeparamref name="T"/>.
        /// * "_ts": Gets the last modified time stamp associated with the item from the Azure Cosmos DB service.
        /// * "_etag": Gets the entity tag associated with the item from the Azure Cosmos DB service.
        /// * "ttl": Gets the time to live in seconds of the item in the Azure Cosmos DB service.
        /// </remarks>
        /// <exception cref="CosmosException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
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
        /// ToDoActivity toDoActivity = await this.container.ReadItemAsync<ToDoActivity>("id", new PartitionKey("partitionKey"));
        /// 
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<ItemResponse<T>> ReadItemAsync<T>(
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Upserts an item stream as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <param name="streamPayload">A <see cref="Stream"/> containing the payload.</param>
        /// <param name="partitionKey">The partition key for the item. <see cref="PartitionKey"/></param>
        /// <param name="requestOptions">(Optional) The options for the item request <see cref="ItemRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="ResponseMessage"/> which wraps a <see cref="Stream"/> containing the read resource record.
        /// </returns>
        /// <exception>
        /// The Stream operation only throws on client side exceptions. 
        /// This is to increase performance and prevent the overhead of throwing exceptions. 
        /// Check the HTTP status code on the response to check if the operation failed.
        /// </exception>
        /// <example>
        /// Upsert a Stream containing the item to Cosmos
        /// <code language="c#">
        /// <![CDATA[
        /// using(ResponseMessage response = await this.container.UpsertItemStreamAsync(stream, new PartitionKey("itemPartitionKey")))
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
        public abstract Task<ResponseMessage> UpsertItemStreamAsync(
                    Stream streamPayload,
                    PartitionKey partitionKey,
                    ItemRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Upserts an item as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <param name="item">A JSON serializable object that must contain an id property. <see cref="CosmosSerializer"/> to implement a custom serializer</param>
        /// <param name="partitionKey">Partition key for the item. If not specified will be populated by extracting from {T}</param>
        /// <param name="requestOptions">(Optional) The options for the item request <see cref="ItemRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The <see cref="ItemResponse{T}"/> that was upserted contained within a <see cref="System.Threading.Tasks.Task"/> object representing the service response for the asynchronous operation.</returns>
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
        /// ItemResponse<ToDoActivity> item = await this.container.UpsertItemAsync<ToDoActivity>(test, new PartitionKey(test.status));
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<ItemResponse<T>> UpsertItemAsync<T>(
            T item,
            PartitionKey? partitionKey = null,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Replaces a item in the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="streamPayload">A <see cref="Stream"/> containing the payload.</param>
        /// <param name="id">The cosmos item id</param>
        /// <param name="partitionKey">The partition key for the item. <see cref="PartitionKey"/></param>
        /// <param name="requestOptions">(Optional) The options for the item request <see cref="ItemRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="ResponseMessage"/> which wraps a <see cref="Stream"/> containing the replace resource record.
        /// </returns>
        /// <exception>
        /// The Stream operation only throws on client side exceptions. 
        /// This is to increase performance and prevent the overhead of throwing exceptions. 
        /// Check the HTTP status code on the response to check if the operation failed.
        /// </exception>
        /// <example>
        /// Replace an item in Cosmos
        /// <code language="c#">
        /// <![CDATA[
        /// using(ResponseMessage response = await this.container.ReplaceItemStreamAsync(stream, "itemId", new PartitionKey("itemPartitionKey"))
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
        public abstract Task<ResponseMessage> ReplaceItemStreamAsync(
                    Stream streamPayload,
                    string id,
                    PartitionKey partitionKey,
                    ItemRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Replaces a item in the Azure Cosmos service as an asynchronous operation.
        /// </summary>        
        /// <param name="item">A JSON serializable object that must contain an id property. <see cref="CosmosSerializer"/> to implement a custom serializer.</param>
        /// <param name="id">The cosmos item id, which is expected to match the value within T.</param>
        /// <param name="partitionKey">Partition key for the item. If not specified will be populated by extracting from {T}</param>
        /// <param name="requestOptions">(Optional) The options for the item request <see cref="ItemRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="ItemResponse{T}"/> which wraps the updated resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If either <paramref name="item"/> is not set.</exception>
        /// <exception cref="CosmosException">
        /// This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property.
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
        /// ItemResponse item = await this.container.ReplaceItemAsync<ToDoActivity>(test, test.id, new PartitionKey(test.status));
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<ItemResponse<T>> ReplaceItemAsync<T>(
            T item,
            string id,
            PartitionKey? partitionKey = null,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Delete a item from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="id">The cosmos item id</param>
        /// <param name="partitionKey">The partition key for the item. <see cref="PartitionKey"/></param>
        /// <param name="requestOptions">(Optional) The options for the item request <see cref="ItemRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="ResponseMessage"/> which wraps a <see cref="Stream"/> containing the delete resource record.
        /// </returns>
        /// <exception>
        /// The Stream operation only throws on client side exceptions. This is to increase performance and prevent the overhead of throwing exceptions. Check the HTTP status code on the response to check if the operation failed.
        /// </exception>
        /// <example>
        /// Delete an item from Cosmos
        /// <code language="c#">
        /// <![CDATA[
        /// using(ResponseMessage response = await this.container.DeleteItemStreamAsync("itemId", new PartitionKey("itemPartitionKey")))
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
        public abstract Task<ResponseMessage> DeleteItemStreamAsync(
                    string id,
                    PartitionKey partitionKey,
                    ItemRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Delete a item from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="id">The cosmos item id</param>
        /// <param name="partitionKey">The partition key for the item. <see cref="PartitionKey"/></param>
        /// <param name="requestOptions">(Optional) The options for the item request <see cref="ItemRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="ItemResponse{T}"/> which will contain information about the request issued.</returns>
        /// <exception cref="CosmosException">
        /// This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
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
        /// ItemResponse item = await this.container.DeleteItemAsync<ToDoActivity>("partitionKey", "id");
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<ItemResponse<T>> DeleteItemAsync<T>(
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        ///  This method creates a query for items under a container in an Azure Cosmos database using a SQL statement with parameterized values. It returns a FeedIterator.
        ///  For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/>.
        /// </summary>
        /// <param name="queryDefinition">The cosmos SQL query definition.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request <see cref="QueryRequestOptions"/></param>
        /// <returns>An iterator to go through the items.</returns>
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
        /// QueryDefinition queryDefinition = new QueryDefinition("select * from ToDos t where t.cost > @expensive")
        ///     .WithParameter("@expensive", 9000);
        /// FeedIterator feedIterator = this.Container.GetItemQueryStreamIterator(
        ///     queryDefinition,
        ///     null,
        ///     new QueryRequestOptions() { PartitionKey = new PartitionKey("Error")});
        ///
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     using (ResponseMessage response = await feedIterator.ReadNextAsync())
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
        public abstract FeedIterator GetItemQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);

        /// <summary>
        ///  This method creates a query for items under a container in an Azure Cosmos database using a SQL statement with parameterized values. It returns a FeedIterator.
        ///  For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/>.
        /// </summary>
        /// <param name="queryDefinition">The cosmos SQL query definition.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request <see cref="QueryRequestOptions"/></param>
        /// <returns>An iterator to go through the items.</returns>
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
        /// QueryDefinition queryDefinition = new QueryDefinition("select * from ToDos t where t.cost > @expensive")
        ///     .WithParameter("@expensive", 9000);
        /// FeedIterator<ToDoActivity> feedIterator = this.Container.GetItemQueryIterator<ToDoActivity>(
        ///     queryDefinition,
        ///     null,
        ///     new QueryRequestOptions() { PartitionKey = new PartitionKey("Error")});
        ///
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     foreach(var item in await feedIterator.ReadNextAsync()){
        ///     {
        ///         Console.WriteLine(item.cost); 
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract FeedIterator<T> GetItemQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);

        /// <summary>
        ///  This method creates a query for items under a container in an Azure Cosmos database using a SQL statement with parameterized values. It returns a FeedIterator.
        ///  For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/>.
        /// </summary>
        /// <param name="queryText">The cosmos SQL query text.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request <see cref="QueryRequestOptions"/></param>
        /// <returns>An iterator to go through the items.</returns>
        /// <remarks>
        /// Query as a stream only supports single partition queries 
        /// </remarks>
        /// <example>
        /// 1. Create a query to get all the ToDoActivity that have a cost greater than 9000 for the specified partition
        /// <code language="c#">
        /// <![CDATA[
        /// public class ToDoActivity{
        ///     public string id {get; set;}
        ///     public string status {get; set;}
        ///     public int cost {get; set;}
        /// }
        /// 
        /// FeedIterator feedIterator = this.Container.GetItemQueryStreamIterator(
        ///     "select * from ToDos t where t.cost > 9000",
        ///     null,
        ///     new QueryRequestOptions() { PartitionKey = new PartitionKey("Error")});
        ///
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     using (ResponseMessage response = await feedIterator.ReadNextAsync())
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
        /// <example>
        /// 2. Creates a FeedIterator to get all the ToDoActivity.
        /// <code language="c#">
        /// <![CDATA[
        /// public class ToDoActivity{
        ///     public string id {get; set;}
        ///     public string status {get; set;}
        ///     public int cost {get; set;}
        /// }
        ///
        /// FeedIterator feedIterator = this.Container.GetItemQueryStreamIterator(
        ///     null,
        ///     null,
        ///     new QueryRequestOptions() { PartitionKey = new PartitionKey("Error")});
        ///
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     using (ResponseMessage response = await feedIterator.ReadNextAsync())
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
        public abstract FeedIterator GetItemQueryStreamIterator(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);

        /// <summary>
        ///  This method creates a query for items under a container in an Azure Cosmos database using a SQL statement with parameterized values. It returns a FeedIterator.
        ///  For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/>.
        /// </summary>
        /// <param name="queryText">The cosmos SQL query text.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request <see cref="QueryRequestOptions"/></param>
        /// <returns>An iterator to go through the items.</returns>
        /// <example>
        /// 1. Create a query to get all the ToDoActivity that have a cost greater than 9000
        /// <code language="c#">
        /// <![CDATA[
        /// public class ToDoActivity{
        ///     public string id {get; set;}
        ///     public string status {get; set;}
        ///     public int cost {get; set;}
        /// }
        /// 
        /// FeedIterator<ToDoActivity> feedIterator = this.Container.GetItemQueryIterator<ToDoActivity>(
        ///     "select * from ToDos t where t.cost > 9000",
        ///     null,
        ///     new QueryRequestOptions() { PartitionKey = new PartitionKey("Error")});
        ///
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     foreach(var item in await feedIterator.ReadNextAsync()){
        ///     {
        ///         Console.WriteLine(item.cost);
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// 2. Create a FeedIterator to get all the ToDoActivity.
        /// <code language="c#">
        /// <![CDATA[
        /// public class ToDoActivity{
        ///     public string id {get; set;}
        ///     public string status {get; set;}
        ///     public int cost {get; set;}
        /// }
        ///
        /// FeedIterator<ToDoActivity> feedIterator = this.Container.GetItemQueryIterator<ToDoActivity>(
        ///     null,
        ///     null,
        ///     new QueryRequestOptions() { PartitionKey = new PartitionKey("Error")});
        ///
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     foreach(var item in await feedIterator.ReadNextAsync()){
        ///     {
        ///         Console.WriteLine(item.cost); 
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract FeedIterator<T> GetItemQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);

        /// <summary>
        /// This method creates a LINQ query for items under a container in an Azure Cosmos DB service.
        /// IQueryable extension method ToFeedIterator() should be use for asynchronous execution with FeedIterator, please refer to example 2.
        /// </summary>
        /// <remarks>
        /// LINQ execution is synchronous which will cause issues related to blocking calls. 
        /// It is recommended to always use ToFeedIterator(), and to do the asynchronous execution.
        /// </remarks>
        /// <typeparam name="T">The type of object to query.</typeparam>
        /// <param name="allowSynchronousQueryExecution">(Optional)the option which allows the query to be executed synchronously via IOrderedQueryable.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional)The options for the item query request.<see cref="QueryRequestOptions"/></param>
        /// <returns>(Optional) An IOrderedQueryable{T} that can evaluate the query.</returns>
        /// <example>
        /// 1. This example below shows LINQ query generation and blocked execution.
        /// <code language="c#">
        /// <![CDATA[
        /// public class Book 
        /// {
        ///     public string Title {get; set;}
        ///     
        ///     public Author Author {get; set;}
        ///     
        ///     public int Price {get; set;}
        /// }
        /// 
        /// public class Author
        /// {
        ///     public string FirstName {get; set;}
        ///     public string LastName {get; set;}
        /// }
        ///  
        /// // Query by the Title property
        /// Book book = container.GetItemLinqQueryable<Book>(true)
        ///                      .Where(b => b.Title == "War and Peace")
        ///                      .AsEnumerable()
        ///                      .FirstOrDefault();
        /// 
        /// // Query a nested property
        /// Book otherBook = container.GetItemLinqQueryable<Book>(true)
        ///                           .Where(b => b.Author.FirstName == "Leo")
        ///                           .AsEnumerable()
        ///                           .FirstOrDefault();
        /// 
        /// // Perform iteration on books
        /// foreach (Book matchingBook in container.GetItemLinqQueryable<Book>(true)
        ///                            .Where(b => b.Price > 100))
        /// {
        ///     // Iterate through books
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// 2. This example below shows LINQ query generation and asynchronous execution with FeedIterator.
        /// <code language="c#">
        /// <![CDATA[
        ///
        /// // LINQ query generation
        /// var setIterator = container.GetItemLinqQueryable<Book>()
        ///                      .Where(b => b.Title == "War and Peace")
        ///                      .ToFeedIterator();
        ///                      
        /// //Asynchronous query execution
        /// while (setIterator.HasMoreResults)
        /// {
        ///     foreach(var item in await feedIterator.ReadNextAsync()){
        ///     {
        ///         Console.WriteLine(item.cost); 
        ///     }
        /// }
        ///
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// The Azure Cosmos DB LINQ provider compiles LINQ to SQL statements. Refer to http://azure.microsoft.com/documentation/articles/documentdb-sql-query/#linq-to-documentdb-sql for the list of expressions supported by the Azure Cosmos DB LINQ provider. ToString() on the generated IQueryable returns the translated SQL statement. The Azure Cosmos DB provider translates JSON.NET and DataContract serialization attributes for members to their JSON property names.
        /// </remarks>
        public abstract IOrderedQueryable<T> GetItemLinqQueryable<T>(
            bool allowSynchronousQueryExecution = false,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);

        /// <summary>
        /// Delegate to receive the changes within a <see cref="ChangeFeedProcessor"/> execution.
        /// </summary>
        /// <param name="changes">The changes that happened.</param>
        /// <param name="cancellationToken">A cancellation token representing the current cancellation status of the <see cref="ChangeFeedProcessor"/> instance.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation that is going to be done with the changes.</returns>
        public delegate Task ChangesHandler<T>(
            IReadOnlyCollection<T> changes,
            CancellationToken cancellationToken);

        /// <summary>
        /// Delegate to receive the estimation of pending changes to be read by the associated <see cref="ChangeFeedProcessor"/> instance.
        /// </summary>
        /// <param name="estimatedPendingChanges">An estimation in number of items.</param>
        /// <param name="cancellationToken">A cancellation token representing the current cancellation status of the <see cref="ChangeFeedProcessor"/> instance.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation that is going to be done with the estimation.</returns>
        public delegate Task ChangesEstimationHandler(
            long estimatedPendingChanges,
            CancellationToken cancellationToken);

        /// <summary>
        /// Initializes a <see cref="ChangeFeedProcessorBuilder"/> for change feed processing.
        /// </summary>
        /// <param name="processorName">A name that identifies the Processor and the particular work it will do.</param>
        /// <param name="onChangesDelegate">Delegate to receive changes.</param>
        /// <returns>An instance of <see cref="ChangeFeedProcessorBuilder"/></returns>
        public abstract ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilder<T>(
            string processorName,
            ChangesHandler<T> onChangesDelegate);

        /// <summary>
        /// Initializes a <see cref="ChangeFeedProcessorBuilder"/> for change feed monitoring.
        /// </summary>
        /// <param name="processorName">The name of the Processor the Estimator is going to measure.</param>
        /// <param name="estimationDelegate">Delegate to receive estimation.</param>
        /// <param name="estimationPeriod">Time interval on which to report the estimation. Default is 5 seconds.</param>
        /// <remarks>
        /// The goal of the Estimator is to measure progress of a particular processor. In order to do that, the <paramref name="processorName"/> and other parameters, like the leases container, need to match that of the Processor to measure.
        /// </remarks>
        /// <returns>An instance of <see cref="ChangeFeedProcessorBuilder"/></returns>
        public abstract ChangeFeedProcessorBuilder GetChangeFeedEstimatorBuilder(
            string processorName,
            ChangesEstimationHandler estimationDelegate,
            TimeSpan? estimationPeriod = null);

#if PREVIEW
        /// <summary>
        /// Initializes a new instance of the <see cref="Batch"/> class.
        /// </summary>
        /// <param name="partitionKey">The partition key for all items in the batch. <see cref="PartitionKey"/>.</param>
        /// <returns>An instance of <see cref="Batch"/></returns>
        /// <exception>
        /// This API only throws on client side exceptions. This is to increase performance and prevent the overhead of throwing exceptions. Check the HTTP status code on the response to check if the operation failed.
        /// </exception>
        /// <example>
        /// This example atomically modifies a set of documents as a batch.
        /// <code language="c#">
        /// <![CDATA[
        /// public class ToDoActivity
        /// {
        ///     public string type { get; set; }
        ///     public string id { get; set; }
        ///     public string status { get; set; }
        /// }
        ///
        /// string activityType = "personal";
        /// ToDoActivity test1 = new ToDoActivity()
        /// {
        ///     type = activityType,
        ///     id = "learning",
        ///     status = "ToBeDone"
        /// };
        ///
        /// ToDoActivity test2 = new ToDoActivity()
        /// {
        ///     type = activityType,
        ///     id = "shopping",
        ///     status = "Done"
        /// };
        ///
        /// ToDoActivity test3 = new ToDoActivity()
        /// {
        ///     type = activityType,
        ///     id = "swimming",
        ///     status = "ToBeDone"
        /// };
        ///
        /// using (BatchResponse batchResponse = await container.CreateBatch(new Cosmos.PartitionKey(activityType))
        ///     .CreateItem<ToDoActivity>(test1)
        ///     .ReplaceItem<ToDoActivity>(test2.id, test2)
        ///     .UpsertItem<ToDoActivity>(test3)
        ///     .DeleteItem("reading")
        ///     .CreateItemStream(streamPayload1)
        ///     .ReplaceItemStream("eating", streamPayload2)
        ///     .UpsertItemStream(streamPayload3)
        ///     .ExecuteAsync())
        /// {
        ///    if (!batchResponse.IsSuccessStatusCode)
        ///    {
        ///        // Handle and log exception
        ///        return;
        ///    }
        ///
        ///    // Look up interested results - eg. via typed access on operation results
        ///    BatchOperationResult<ToDoActivity> replaceResult = batchResponse.GetOperationResultAtIndex<ToDoActivity>(0);
        ///    ToDoActivity readActivity = replaceResult.Resource;
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// This example atomically reads a set of documents as a batch.
        /// <code language="c#">
        /// <![CDATA[
        /// string activityType = "personal";
        /// using (BatchResponse batchResponse = await container.CreateBatch(new Cosmos.PartitionKey(activityType))
        ///    .ReadItem("playing")
        ///    .ReadItem("walking")
        ///    .ReadItem("jogging")
        ///    .ReadItem("running")
        ///    .ExecuteAsync())
        /// {
        ///     // Look up interested results - eg. via direct access to operation result stream
        ///     List<string> resultItems = new List<string>();
        ///     foreach (BatchOperationResult operationResult in batchResponse)
        ///     {
        ///         using (StreamReader streamReader = new StreamReader(operationResult.ResourceStream))
        ///         {
        ///             resultItems.Add(await streamReader.ReadToEndAsync());
        ///         }
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract Batch CreateBatch(PartitionKey partitionKey);
#endif
    }
}