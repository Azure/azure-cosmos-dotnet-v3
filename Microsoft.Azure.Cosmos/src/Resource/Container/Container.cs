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
    using Microsoft.Azure.Cosmos.Patch;
    using Microsoft.Azure.Cosmos.Query;

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
        /// <param name="requestOptions">(Optional) The options for the container request.</param>
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
        /// <param name="requestOptions">(Optional) The options for the container request.</param>
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
        /// <param name="requestOptions">(Optional) The options for the container request.</param>
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
        /// <param name="requestOptions">(Optional) The options for the container request.</param>
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
        /// <param name="requestOptions">(Optional) The options for the container request.</param>
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
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units">Request Units</seealso>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/set-throughput#set-throughput-on-a-database">Set throughput on a database</seealso>
        public abstract Task<int?> ReadThroughputAsync(
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Gets container throughput in measurement of request units per second in the Azure Cosmos service.
        /// </summary>
        /// <param name="requestOptions">The options for the throughput request.</param>
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
        /// <param name="throughput">The Cosmos container throughput, expressed in Request Units per second.</param>
        /// <param name="requestOptions">(Optional) The options for the throughput request.</param>
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
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units">Request Units</seealso>
        public abstract Task<ThroughputResponse> ReplaceThroughputAsync(
            int throughput,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Sets throughput provisioned for a container in measurement of request units per second in the Azure Cosmos service.
        /// </summary>
        /// <param name="throughputProperties">The Cosmos container throughput expressed in Request Units per second.</param>
        /// <param name="requestOptions">(Optional) The options for the throughput request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The throughput response.</returns>
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
        /// </remarks>
        public abstract Task<ThroughputResponse> ReplaceThroughputAsync(
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

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
        /// <param name="partitionKey"><see cref="PartitionKey"/> for the item. If not specified will be populated by extracting from {T}</param>
        /// <param name="requestOptions">(Optional) The options for the item request.</param>
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
        /// <param name="id">The Cosmos item id</param>
        /// <param name="partitionKey">The partition key for the item.</param>
        /// <param name="requestOptions">(Optional) The options for the item request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="ResponseMessage"/> which wraps a <see cref="Stream"/> containing the read resource record.
        /// </returns>
        /// <remarks>
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
        /// <param name="partitionKey"><see cref="PartitionKey"/> for the item. If not specified will be populated by extracting from {T}</param>
        /// <param name="requestOptions">(Optional) The options for the item request.</param>
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
        /// Patches an item in the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <remarks>
        /// The item's partition key value is immutable. 
        /// To change an item's partition key value you must delete the original item and insert a new item.
        /// </remarks>
        /// <param name="id">The Cosmos item id of the item to be patched.</param>
        /// <param name="partitionKey"><see cref="PartitionKey"/> for the item</param>
        /// <param name="patchSpecification">Represents a list of operations to be sequentially applied to the referred Cosmos item.</param>
        /// <param name="requestOptions">(Optional) The options for the item request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="ItemResponse{T}"/> which wraps the updated resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="id"/>, <paramref name="partitionKey"/> or <paramref name="patchSpecification"/> is not set.</exception>
        /// <exception cref="CosmosException">
        /// This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property.
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest due to a malformed <paramref name="patchSpecification"/>.</description>
        ///     </item>
        ///     <item>
        ///         <term>404</term><description>Resource (Cosmos item referred to) not found.</description>
        ///     </item>
        ///     <item>
        ///         <term>412</term><description>When the specified If-Match precondition fails.</description>
        ///     </item>
        ///     <item>
        ///         <term>413</term><description>RequestEntityTooLarge - This means the item exceeds the current max entity size. Consult documentation for limits.</description>
        ///     </item>
        ///     <item>
        ///         <term>422</term><description>The patch document is valid, but the server is incapable of processing the request.</description>
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
        ///     public string description {get; set;}
        /// }
        /// 
        /// ToDoActivity test = new ToDoActivity()
        /// {
        ///    id = Guid.NewGuid().ToString(),
        ///    status = "InProgress",
        ///    description = "Test"
        /// };
        ///
        /// PatchSpecification patchSpecification = new PatchSpecification()
        ///                                             .Add("/foo", "bar")
        ///                                             .Remove("/description")
        ///                                             .Replace("/status", "Done");
        ///                                 
        /// ItemResponse item = await this.container.PatchItemAsync<ToDoActivity>(test.id, new PartitionKey(test.status), patchSpecification);
        /// ]]>
        /// </code>
        /// </example>
#if PREVIEW
        public
#else
        internal
#endif
            abstract Task<ItemResponse<T>> PatchItemAsync<T>(
                string id,
                PartitionKey partitionKey,
                PatchSpecification patchSpecification,
                ItemRequestOptions requestOptions = null,
                CancellationToken cancellationToken = default(CancellationToken));

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
        /// <remarks>
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
                    CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Delete a item from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="id">The Cosmos item id</param>
        /// <param name="partitionKey">The partition key for the item.</param>
        /// <param name="requestOptions">(Optional) The options for the item request.</param>
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
        /// <param name="queryDefinition">The Cosmos SQL query definition.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request.</param>
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
        /// <param name="queryDefinition">The Cosmos SQL query definition.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request.</param>
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
        /// <param name="queryText">The Cosmos SQL query text.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request.</param>
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
        /// <param name="queryText">The Cosmos SQL query text.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request.</param>
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
        /// <param name="requestOptions">(Optional) The options for the item query request.</param>
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
        /// The Azure Cosmos DB LINQ provider compiles LINQ to SQL statements. Refer to https://docs.microsoft.com/azure/cosmos-db/sql-query-linq-to-sql for the list of expressions supported by the Azure Cosmos DB LINQ provider. ToString() on the generated IQueryable returns the translated SQL statement. The Azure Cosmos DB provider translates JSON.NET and DataContract serialization attributes for members to their JSON property names.
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

        /// <summary>
        /// Initializes a new instance of <see cref="TransactionalBatch"/>
        /// that can be used to perform operations across multiple items
        /// in the container with the provided partition key in a transactional manner.
        /// </summary>
        /// <param name="partitionKey">The partition key for all items in the batch.</param>
        /// <returns>A new instance of <see cref="TransactionalBatch"/>.</returns>
        public abstract TransactionalBatch CreateTransactionalBatch(PartitionKey partitionKey);

#if PREVIEW
        /// <summary>
        /// Obtains a list of <see cref="FeedRange"/> that can be used to parallelize Feed operations.
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A list of <see cref="FeedRange"/>.</returns>
        public abstract Task<IReadOnlyList<FeedRange>> GetFeedRangesAsync(CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// This method creates an iterator to consume the container's Change Feed.
        /// </summary>
        /// <param name="continuationToken">(Optional) The continuation from a previous Change Feed iterator.</param>
        /// <param name="changeFeedRequestOptions">(Optional) The options for the Change Feed consumption.</param>
        /// <returns>An iterator to go through the Change Feed.</returns>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// FeedIterator feedIterator = this.Container.GetChangeFeedStreamIterator();
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
        public abstract FeedIterator GetChangeFeedStreamIterator(
            string continuationToken = null,
            ChangeFeedRequestOptions changeFeedRequestOptions = null);

        /// <summary>
        ///  This method creates an iterator to consume a FeedRange's Change Feed.
        /// </summary>
        /// <param name="feedRange">A FeedRange obtained from <see cref="Container.GetFeedRangesAsync(CancellationToken)"/> or from a previous FeedIterator</param>
        /// <param name="changeFeedRequestOptions">(Optional) The options for the Change Feed consumption.</param>
        /// <seealso cref="Container.GetFeedRangesAsync(CancellationToken)"/>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// IReadOnlyList<FeedRange> feedRanges = await this.Container.GetFeedRangesAsync();
        /// // Distribute feedRanges across multiple compute units and pass each one to a different iterator
        /// FeedIterator feedIterator = this.Container.GetChangeFeedStreamIterator(feedRanges[0]);
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
        /// <returns>An iterator to go through the Change Feed for a particular FeedRange.</returns>
        public abstract FeedIterator GetChangeFeedStreamIterator(
            FeedRange feedRange,
            ChangeFeedRequestOptions changeFeedRequestOptions = null);

        /// <summary>
        ///  This method creates an iterator to consume the Change Feed for a Partition Key value.
        /// </summary>
        /// <param name="partitionKey">A <see cref="PartitionKey"/> to read the Change Feed for.</param>
        /// <param name="changeFeedRequestOptions">(Optional) The options for the Change Feed consumption.</param>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// FeedIterator feedIterator = this.Container.GetChangeFeedStreamIterator(new PartitionKey("myPartitionKey"));
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
        /// <returns>An iterator to go through the Change Feed for a particular Partition Key.</returns>
        public abstract FeedIterator GetChangeFeedStreamIterator(
            PartitionKey partitionKey,
            ChangeFeedRequestOptions changeFeedRequestOptions = null);

        /// <summary>
        ///  This method creates an iterator to consume the container's Change Feed.
        /// </summary>
        /// <param name="continuationToken">(Optional) The continuation from a previous Change Feed iterator.</param>
        /// <param name="changeFeedRequestOptions">(Optional) The options for the Change Feed consumption.</param>
        /// <returns>An iterator to go through the Change Feed.</returns>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// FeedIterator<MyItem> feedIterator = this.Container.GetChangeFeedIterator<MyItem>();
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     FeedResponse<MyItem> response = await feedIterator.ReadNextAsync();
        ///     foreach (var item in response)
        ///     {
        ///         Console.WriteLine(item);
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract FeedIterator<T> GetChangeFeedIterator<T>(
            string continuationToken = null,
            ChangeFeedRequestOptions changeFeedRequestOptions = null);

        /// <summary>
        ///  This method creates an iterator to consume a FeedRange's Change Feed.
        /// </summary>
        /// <param name="feedRange">A FeedRange obtained from <see cref="Container.GetFeedRangesAsync(CancellationToken)"/>.</param>
        /// <param name="changeFeedRequestOptions">(Optional) The options for the Change Feed consumption.</param>
        /// <seealso cref="Container.GetFeedRangesAsync(CancellationToken)"/>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// IReadOnlyList<FeedRange> feedRanges = await this.Container.GetFeedRangessAsync();
        /// // Distribute feedRangess across multiple compute units and pass each one to a different iterator
        /// FeedIterator<MyItem> feedIterator = this.Container.GetChangeFeedIterator<MyItem>(feedRanges[0]);
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     FeedResponse<MyItem> response = await feedIterator.ReadNextAsync();
        ///     foreach (var item in response)
        ///     {
        ///         Console.WriteLine(item);
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <returns>An iterator to go through the Change Feed for a particular FeedRange.</returns>
        public abstract FeedIterator<T> GetChangeFeedIterator<T>(
            FeedRange feedRange,
            ChangeFeedRequestOptions changeFeedRequestOptions = null);

        /// <summary>
        ///  This method creates an iterator to consume the Change Feed for a Partition Key value.
        /// </summary>
        /// <param name="partitionKey">A <see cref="PartitionKey"/> to read the Change Feed for.</param>
        /// <param name="changeFeedRequestOptions">(Optional) The options for the Change Feed consumption.</param>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// FeedIterator<MyItem> feedIterator = this.Container.GetChangeFeedIterator<MyItem>(new PartitionKey("myPartitionKey"));
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     FeedResponse<MyItem> response = await feedIterator.ReadNextAsync();
        ///     foreach (var item in response)
        ///     {
        ///         Console.WriteLine(item);
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <returns>An iterator to go through the Change Feed for a Partition Key.</returns>
        public abstract FeedIterator<T> GetChangeFeedIterator<T>(
            PartitionKey partitionKey,
            ChangeFeedRequestOptions changeFeedRequestOptions = null);

        /// <summary>
        /// Gets the list of Partition Key Range identifiers for a <see cref="FeedRange"/>.
        /// </summary>
        /// <param name="feedRange">A <see cref="FeedRange"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The list of Partition Key Range identifiers affected by a particular FeedRange.</returns>
        /// <seealso cref="Container.GetFeedRangesAsync(CancellationToken)"/>
        public abstract Task<IEnumerable<string>> GetPartitionKeyRangesAsync(
            FeedRange feedRange,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        ///  This method creates a query for items under a container in an Azure Cosmos database using a SQL statement with parameterized values. It returns a FeedIterator.
        ///  For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/>.
        /// </summary>
        /// <param name="feedRange">A FeedRange obtained from <see cref="Container.GetFeedRangesAsync(CancellationToken)"/></param>
        /// <param name="queryDefinition">The Cosmos SQL query definition.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request.</param>
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
        /// IReadOnlyList<FeedRange> feedRanges = await this.Container.GetFeedRangesAsync();
        /// // Distribute feedRanges across multiple compute units and pass each one to a different iterator
        /// QueryDefinition queryDefinition = new QueryDefinition("select * from ToDos t where t.cost > @expensive")
        ///     .WithParameter("@expensive", 9000);
        /// FeedIterator feedIterator = this.Container.GetItemQueryStreamIterator(
        ///     feedRanges[0],
        ///     queryDefinition,
        ///     null,
        ///     new QueryRequestOptions() { });
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
        /// IReadOnlyList<FeedRange> feedRanges = await this.Container.GetFeedRangesAsync();
        /// // Distribute feedRanges across multiple compute units and pass each one to a different iterator
        /// QueryDefinition queryDefinition = new QueryDefinition("select * from ToDos t where t.cost > @expensive")
        ///     .WithParameter("@expensive", 9000);
        /// FeedIterator<ToDoActivity> feedIterator = this.Container.GetItemQueryIterator<ToDoActivity>(
        ///     feedRanges[0],
        ///     queryDefinition,
        ///     null,
        ///     new QueryRequestOptions() { });
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
            FeedRange feedRange,
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);
#endif
    }
}