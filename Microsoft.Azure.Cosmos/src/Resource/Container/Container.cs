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
    /// See <see cref="Cosmos.Database"/> for creating new containers, and reading/querying all containers.
    /// </summary>
    /// <remarks>
    ///  Note: all these operations make calls against a fixed budget.
    ///  You should design your system such that these calls scale sub linearly with your application.
    ///  For instance, do not call `container.readAsync()` before every single `container.readItemAsync()` call to ensure the container exists;
    ///  do this once on application start up.
    /// </remarks>
    public abstract class Container
    {
        /// <summary>
        /// The Id of the Cosmos container
        /// </summary>
        public abstract string Id { get; }

        /// <summary>
        /// Returns the parent Database reference
        /// </summary>
        public abstract Database Database { get; }

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
        /// <param name="requestOptions">(Optional) The options for the container request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="ContainerResponse"/> which wraps a <see cref="ContainerProperties"/> containing the read resource record.
        /// </returns>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions#typed-api</exception>
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
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads a <see cref="ContainerProperties"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the container request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="ResponseMessage"/> containing the read resource record.
        /// </returns>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions#stream-api</exception>
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
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Replace a <see cref="ContainerProperties"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="containerProperties">The <see cref="ContainerProperties"/> object.</param>
        /// <param name="requestOptions">(Optional) The options for the container request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="ContainerResponse"/> which wraps a <see cref="ContainerProperties"/> containing the replace resource record.
        /// </returns>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions#typed-api</exception>
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
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Replace a <see cref="ContainerProperties"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="containerProperties">The <see cref="ContainerProperties"/>.</param>
        /// <param name="requestOptions">(Optional) The options for the container request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="ResponseMessage"/> containing the replace resource record.
        /// </returns>
        /// <example>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions#stream-api</exception>
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
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete a <see cref="ContainerProperties"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the container request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="ContainerResponse"/> which will contain information about the request issued.</returns>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions#typed-api</exception>
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
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete a <see cref="ContainerProperties"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the container request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions#stream-api</exception>
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
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets container throughput in measurement of request units per second in the Azure Cosmos service.
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>Provisioned throughput in request units per second</returns>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions#typed-api</exception>
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
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units">Request Units</seealso>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/set-throughput#set-throughput-on-a-container">Set throughput on a container</seealso>
        public abstract Task<int?> ReadThroughputAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets container throughput in measurement of request units per second in the Azure Cosmos service.
        /// </summary>
        /// <param name="requestOptions">The options for the throughput request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The throughput response</returns>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions#typed-api</exception>
        /// <value>
        /// The provisioned throughput for this container.
        /// </value>
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
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units">Request Units</seealso>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/set-throughput#set-throughput-on-a-container">Set throughput on a container</seealso>
        public abstract Task<ThroughputResponse> ReadThroughputAsync(
            RequestOptions requestOptions,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets throughput provisioned for a container in measurement of request units per second in the Azure Cosmos service.
        /// </summary>
        /// <param name="throughput">The Cosmos container throughput, expressed in Request Units per second.</param>
        /// <param name="requestOptions">(Optional) The options for the throughput request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The throughput response.</returns>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions#typed-api</exception>
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
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units">Request Units</seealso>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/set-throughput#set-throughput-on-a-container">Set throughput on a container</seealso>
        public abstract Task<ThroughputResponse> ReplaceThroughputAsync(
            int throughput,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets throughput provisioned for a container in measurement of request units per second in the Azure Cosmos service.
        /// </summary>
        /// <param name="throughputProperties">The Cosmos container throughput expressed in Request Units per second.</param>
        /// <param name="requestOptions">(Optional) The options for the throughput request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The throughput response.</returns>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions#typed-api</exception>
        /// <example>
        /// The following example shows how to replace the fixed throughput.
        /// <code language="c#">
        /// <![CDATA[
        /// ThroughputResponse throughput = await this.cosmosContainer.ReplaceThroughputAsync(
        ///     ThroughputProperties.CreateManualThroughput(10000));
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// The following example shows how to replace the autoscale provisioned throughput
        /// <code language="c#">
        /// <![CDATA[
        /// ThroughputResponse throughput = await this.cosmosContainer.ReplaceThroughputAsync(
        ///     ThroughputProperties.CreateAutoscaleThroughput(10000));
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units">Request Units</seealso>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/set-throughput#set-throughput-on-a-container">Set throughput on a container</seealso>
        /// </remarks>
        public abstract Task<ThroughputResponse> ReplaceThroughputAsync(
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a Item as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <param name="streamPayload">A <see cref="Stream"/> containing the payload.</param>
        /// <param name="partitionKey">The partition key for the item.</param>
        /// <param name="requestOptions">(Optional) The options for the item request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The <see cref="ResponseMessage"/> that was created contained within a <see cref="System.Threading.Tasks.Task"/> object representing the service response for the asynchronous operation.</returns>
        /// <remarks>
        /// The Stream operation only throws on client side exceptions. This is to increase performance and prevent the overhead of throwing exceptions. Check the HTTP status code on the response to check if the operation failed.
        /// </remarks>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions#stream-api</exception>
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
        ///     //Read or do other operations with the stream
        ///     using (StreamReader streamReader = new StreamReader(response.Content))
        ///     {
        ///         string responseContentAsString = await streamReader.ReadToEndAsync();
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<ResponseMessage> CreateItemStreamAsync(
                    Stream streamPayload,
                    PartitionKey partitionKey,
                    ItemRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a item as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <param name="item">A JSON serializable object that must contain an id property. <see cref="CosmosSerializer"/> to implement a custom serializer</param>
        /// <param name="partitionKey"><see cref="PartitionKey"/> for the item. If not specified will be populated by extracting from {T}</param>
        /// <param name="requestOptions">(Optional) The options for the item request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The <see cref="ItemResponse{T}"/> that was created contained within a <see cref="System.Threading.Tasks.Task"/> object representing the service response for the asynchronous operation.</returns>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions#typed-api</exception>
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
        /// ItemResponse item = await this.container.CreateItemAsync<ToDoActivity>(test, new PartitionKey(test.status));
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<ItemResponse<T>> CreateItemAsync<T>(
            T item,
            PartitionKey? partitionKey = null,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads a item from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="id">The Cosmos item id</param>
        /// <param name="partitionKey">The partition key for the item.</param>
        /// <param name="requestOptions">(Optional) The options for the item request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="ResponseMessage"/> which wraps a <see cref="Stream"/> containing the read resource record.
        /// </returns>
        /// <remarks>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions#stream-api</exception>
        /// The Stream operation only throws on client side exceptions. This is to increase performance and prevent the overhead of throwing exceptions. Check the HTTP status code on the response to check if the operation failed.
        /// </remarks>
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
        ///     //Read or do other operations with the stream
        ///     using (StreamReader streamReader = new StreamReader(response.Content))
        ///     {
        ///         string content = await streamReader.ReadToEndAsync();
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
                    CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads a item from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="id">The Cosmos item id</param>
        /// <param name="partitionKey">The partition key for the item.</param>
        /// <param name="requestOptions">(Optional) The options for the item request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="ItemResponse{T}"/> which wraps the read resource record.
        /// </returns>
        /// <remarks>
        /// Items contain meta data that can be obtained by mapping these meta data attributes to properties in <typeparamref name="T"/>.
        /// * "_ts": Gets the last modified time stamp associated with the item from the Azure Cosmos DB service.
        /// * "_etag": Gets the entity tag associated with the item from the Azure Cosmos DB service.
        /// * "ttl": Gets the time to live in seconds of the item in the Azure Cosmos DB service.
        /// Note that, this API does not support the usage of <see cref="RequestOptions.IfMatchEtag"/> property at the moment.
        /// </remarks>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions#typed-api</exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// public class ToDoActivity{
        ///     public string id {get; set;}
        ///     public string status {get; set;}
        /// }
        /// 
        /// Example 1: Reading Item with Full Response
        ///
        /// This example demonstrates how to read an item from the container and retrieve the full
        /// response, including metadata such as request units (RU) consumed, along with the
        /// `ToDoActivity` object.
        ///
        /// ItemResponse<ToDoActivity> toDoActivity = await this.container.ReadItemAsync<ToDoActivity>("id", new PartitionKey("partitionKey"));
        ///
        /// Example 2: Reading Item with Implicit Casting
        ///
        /// This example shows how to read an item from the container and implicitly cast the
        /// response directly to a `ToDoActivity` object, omitting the metadata in the `ItemResponse`.
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
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Upserts an item stream as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <param name="streamPayload">A <see cref="Stream"/> containing the payload.</param>
        /// <param name="partitionKey">The partition key for the item.</param>
        /// <param name="requestOptions">(Optional) The options for the item request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="ResponseMessage"/> which wraps a <see cref="Stream"/> containing the read resource record.
        /// </returns>
        /// <remarks>
        /// The Stream operation only throws on client side exceptions. 
        /// This is to increase performance and prevent the overhead of throwing exceptions. 
        /// Check the HTTP status code on the response to check if the operation failed.
        /// </remarks>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions#stream-api</exception>
        /// <remarks>
        /// <para>
        /// Upsert result i.e. creation or replace can be identified by the status code:
        /// 201 - item created
        /// 200 - item replaced
        /// </para>
        /// </remarks>
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
        ///     //Read or do other operations with the stream
        ///     using (StreamReader streamReader = new StreamReader(response.Content))
        ///     {
        ///         string content = await streamReader.ReadToEndAsync();
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<ResponseMessage> UpsertItemStreamAsync(
                    Stream streamPayload,
                    PartitionKey partitionKey,
                    ItemRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default);

        /// <summary>
        /// Upserts an item as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <param name="item">A JSON serializable object that must contain an id property. <see cref="CosmosSerializer"/> to implement a custom serializer</param>
        /// <param name="partitionKey"><see cref="PartitionKey"/> for the item. If not specified will be populated by extracting from {T}</param>
        /// <param name="requestOptions">(Optional) The options for the item request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The <see cref="ItemResponse{T}"/> that was upserted contained within a <see cref="System.Threading.Tasks.Task"/> object representing the service response for the asynchronous operation.</returns>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions#typed-api</exception>
        /// <remarks>
        /// <para>
        /// Upsert result i.e. creation or replace can be identified by the status code:
        /// 201 - item created
        /// 200 - item replaced
        /// </para>
        /// </remarks>
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
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Replaces a item in the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <remarks>
        /// The item's partition key value is immutable. 
        /// To change an item's partition key value you must delete the original item and insert a new item.
        /// </remarks>
        /// <param name="streamPayload">A <see cref="Stream"/> containing the payload.</param>
        /// <param name="id">The Cosmos item id</param>
        /// <param name="partitionKey">The partition key for the item.</param>
        /// <param name="requestOptions">(Optional) The options for the item request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="ResponseMessage"/> which wraps a <see cref="Stream"/> containing the replace resource record.
        /// </returns>
        /// <remarks>
        /// The Stream operation only throws on client side exceptions. 
        /// This is to increase performance and prevent the overhead of throwing exceptions. 
        /// Check the HTTP status code on the response to check if the operation failed.
        /// </remarks>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions#stream-api</exception>
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
        ///     //Read or do other operations with the stream
        ///     using (StreamReader streamReader = new StreamReader(response.Content))
        ///     {
        ///         string content = await streamReader.ReadToEndAsync();
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
                    CancellationToken cancellationToken = default);

        /// <summary>
        /// Replaces a item in the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <remarks>
        /// The item's partition key value is immutable. 
        /// To change an item's partition key value you must delete the original item and insert a new item.
        /// </remarks>
        /// <param name="item">A JSON serializable object that must contain an id property. <see cref="CosmosSerializer"/> to implement a custom serializer.</param>
        /// <param name="id">The Cosmos item id of the existing item.</param>
        /// <param name="partitionKey"><see cref="PartitionKey"/> for the item. If not specified will be populated by extracting from {T}</param>
        /// <param name="requestOptions">(Optional) The options for the item request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="ItemResponse{T}"/> which wraps the updated resource record.
        /// </returns>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions#typed-api</exception>
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
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads multiple items from a container using Id and PartitionKey values.
        /// </summary>
        /// <param name="items">List of item.Id and <see cref="PartitionKey"/></param>
        /// <param name="readManyRequestOptions">Request Options for ReadMany Operation</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="ResponseMessage"/> which wraps a <see cref="Stream"/> containing the response.
        /// </returns>
        /// <remarks><see cref="Container.ReadManyItemsStreamAsync"/> is meant to perform better latency-wise than a query with IN statements to fetch a large number of independent items.</remarks>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// IReadOnlyList<(string, PartitionKey)> itemList = new List<(string, PartitionKey)>
        /// {
        ///     ("Id1", new PartitionKey("pkValue1")),
        ///     ("Id2", new PartitionKey("pkValue2")),
        ///     ("Id3", new PartitionKey("pkValue3"))
        /// };
        /// 
        /// using (ResponseMessage response = await this.Container.ReadManyItemsStreamAsync(itemList))
        /// {
        ///     if (!response.IsSuccessStatusCode)
        ///     {
        ///         //Handle and log exception
        ///         return;
        ///     }
        ///
        ///     //Read or do other operations with the stream
        ///     using (StreamReader streamReader = new StreamReader(response.Content))
        ///     {
        ///         string content = streamReader.ReadToEndAsync();
        ///     }
        ///     
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<ResponseMessage> ReadManyItemsStreamAsync(
            IReadOnlyList<(string id, PartitionKey partitionKey)> items,
            ReadManyRequestOptions readManyRequestOptions = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads multiple items from a container using Id and PartitionKey values.
        /// </summary>
        /// <param name="items">List of item.Id and <see cref="PartitionKey"/></param>
        /// <param name="readManyRequestOptions">Request Options for ReadMany Operation</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="FeedResponse{T}"/> which wraps the typed items.
        /// </returns>
        /// <remarks><see cref="Container.ReadManyItemsAsync"/> is meant to perform better latency-wise than a query with IN statements to fetch a large number of independent items.</remarks>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// public class ToDoActivity{
        ///     public string id {get; set;}
        ///     public string status {get; set;}
        /// }
        /// 
        /// IReadOnlyList<(string, PartitionKey)> itemList = new List<(string, PartitionKey)>
        /// {
        ///     ("Id1", new PartitionKey("pkValue1")),
        ///     ("Id2", new PartitionKey("pkValue2")),
        ///     ("Id3", new PartitionKey("pkValue3"))
        /// };
        ///
        /// FeedResponse<ToDoActivity> feedResponse = this.Container.ReadManyItemsAsync<ToDoActivity>(itemList);
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<FeedResponse<T>> ReadManyItemsAsync<T>(
            IReadOnlyList<(string id, PartitionKey partitionKey)> items,
            ReadManyRequestOptions readManyRequestOptions = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Patches an item in the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <remarks>
        /// The item's partition key value is immutable. 
        /// To change an item's partition key value you must delete the original item and insert a new item.
        /// The patch operations are atomic and are executed sequentially.
        /// By default, resource body will be returned as part of the response. User can request no content by setting <see cref="ItemRequestOptions.EnableContentResponseOnWrite"/> flag to false.
        /// Note that, this API does not support the usage of <see cref="RequestOptions.IfMatchEtag"/> property at the moment.
        /// </remarks>
        /// <param name="id">The Cosmos item id of the item to be patched.</param>
        /// <param name="partitionKey"><see cref="PartitionKey"/> for the item</param>
        /// <param name="patchOperations">Represents a list of operations to be sequentially applied to the referred Cosmos item.</param>
        /// <param name="requestOptions">(Optional) The options for the item request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="ItemResponse{T}"/> which wraps the updated resource record.
        /// </returns>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions#typed-api</exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// public class ToDoActivity{
        ///     public string id {get; set;}
        ///     public string status {get; set;}
        ///     public string description {get; set;}
        ///     public int frequency {get; set;}
        /// }
        /// 
        /// Example 1: Reading Item with Full Response
        ///
        /// This example demonstrates how to read an item from the container and retrieve the full
        /// response, including metadata such as request units (RU) consumed, along with the
        /// `ToDoActivity` object.
        ///
        /// ItemResponse<ToDoActivity> toDoActivity = await this.container.ReadItemAsync<ToDoActivity>("id", new PartitionKey("partitionKey"));
        ///
        /// Example 2: Reading Item with Implicit Casting
        ///
        /// This example shows how to read an item from the container and implicitly cast the
        /// response directly to a `ToDoActivity` object, omitting the metadata in the `ItemResponse`.
        ///
        /// ToDoActivity toDoActivity = await this.container.ReadItemAsync<ToDoActivity>("id", new PartitionKey("partitionKey"));
        ///
        /// /* toDoActivity = {
        ///     "id" : "someId",
        ///     "status" : "someStatusPK",
        ///     "description" : "someDescription",
        ///     "frequency" : 7
        /// }*/
        /// 
        /// List<PatchOperation> patchOperations = new List<PatchOperation>()
        /// {
        ///     PatchOperation.Add("/daysOfWeek", new string[]{"Monday", "Thursday"}),
        ///     PatchOperation.Replace("/frequency", 2),
        ///     PatchOperation.Remove("/description")
        /// };
        /// 
        /// ItemResponse<dynamic> item = await this.container.PatchItemAsync<dynamic>(toDoActivity.id, new PartitionKey(toDoActivity.status), patchOperations);
        /// /* item.Resource = {
        ///     "id" : "someId",
        ///     "status" : "someStatusPK",
        ///     "description" : null,
        ///     "frequency" : 2,
        ///     "daysOfWeek" : ["Monday", "Thursday"]
        /// }*/
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso href="https://learn.microsoft.com/azure/cosmos-db/partial-document-update#supported-modes">Supported partial document update modes</seealso>
        public abstract Task<ItemResponse<T>> PatchItemAsync<T>(
            string id,
            PartitionKey partitionKey,
            IReadOnlyList<PatchOperation> patchOperations,
            PatchItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Patches an item in the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <remarks>
        /// The item's partition key value is immutable. 
        /// To change an item's partition key value you must delete the original item and insert a new item.
        /// The patch operations are atomic and are executed sequentially.
        /// By default, resource body will be returned as part of the response. User can request no content by setting <see cref="ItemRequestOptions.EnableContentResponseOnWrite"/> flag to false.
        /// </remarks>
        /// <param name="id">The Cosmos item id</param>
        /// <param name="partitionKey">The partition key for the item.</param>
        /// <param name="patchOperations">Represents a list of operations to be sequentially applied to the referred Cosmos item.</param>
        /// <param name="requestOptions">(Optional) The options for the item request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="ResponseMessage"/> which wraps a <see cref="Stream"/> containing the patched resource record.
        /// </returns>
        /// <remarks>
        /// https://aka.ms/cosmosdb-dot-net-exceptions#stream-api
        /// This is to increase performance and prevent the overhead of throwing exceptions. 
        /// Check the HTTP status code on the response to check if the operation failed.
        /// </remarks>
        /// <example>
        /// <see cref="Container.PatchItemAsync"/>
        /// </example>
        /// <seealso href="https://learn.microsoft.com/azure/cosmos-db/partial-document-update#supported-modes">Supported partial document update modes</seealso>
        public abstract Task<ResponseMessage> PatchItemStreamAsync(
            string id,
            PartitionKey partitionKey,
            IReadOnlyList<PatchOperation> patchOperations,
            PatchItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete a item from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="id">The Cosmos item id</param>
        /// <param name="partitionKey">The partition key for the item.</param>
        /// <param name="requestOptions">(Optional) The options for the item request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="ResponseMessage"/> which wraps a <see cref="Stream"/> containing the delete resource record.
        /// </returns>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions#stream-api</exception>
        /// <remarks>
        /// For delete operations, the <see cref="ResponseMessage.Content"/> will be null. Item content is not expected in the response.
        /// 
        /// The Stream operation only throws on client side exceptions. This is to increase performance and prevent the overhead of throwing exceptions. Check the HTTP status code on the response to check if the operation failed.
        /// </remarks>
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
                    CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete a item from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="id">The Cosmos item id</param>
        /// <param name="partitionKey">The partition key for the item.</param>
        /// <param name="requestOptions">(Optional) The options for the item request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="ItemResponse{T}"/> which will contain information about the request issued.</returns>
        /// <remarks>
        /// <see cref="ItemResponse{T}.Resource"/> is <see href="https://docs.microsoft.com/rest/api/cosmos-db/delete-a-document#body">always null</see>
        /// </remarks>
        /// For delete operations, the <see cref="ItemResponse{T}.Resource"/> will be null. Item content is not expected in the response.
        /// 
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions#typed-api</exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// public class ToDoActivity{
        ///     public string id {get; set;}
        ///     public string status {get; set;}
        /// }
        /// 
        /// ItemResponse item = await this.container.DeleteItemAsync<ToDoActivity>("id", new PartitionKey("partitionKey"));
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<ItemResponse<T>> DeleteItemAsync<T>(
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///  This method creates a query for items under a container in an Azure Cosmos database using a SQL statement with parameterized values. It returns a FeedIterator.
        ///  For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/>.
        /// </summary>
        /// <param name="queryDefinition">The Cosmos SQL query definition.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request.</param>
        /// <returns>An iterator to go through the items.</returns>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions#stream-api</exception>
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
        /// using (FeedIterator feedIterator = this.Container.GetItemQueryStreamIterator(
        ///     queryDefinition,
        ///     null,
        ///     new QueryRequestOptions() { PartitionKey = new PartitionKey("Error")}))
        /// {
        ///     while (feedIterator.HasMoreResults)
        ///     {
        ///         using (ResponseMessage response = await feedIterator.ReadNextAsync())
        ///         {
        ///             using (StreamReader sr = new StreamReader(response.Content))
        ///             using (JsonTextReader jtr = new JsonTextReader(sr))
        ///             {
        ///                 JObject result = JObject.Load(jtr);
        ///             }
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
        /// <param name="queryDefinition">The Cosmos SQL query definition.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request.</param>
        /// <returns>An iterator to go through the items.</returns>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions#typed-api</exception>
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
        /// using (FeedIterator<ToDoActivity> feedIterator = this.Container.GetItemQueryIterator<ToDoActivity>(
        ///     queryDefinition,
        ///     null,
        ///     new QueryRequestOptions() { PartitionKey = new PartitionKey("Error")}))
        /// {
        ///     while (feedIterator.HasMoreResults)
        ///     {
        ///         foreach(var item in await feedIterator.ReadNextAsync())
        ///         {
        ///             Console.WriteLine(item.cost); 
        ///         }
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
        ///  This method creates a query for items under a container in an Azure Cosmos database using a SQL statement. It returns a FeedIterator.
        /// </summary>
        /// <param name="queryText">The Cosmos SQL query text.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request.</param>
        /// <returns>An iterator to go through the items.</returns>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions#stream-api</exception>
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
        /// using (FeedIterator feedIterator = this.Container.GetItemQueryStreamIterator(
        ///     "select * from ToDos t where t.cost > 9000",
        ///     null,
        ///     new QueryRequestOptions() { PartitionKey = new PartitionKey("Error")}))
        /// {
        ///     while (feedIterator.HasMoreResults)
        ///     {
        ///         using (ResponseMessage response = await feedIterator.ReadNextAsync())
        ///         {
        ///             using (StreamReader sr = new StreamReader(response.Content))
        ///             using (JsonTextReader jtr = new JsonTextReader(sr))
        ///             {
        ///                 JObject result = JObject.Load(jtr);
        ///             }
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
        /// using (FeedIterator feedIterator = this.Container.GetItemQueryStreamIterator(
        ///     null,
        ///     null,
        ///     new QueryRequestOptions() { PartitionKey = new PartitionKey("Error")}))
        /// {
        ///     while (feedIterator.HasMoreResults)
        ///     {
        ///         using (ResponseMessage response = await feedIterator.ReadNextAsync())
        ///         {
        ///             using (StreamReader sr = new StreamReader(response.Content))
        ///             using (JsonTextReader jtr = new JsonTextReader(sr))
        ///             {
        ///                 JObject result = JObject.Load(jtr);
        ///             }
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
        ///  This method creates a query for items under a container in an Azure Cosmos database using a SQL statement. It returns a FeedIterator.
        /// </summary>
        /// <param name="queryText">The Cosmos SQL query text.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request.</param>
        /// <returns>An iterator to go through the items.</returns>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions#typed-api</exception>
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
        /// using (FeedIterator<ToDoActivity> feedIterator = this.Container.GetItemQueryIterator<ToDoActivity>(
        ///     "select * from ToDos t where t.cost > 9000",
        ///     null,
        ///     new QueryRequestOptions() { PartitionKey = new PartitionKey("Error")}))
        /// {
        ///     while (feedIterator.HasMoreResults)
        ///     {
        ///         foreach(var item in await feedIterator.ReadNextAsync())
        ///         {
        ///             Console.WriteLine(item.cost);
        ///         }
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
        /// using (FeedIterator<ToDoActivity> feedIterator = this.Container.GetItemQueryIterator<ToDoActivity>(
        ///     null,
        ///     null,
        ///     new QueryRequestOptions() { PartitionKey = new PartitionKey("Error")}))
        /// {
        ///     while (feedIterator.HasMoreResults)
        ///     {
        ///         foreach(var item in await feedIterator.ReadNextAsync())
        ///         {
        ///             Console.WriteLine(item.cost); 
        ///         }
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
        ///  This method creates a query for items under a container in an Azure Cosmos database using a SQL statement with parameterized values. It returns a FeedIterator.
        ///  For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/>.
        /// </summary>
        /// <param name="feedRange">A FeedRange obtained from <see cref="Container.GetFeedRangesAsync(CancellationToken)"/></param>
        /// <param name="queryDefinition">The Cosmos SQL query definition.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request.</param>
        /// <returns>An iterator to go through the items.</returns>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions#stream-api</exception>
        /// <example>
        /// Create a query to get all the ToDoActivity that have a cost greater than 9000 for the specified partition
        /// <code language="c#">
        /// <![CDATA[
        /// public class ToDoActivity{
        ///     public string id {get; set;}
        ///     public string status {get; set;}
        ///     public int cost {get; set;}
        /// }
        /// IReadOnlyList<FeedRange> feedRanges = await this.Container.GetFeedRangesAsync();
        /// // Distribute feedRanges across multiple compute units and pass each one to a different iterator
        /// QueryDefinition queryDefinition = new QueryDefinition("select * from ToDos t where t.cost > @expensive")
        ///     .WithParameter("@expensive", 9000);
        /// using (FeedIterator feedIterator = this.Container.GetItemQueryStreamIterator(
        ///     feedRanges[0],
        ///     queryDefinition,
        ///     null,
        ///     new QueryRequestOptions() { }))
        /// {
        ///     while (feedIterator.HasMoreResults)
        ///     {
        ///         using (ResponseMessage response = await feedIterator.ReadNextAsync())
        ///         {
        ///             using (StreamReader sr = new StreamReader(response.Content))
        ///             using (JsonTextReader jtr = new JsonTextReader(sr))
        ///             {
        ///                 JObject result = JObject.Load(jtr);
        ///             }
        ///         }
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract FeedIterator GetItemQueryStreamIterator(
            FeedRange feedRange,
            QueryDefinition queryDefinition,
            string continuationToken,
            QueryRequestOptions requestOptions = null);

        /// <summary>
        ///  This method creates a query for items under a container in an Azure Cosmos database using a SQL statement with parameterized values. It returns a FeedIterator.
        ///  For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/>.
        /// </summary>
        /// <param name="feedRange">A FeedRange obtained from <see cref="Container.GetFeedRangesAsync(CancellationToken)"/>.</param>
        /// <param name="queryDefinition">The Cosmos SQL query definition.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request.</param>
        /// <returns>An iterator to go through the items.</returns>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions#typed-api</exception>
        /// <example>
        /// Create a query to get all the ToDoActivity that have a cost greater than 9000 for the specified partition
        /// <code language="c#">
        /// <![CDATA[
        /// public class ToDoActivity{
        ///     public string id {get; set;}
        ///     public string status {get; set;}
        ///     public int cost {get; set;}
        /// }
        /// IReadOnlyList<FeedRange> feedRanges = await this.Container.GetFeedRangesAsync();
        /// // Distribute feedRanges across multiple compute units and pass each one to a different iterator
        /// QueryDefinition queryDefinition = new QueryDefinition("select * from ToDos t where t.cost > @expensive")
        ///     .WithParameter("@expensive", 9000);
        /// using (FeedIterator<ToDoActivity> feedIterator = this.Container.GetItemQueryIterator<ToDoActivity>(
        ///     feedRanges[0],
        ///     queryDefinition,
        ///     null,
        ///     new QueryRequestOptions() { }))
        /// {
        ///     while (feedIterator.HasMoreResults)
        ///     {
        ///         foreach(var item in await feedIterator.ReadNextAsync())
        ///         {
        ///             Console.WriteLine(item.cost); 
        ///         }
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract FeedIterator<T> GetItemQueryIterator<T>(
            FeedRange feedRange,
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);

        /// <summary>
        /// This method creates a LINQ query for items under a container in an Azure Cosmos DB service.
        /// IQueryable extension method ToFeedIterator() should be use for asynchronous execution with FeedIterator, please refer to example 2.
        /// </summary>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions#typed-api</exception>
        /// <remarks>
        /// LINQ execution is synchronous which will cause issues related to blocking calls. 
        /// It is recommended to always use ToFeedIterator(), and to do the asynchronous execution.
        /// </remarks>
        /// <typeparam name="T">The type of object to query.</typeparam>
        /// <param name="allowSynchronousQueryExecution">(Optional)the option which allows the query to be executed synchronously via IOrderedQueryable.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request.</param>
        /// <param name="linqSerializerOptions">(Optional) The options to configure Linq Serializer Properties. This overrides properties in CosmosSerializerOptions while creating client</param>
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
        /// using (FeedIterator<Book> setIterator = container.GetItemLinqQueryable<Book>()
        ///                      .Where(b => b.Title == "War and Peace")
        ///                      .ToFeedIterator())
        /// {                   
        ///     //Asynchronous query execution
        ///     while (setIterator.HasMoreResults)
        ///     {
        ///         foreach(var item in await setIterator.ReadNextAsync())
        ///         {
        ///             Console.WriteLine(item.Price); 
        ///         }
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// The Azure Cosmos DB LINQ provider compiles LINQ to SQL statements. Refer to https://docs.microsoft.com/azure/cosmos-db/sql-query-linq-to-sql for the list of expressions supported by the Azure Cosmos DB LINQ provider. ToString() on the generated IQueryable returns the translated SQL statement. The Azure Cosmos DB provider translates JSON.NET and DataContract serialization attributes for members to their JSON property names.
        /// </remarks>
        /// <seealso cref="CosmosSerializationOptions"/>
        public abstract IOrderedQueryable<T> GetItemLinqQueryable<T>(
            bool allowSynchronousQueryExecution = false,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            CosmosLinqSerializerOptions linqSerializerOptions = null);

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
        /// <param name="estimatedPendingChanges">An estimation in number of transactions.</param>
        /// <param name="cancellationToken">A cancellation token representing the current cancellation status of the <see cref="ChangeFeedProcessor"/> instance.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation that is going to be done with the estimation.</returns>
        /// <remarks>
        /// The estimation over the Change Feed identifies volumes of transactions. If operations in the container are performed through stored procedures, transactional batch or bulk, a group of operations may share the same <see href="https://docs.microsoft.com/azure/cosmos-db/stored-procedures-triggers-udfs#transactions">transaction scope</see> and represented by a single transaction. 
        /// In those cases, the estimation might not exactly represent number of items, but it is still valid to understand if the pending volume is increasing, decreasing, or on a steady state.
        /// </remarks>
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

        /// <summary>
        /// Gets a <see cref="ChangeFeedEstimator"/> for change feed monitoring.
        /// </summary>
        /// <param name="processorName">The name of the Processor the Estimator is going to measure.</param>
        /// <param name="leaseContainer">Instance of a Cosmos Container that holds the leases.</param>
        /// <remarks>
        /// The goal of the Estimator is to measure progress of a particular processor. In order to do that, the <paramref name="processorName"/> and other parameters, like the leases container, need to match that of the Processor to measure.
        /// </remarks>
        /// <returns>An instance of <see cref="ChangeFeedEstimator"/></returns>
        public abstract ChangeFeedEstimator GetChangeFeedEstimator(
            string processorName,
            Container leaseContainer);

        /// <summary>
        /// Initializes a new instance of <see cref="TransactionalBatch"/>
        /// that can be used to perform operations across multiple items
        /// in the container with the provided partition key in a transactional manner.
        /// </summary>
        /// <param name="partitionKey">The partition key for all items in the batch.</param>
        /// <returns>A new instance of <see cref="TransactionalBatch"/>.</returns>
        /// <remarks>
        /// <seealso href="https://learn.microsoft.com/azure/cosmos-db/concepts-limits#per-request-limits">Limits on TransactionalBatch requests</seealso>
        /// </remarks>
        public abstract TransactionalBatch CreateTransactionalBatch(PartitionKey partitionKey);

        /// <summary>
        /// Obtains a list of <see cref="FeedRange"/> that can be used to parallelize Feed operations.
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A list of <see cref="FeedRange"/>.</returns>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions#typed-api</exception>
        public abstract Task<IReadOnlyList<FeedRange>> GetFeedRangesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        ///  This method creates an iterator to consume a Change Feed.
        /// </summary>
        /// <param name="changeFeedStartFrom">Where to start the changefeed from.</param>
        /// <param name="changeFeedMode">Defines the mode on which to consume the change feed.</param>
        /// <param name="changeFeedRequestOptions">(Optional) The options for the Change Feed consumption.</param>
        /// <seealso cref="Container.GetFeedRangesAsync(CancellationToken)"/>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions#stream-api</exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// ChangeFeedRequestOptions options = new ChangeFeedRequestOptions()
        /// {
        ///     PageSizeHint = 10,
        /// }
        /// 
        /// FeedIterator feedIterator = this.Container.GetChangeFeedStreamIterator(
        ///     ChangeFeedStartFrom.Beginning(),
        ///     ChangeFeedMode.Incremental,
        ///     options);
        ///
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     using (ResponseMessage response = await feedIterator.ReadNextAsync())
        ///     {
        ///         if (response.StatusCode == NotModified) 
        ///         {
        ///             // No new changes
        ///             // Capture response.ContinuationToken and break or sleep for some time
        ///         }
        ///         else 
        ///         {
        ///             using (StreamReader sr = new StreamReader(response.Content))
        ///             using (JsonTextReader jtr = new JsonTextReader(sr))
        ///             {
        ///                 JObject result = JObject.Load(jtr);
        ///             }
        ///         }
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <returns>An iterator to go through the Change Feed.</returns>
        public abstract FeedIterator GetChangeFeedStreamIterator(
            ChangeFeedStartFrom changeFeedStartFrom,
            ChangeFeedMode changeFeedMode,
            ChangeFeedRequestOptions changeFeedRequestOptions = null);

        /// <summary>
        ///  This method creates an iterator to consume a Change Feed.
        /// </summary>
        /// <param name="changeFeedStartFrom">Where to start the changefeed from.</param>
        /// <param name="changeFeedMode">Defines the mode on which to consume the change feed.</param>
        /// <param name="changeFeedRequestOptions">(Optional) The options for the Change Feed consumption.</param>
        /// <seealso cref="Container.GetFeedRangesAsync(CancellationToken)"/>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions#typed-api</exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// ChangeFeedRequestOptions options = new ChangeFeedRequestOptions()
        /// {
        ///     PageSizeHint = 10,
        /// }
        /// 
        /// FeedIterator<MyItem> feedIterator = this.Container.GetChangeFeedIterator<MyItem>(
        ///     ChangeFeedStartFrom.Beginning(),
        ///     ChangeFeedMode.Incremental,
        ///     options);
        ///     
        ///     while (feedIterator.HasMoreResults)
        ///     {
        ///         FeedResponse<MyItem> response = await feedIterator.ReadNextAsync();
        ///
        ///         if (response.StatusCode == NotModified) 
        ///         {
        ///             // No new changes
        ///             // Capture response.ContinuationToken and break or sleep for some time
        ///         }
        ///         else 
        ///         {
        ///             foreach (var item in response)
        ///             {
        ///                 Console.WriteLine(item);
        ///             }
        ///         }
        ///     }
        /// ]]>
        /// </code>
        /// </example>
        /// <returns>An iterator to go through the Change Feed.</returns>
        public abstract FeedIterator<T> GetChangeFeedIterator<T>(
            ChangeFeedStartFrom changeFeedStartFrom,
            ChangeFeedMode changeFeedMode,
            ChangeFeedRequestOptions changeFeedRequestOptions = null);

        /// <summary>
        /// Delegate to receive the changes within a <see cref="ChangeFeedProcessor"/> execution.
        /// </summary>
        /// <param name="context">The context related to the changes.</param>
        /// <param name="changes">The changes that happened.</param>
        /// <param name="cancellationToken">A cancellation token representing the current cancellation status of the <see cref="ChangeFeedProcessor"/> instance.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation that is going to be done with the changes.</returns>
        public delegate Task ChangeFeedHandler<T>(
            ChangeFeedProcessorContext context,
            IReadOnlyCollection<T> changes,
            CancellationToken cancellationToken);

        /// <summary>
        /// Delegate to receive the changes within a <see cref="ChangeFeedProcessor"/> execution with manual checkpoint.
        /// </summary>
        /// <param name="context">The context related to the changes.</param>
        /// <param name="changes">The changes that happened.</param>
        /// <param name="checkpointAsync">A task representing an asynchronous checkpoint on the progress of a lease.</param>
        /// <param name="cancellationToken">A cancellation token representing the current cancellation status of the <see cref="ChangeFeedProcessor"/> instance.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation that is going to be done with the changes.</returns>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// (ChangeFeedProcessorContext context, IReadOnlyCollection<T> changes, Func<Task> checkpointAsync, CancellationToken cancellationToken) =>
        /// {
        ///     // consume changes
        ///     
        ///     // On certain condition, we can checkpoint
        ///     await checkpointAsync();
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public delegate Task ChangeFeedHandlerWithManualCheckpoint<T>(
            ChangeFeedProcessorContext context,
            IReadOnlyCollection<T> changes,
            Func<Task> checkpointAsync,
            CancellationToken cancellationToken);

        /// <summary>
        /// Delegate to receive the changes within a <see cref="ChangeFeedProcessor"/> execution.
        /// </summary>
        /// <param name="context">The context related to the changes.</param>
        /// <param name="changes">The changes that happened.</param>
        /// <param name="cancellationToken">A cancellation token representing the current cancellation status of the <see cref="ChangeFeedProcessor"/> instance.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation that is going to be done with the changes.</returns>
        public delegate Task ChangeFeedStreamHandler(
            ChangeFeedProcessorContext context,
            Stream changes,
            CancellationToken cancellationToken);

        /// <summary>
        /// Delegate to receive the changes within a <see cref="ChangeFeedProcessor"/> execution with manual checkpoint.
        /// </summary>
        /// <param name="context">The context related to the changes.</param>
        /// <param name="changes">The changes that happened.</param>
        /// <param name="checkpointAsync">A task representing an asynchronous checkpoint on the progress of a lease.</param>
        /// <param name="cancellationToken">A cancellation token representing the current cancellation status of the <see cref="ChangeFeedProcessor"/> instance.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation that is going to be done with the changes.</returns>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// (ChangeFeedProcessorContext context, Stream stream, Func<Task> checkpointAsync, CancellationToken cancellationToken) =>
        /// {
        ///     // consume stream
        ///     
        ///     // On certain condition, we can checkpoint
        ///     await checkpointAsync();
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public delegate Task ChangeFeedStreamHandlerWithManualCheckpoint(
            ChangeFeedProcessorContext context,
            Stream changes,
            Func<Task> checkpointAsync,
            CancellationToken cancellationToken);

        /// <summary>
        /// Delegate to notify errors during change feed operations.
        /// </summary>
        /// <param name="leaseToken">A unique identifier for the lease.</param>
        /// <param name="exception">The exception that happened.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation that is going to be done with the notification.</returns>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// (string leaseToken, Exception exception) =>
        /// {
        ///     if (exception is ChangeFeedProcessorUserException userException)
        ///     {
        ///         Console.WriteLine($"Current instance's delegate had an unhandled when processing lease {leaseToken}.");
        ///         Console.WriteLine($"Diagnostics {userException.ExceptionContext.Diagnostics}");
        ///         Console.WriteLine($"Headers {userException.ExceptionContext.Headers}");
        ///         Console.WriteLine(userException.ToString());
        ///     }
        ///     else 
        ///     {
        ///         Console.WriteLine($"Current instance faced an exception when processing lease {leaseToken}.");
        ///         Console.WriteLine(exception.ToString());
        ///     }
        ///     
        ///     return Task.CompletedTask;
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public delegate Task ChangeFeedMonitorErrorDelegate(
            string leaseToken,
            Exception exception);

        /// <summary>
        /// Delegate to notify events of leases being acquired by a change feed processor.
        /// </summary>
        /// <param name="leaseToken">A unique identifier for the lease.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation that is going to be done with the notification.</returns>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// (string leaseToken) =>
        /// {
        ///     Console.WriteLine($"Current instance released lease {leaseToken} and stopped processing it.");
        ///     
        ///     return Task.CompletedTask;
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public delegate Task ChangeFeedMonitorLeaseAcquireDelegate(string leaseToken);

        /// <summary>
        /// Delegate to notify events of leases being releases by a change feed processor.
        /// </summary>
        /// <param name="leaseToken">A unique identifier for the lease.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation that is going to be done with the notification.</returns>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// (string leaseToken) =>
        /// {
        ///     Console.WriteLine($"Current instance acquired lease {leaseToken} and will start processing it.");
        ///     
        ///     return Task.CompletedTask;
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public delegate Task ChangeFeedMonitorLeaseReleaseDelegate(string leaseToken);

        /// <summary>
        /// Initializes a <see cref="ChangeFeedProcessorBuilder"/> for change feed processing.
        /// </summary>
        /// <param name="processorName">A name that identifies the Processor and the particular work it will do.</param>
        /// <param name="onChangesDelegate">Delegate to receive changes.</param>
        /// <returns>An instance of <see cref="ChangeFeedProcessorBuilder"/></returns>
        public abstract ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilder<T>(
            string processorName,
            ChangeFeedHandler<T> onChangesDelegate);

        /// <summary>
        /// Initializes a <see cref="ChangeFeedProcessorBuilder"/> for change feed processing with manual checkpoint.
        /// </summary>
        /// <param name="processorName">A name that identifies the Processor and the particular work it will do.</param>
        /// <param name="onChangesDelegate">Delegate to receive changes.</param>
        /// <returns>An instance of <see cref="ChangeFeedProcessorBuilder"/></returns>
        public abstract ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilderWithManualCheckpoint<T>(
            string processorName,
            ChangeFeedHandlerWithManualCheckpoint<T> onChangesDelegate);

        /// <summary>
        /// Initializes a <see cref="ChangeFeedProcessorBuilder"/> for change feed processing.
        /// </summary>
        /// <param name="processorName">A name that identifies the Processor and the particular work it will do.</param>
        /// <param name="onChangesDelegate">Delegate to receive changes.</param>
        /// <returns>An instance of <see cref="ChangeFeedProcessorBuilder"/></returns>
        public abstract ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilder(
            string processorName,
            ChangeFeedStreamHandler onChangesDelegate);

        /// <summary>
        /// Initializes a <see cref="ChangeFeedProcessorBuilder"/> for change feed processing with manual checkpoint.
        /// </summary>
        /// <param name="processorName">A name that identifies the Processor and the particular work it will do.</param>
        /// <param name="onChangesDelegate">Delegate to receive changes.</param>
        /// <returns>An instance of <see cref="ChangeFeedProcessorBuilder"/></returns>
        public abstract ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilderWithManualCheckpoint(
            string processorName,
            ChangeFeedStreamHandlerWithManualCheckpoint onChangesDelegate);

        /// <summary>
        /// Deletes all items in the Container with the specified <see cref="PartitionKey"/> value.
        /// Starts an asynchronous Cosmos DB background operation which deletes all items in the Container with the specified value. 
        /// The asynchronous Cosmos DB background operation runs using a percentage of user RUs.
        /// </summary>
        /// <param name="partitionKey">The <see cref="PartitionKey"/> of the items to be deleted.</param>
        /// <param name="requestOptions">(Optional) The options for the Partition Key Delete request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="ResponseMessage"/>.
        /// </returns>
        public virtual Task<ResponseMessage> DeleteAllItemsByPartitionKeyStreamAsync(
               Cosmos.PartitionKey partitionKey,
               RequestOptions requestOptions = null,
               CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Deriving classes are expected to override this method with a valid implementation");
        }

#if PREVIEW
        /// <summary>
        /// Gets the list of Partition Key Range identifiers for a <see cref="FeedRange"/>.
        /// </summary>
        /// <param name="feedRange">A <see cref="FeedRange"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The list of Partition Key Range identifiers affected by a particular FeedRange.</returns>
        /// <seealso cref="Container.GetFeedRangesAsync(CancellationToken)"/>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions#typed-api</exception>
        public abstract Task<IEnumerable<string>> GetPartitionKeyRangesAsync(
            FeedRange feedRange,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Initializes a <see cref="GetChangeFeedProcessorBuilderWithAllVersionsAndDeletes"/> for change feed processing with all versions and deletes.
        /// </summary>
        /// <typeparam name="T">Document type</typeparam>
        /// <param name="processorName">A name that identifies the Processor and the particular work it will do.</param>
        /// <param name="onChangesDelegate">Delegate to receive all changes and deletes</param>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// Container leaseContainer = await this.database.CreateContainerAsync(
        ///     new ContainerProperties(id: "leases", partitionKeyPath: "/id"),
        ///     cancellationToken: this.cancellationToken);
        ///     
        /// ManualResetEvent allProcessedDocumentsEvent = new ManualResetEvent(false);
        /// 
        /// ChangeFeedProcessor changeFeedProcessor = this.Container
        ///     .GetChangeFeedProcessorBuilderWithAllVersionsAndDeletes(processorName: "processor", onChangesDelegate: (ChangeFeedProcessorContext context, IReadOnlyCollection<ChangeFeedItem<dynamic>> documents, CancellationToken token) =>
        /// {
        ///     Console.WriteLine($"number of documents processed: {documents.Count}");
        ///     
        ///     string id = default;
        ///     string pk = default;
        ///     string description = default;
        ///     
        ///     foreach (ChangeFeedItem<dynamic> changeFeedItem in documents)
        ///     {
        ///         if (changeFeedItem.Metadata.OperationType != ChangeFeedOperationType.Delete)
        ///         {
        ///             id = changeFeedItem.Current.id.ToString();
        ///             pk = changeFeedItem.Current.pk.ToString();
        ///             description = changeFeedItem.Current.description.ToString();
        ///         }
        ///         else
        ///         {
        ///             id = changeFeedItem.Previous.id.ToString();
        ///             pk = changeFeedItem.Previous.pk.ToString();
        ///             description = changeFeedItem.Previous.description.ToString();
        ///         }
        ///         
        ///         ChangeFeedOperationType operationType = changeFeedItem.Metadata.OperationType;
        ///         long previousLsn = changeFeedItem.Metadata.PreviousLsn;
        ///         DateTime conflictResolutionTimestamp = changeFeedItem.Metadata.ConflictResolutionTimestamp;
        ///         long lsn = changeFeedItem.Metadata.Lsn;
        ///         bool isTimeToLiveExpired = changeFeedItem.Metadata.IsTimeToLiveExpired;
        ///     }
        ///     
        ///     return Task.CompletedTask;
        /// })
        /// .WithInstanceName(Guid.NewGuid().ToString())
        /// .WithLeaseContainer(leaseContainer)
        /// .WithErrorNotification((leaseToken, error) =>
        /// {
        ///     Console.WriteLine(error.ToString());
        ///     
        ///     return Task.CompletedTask;
        /// })
        /// .Build();
        /// 
        /// await changeFeedProcessor.StartAsync();
        /// await Task.Delay(1000);
        /// await this.Container.CreateItemAsync<dynamic>(new { id = "1", pk = "1", description = "original test" }, partitionKey: new PartitionKey("1"));
        /// await this.Container.UpsertItemAsync<dynamic>(new { id = "1", pk = "1", description = "test after replace" }, partitionKey: new PartitionKey("1"));
        /// await this.Container.DeleteItemAsync<dynamic>(id: "1", partitionKey: new PartitionKey("1"));
        /// 
        /// allProcessedDocumentsEvent.WaitOne(10 * 1000);
        /// 
        /// await changeFeedProcessor.StopAsync();
        /// ]]>
        /// </code>
        /// </example>
        /// <returns>An instance of <see cref="ChangeFeedProcessorBuilder"/></returns>
        public abstract ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilderWithAllVersionsAndDeletes<T>(
            string processorName,
            ChangeFeedHandler<ChangeFeedItem<T>> onChangesDelegate);

        /// <summary>
        /// Determines whether the given y feed range is a part of the specified x feed range.
        /// </summary>
        /// <param name="x">The feed range representing the x range.</param>
        /// <param name="y">The feed range representing the y range.</param>
        /// <param name="cancellationToken">A token to cancel the operation if needed.</param>
        /// <example>
        /// <code language="csharp">
        /// <![CDATA[
        /// CancellationToken cancellationToken = ...;
        /// CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder("your-connection-string");
        /// CosmosClient cosmosClient = cosmosClientBuilder.Build();
        /// Database cosmosDatabase = cosmosClient.GetDatabase("your-database-id");
        /// Container container = cosmosDatabase.GetContainer("your-container-id");
        /// FeedRange x = ...; // Define the feed range for x
        /// FeedRange y = ...;  // Define the feed range for y
        ///
        /// bool isFeedRangePartOfAsync = await container.IsFeedRangePartOfAsync(
        ///    x,
        ///    y,
        ///    cancellationToken);
        /// ]]>
        /// </code>
        /// </example>
        /// <returns>Returns a boolean indicating whether the y feed range is fully contained within the x feed range.</returns>
        public virtual Task<bool> IsFeedRangePartOfAsync(
            Cosmos.FeedRange x,
            Cosmos.FeedRange y,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
#endif
    }
}