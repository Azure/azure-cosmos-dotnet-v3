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
    /// <see cref="CosmosDatabase"/> for creating new containers, and reading/querying all containers;
    /// </summary>
    /// <remarks>
    ///  Note: all these operations make calls against a fixed budget.
    ///  You should design your system such that these calls scale sub linearly with your application.
    ///  For instance, do not call `cosmosContainer(id).read()` before every single `item.read()` call, to ensure the cosmosContainer exists;
    ///  do this once on application start up.
    /// </remarks>
    public abstract partial class CosmosContainer
    {
        /// <summary>
        /// The Id of the Cosmos container
        /// </summary>
        public abstract string Id { get; }

        /// <summary>
        /// Returns the parent database reference
        /// </summary>
        public abstract CosmosDatabase Database { get; }

        /// <summary>
        /// Operations for reading/querying all conflicts
        /// </summary>
        /// <returns>An instance of <see cref="CosmosConflicts"/> to do operations on Conflicts.</returns>
        public abstract CosmosConflicts GetConflicts();

        /// <summary>
        /// Reads a <see cref="CosmosContainerProperties"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the container request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="ContainerResponse"/> which wraps a <see cref="CosmosContainerProperties"/> containing the read resource record.
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
        /// CosmosContainer cosmosContainer = this.database.GetContainer("containerId");
        /// CosmosContainerProperties containerProperties = cosmosContainer.ReadAsync();
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<ContainerResponse> ReadAsync(
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Replace a <see cref="CosmosContainerProperties"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="containerProperties">The <see cref="CosmosContainerProperties"/> object.</param>
        /// <param name="requestOptions">(Optional) The options for the container request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="ContainerResponse"/> which wraps a <see cref="CosmosContainerProperties"/> containing the replace resource record.
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
        /// Update the cosmosContainer to disable automatic indexing
        /// <code language="c#">
        /// <![CDATA[
        /// ContainerProperties containerProperties = containerReadResponse;
        /// setting.IndexingPolicy.Automatic = false;
        /// ContainerResponse response = cosmosContainer.ReplaceAsync(containerProperties);
        /// ContainerProperties replacedProperties = response;
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<ContainerResponse> ReplaceAsync(
            CosmosContainerProperties containerProperties,
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Delete a <see cref="CosmosContainerProperties"/> from the Azure Cosmos DB service as an asynchronous operation.
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
        /// CosmosContainer cosmosContainer = this.database.Containers["containerId"];
        /// ContainerResponse response = cosmosContainer.DeleteAsync();
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<ContainerResponse> DeleteAsync(
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Gets provisioned container throughput in measurement of Requests-per-Unit in the Azure Cosmos service.
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <value>
        /// The provisioned throughput for this database.
        /// </value>
        /// <remarks>
        /// <para>
        /// Refer to http://azure.microsoft.com/documentation/articles/documentdb-performance-levels/ for details on provision offer throughput.
        /// </para>
        /// </remarks>
        /// <example>
        /// The following example shows how to get the throughput.
        /// <code language="c#">
        /// <![CDATA[
        /// int? throughput = await this.cosmosContainer.ReadProvisionedThroughputAsync();
        /// ]]>
        /// </code>
        /// </example>
        /// <returns>The value of the provisioned throughput if any</returns>
        internal abstract Task<int?> ReadProvisionedThroughputAsync(
            CancellationToken cancellationToken = default(CancellationToken));
            
        /// <summary>
        /// Sets throughput provisioned for a container in measurement of Requests-per-Unit in the Azure Cosmos service.
        /// </summary>
        /// <param name="requestUnitsPerSecond">The cosmos container throughput, expressed in Request Units per second.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <value>
        /// The provisioned throughput for this container.
        /// </value>
        /// <example>
        /// The following example shows how to get the throughput.
        /// <code language="c#">
        /// <![CDATA[
        /// int? throughput = await this.cosmosContainer.ReplaceProvisionedThroughputAsync(400);
        /// ]]>
        /// </code>
        /// </example>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <remarks>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units"/> for details on provision throughput.
        /// </remarks>
        internal abstract Task ReplaceProvisionedThroughputAsync(
            int requestUnitsPerSecond,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Gets container throughput in measurement of Requests-per-Unit in the Azure Cosmos service.
        /// </summary>
        /// <param name="options">(Optional) The options for the throughput request.<see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <value>
        /// The provisioned throughput for this container.
        /// </value>
        /// <remarks>
        /// <para>
        /// Refer to http://azure.microsoft.com/documentation/articles/documentdb-performance-levels/ for details on provision offer throughput.
        /// </para>
        /// </remarks>
        /// <example>
        /// The following example shows how to get the throughput.
        /// <code language="c#">
        /// <![CDATA[
        /// int? throughput = await this.cosmosContainer.ReadProvisionedThroughputAsync();
        /// ]]>
        /// </code>
        /// </example>
        /// <returns>The throughput response</returns>
        public abstract Task<ThroughputResponse> ReadProvisionedThroughputAsync(
            RequestOptions options,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Sets throughput provisioned for a container in measurement of Requests-per-Unit in the Azure Cosmos service.
        /// </summary>
        /// <param name="requestUnitsPerSecond">The cosmos container throughput, expressed in Request Units per second.</param>
        /// <param name="options">(Optional) The options for the throughput request.<see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <value>
        /// The provisioned throughput for this container.
        /// </value>
        /// <example>
        /// The following example shows how to get the throughput.
        /// <code language="c#">
        /// <![CDATA[
        /// int? throughput = await this.cosmosContainer.ReplaceProvisionedThroughputAsync(400);
        /// ]]>
        /// </code>
        /// </example>
        /// <returns>The throughput response.</returns>
        /// <remarks>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units"/> for details on provision throughput.
        /// </remarks>
        public abstract Task<ThroughputResponse> ReplaceProvisionedThroughputAsync(
            int requestUnitsPerSecond,
            RequestOptions options,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Reads a <see cref="CosmosContainerProperties"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the container request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="CosmosResponseMessage"/> containing the read resource record.
        /// </returns>
        public abstract Task<CosmosResponseMessage> ReadStreamAsync(
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Replace a <see cref="CosmosContainerProperties"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="containerProperties">The <see cref="CosmosContainerProperties"/>.</param>
        /// <param name="requestOptions">(Optional) The options for the container request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="CosmosResponseMessage"/> containing the replace resource record.
        /// </returns>
        public abstract Task<CosmosResponseMessage> ReplaceStreamAsync(
            CosmosContainerProperties containerProperties,
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Delete a <see cref="CosmosContainerProperties"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the container request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="CosmosResponseMessage"/> which will contain information about the request issued.</returns>
        public abstract Task<CosmosResponseMessage> DeleteStreamAsync(
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Creates a Item as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <param name="partitionKey">The partition key for the item. <see cref="Microsoft.Azure.Documents.PartitionKey"/></param>
        /// <param name="streamPayload">A <see cref="Stream"/> containing the payload.</param>
        /// <param name="requestOptions">(Optional) The options for the item request <see cref="ItemRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The <see cref="CosmosResponseMessage"/> that was created contained within a <see cref="System.Threading.Tasks.Task"/> object representing the service response for the asynchronous operation.</returns>
        /// <exception>
        /// The Stream operation only throws on client side exceptions. This is to increase performance and prevent the overhead of throwing exceptions. Check the HTTP status code on the response to check if the operation failed.
        /// </exception>
        /// <example>
        /// This example creates an item in a Cosmos container.
        /// <code language="c#">
        /// <![CDATA[
        /// //Create the object in Cosmos
        /// using (CosmosResponseMessage response = await this.Container.CreateItemStreamAsync(partitionKey: "streamPartitionKey", streamPayload: stream))
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
        public abstract Task<CosmosResponseMessage> CreateItemStreamAsync(
                    PartitionKey partitionKey,
                    Stream streamPayload,
                    ItemRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Creates a item as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <param name="item">A JSON serializable object that must contain an id property. <see cref="CosmosJsonSerializer"/> to implement a custom serializer</param>
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
        /// ItemResponse item = this.cosmosContainer.CreateItemAsync<ToDoActivity>(test.status, tests);
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<ItemResponse<T>> CreateItemAsync<T>(
            T item,
            PartitionKey partitionKey = null,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Reads a item from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="partitionKey">The partition key for the item. <see cref="Microsoft.Azure.Documents.PartitionKey"/></param>
        /// <param name="id">The cosmos item id</param>
        /// <param name="requestOptions">(Optional) The options for the item request <see cref="ItemRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="CosmosResponseMessage"/> which wraps a <see cref="Stream"/> containing the read resource record.
        /// </returns>
        /// <exception>
        /// The Stream operation only throws on client side exceptions. This is to increase performance and prevent the overhead of throwing exceptions. Check the HTTP status code on the response to check if the operation failed.
        /// </exception>
        /// <example>
        /// Read a response as a stream.
        /// <code language="c#">
        /// <![CDATA[
        /// using(CosmosResponseMessage response = this.cosmosContainer.ReadItemStreamAsync("partitionKey", "id"))
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
        public abstract Task<CosmosResponseMessage> ReadItemStreamAsync(
                    PartitionKey partitionKey,
                    string id,
                    ItemRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Reads a item from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="partitionKey">The partition key for the item. <see cref="Microsoft.Azure.Documents.PartitionKey"/></param>
        /// <param name="id">The cosmos item id</param>
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
        /// ToDoActivity toDoActivity = this.cosmosContainer.ReadItemAsync<ToDoActivity>("partitionKey", "id");
        /// 
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<ItemResponse<T>> ReadItemAsync<T>(
            PartitionKey partitionKey,
            string id,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Upserts an item stream as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <param name="partitionKey">The partition key for the item. <see cref="Microsoft.Azure.Documents.PartitionKey"/></param>
        /// <param name="streamPayload">A <see cref="Stream"/> containing the payload.</param>
        /// <param name="requestOptions">(Optional) The options for the item request <see cref="ItemRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="CosmosResponseMessage"/> which wraps a <see cref="Stream"/> containing the read resource record.
        /// </returns>
        /// <exception>
        /// The Stream operation only throws on client side exceptions. This is to increase performance and prevent the overhead of throwing exceptions. Check the HTTP status code on the response to check if the operation failed.
        /// </exception>
        /// <example>
        /// Upsert a Stream containing the item to Cosmos
        /// <code language="c#">
        /// <![CDATA[
        /// using(CosmosResponseMessage response = this.cosmosContainer.UpsertItemStreamAsync(partitionKey: "itemPartitionKey", streamPayload: stream))
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
        public abstract Task<CosmosResponseMessage> UpsertItemStreamAsync(
                    PartitionKey partitionKey,
                    Stream streamPayload,
                    ItemRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Upserts an item as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <param name="item">A JSON serializable object that must contain an id property. <see cref="CosmosJsonSerializer"/> to implement a custom serializer</param>
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
        /// ItemResponse<ToDoActivity> item = await this.cosmosContainer.UpsertAsync<ToDoActivity>(test.status, test);
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<ItemResponse<T>> UpsertItemAsync<T>(
            T item,
            PartitionKey partitionKey = null,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Replaces a item in the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="partitionKey">The partition key for the item. <see cref="Microsoft.Azure.Documents.PartitionKey"/></param>
        /// <param name="id">The cosmos item id</param>
        /// <param name="streamPayload">A <see cref="Stream"/> containing the payload.</param>
        /// <param name="requestOptions">(Optional) The options for the item request <see cref="ItemRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="CosmosResponseMessage"/> which wraps a <see cref="Stream"/> containing the replace resource record.
        /// </returns>
        /// <exception>
        /// The Stream operation only throws on client side exceptions. This is to increase performance and prevent the overhead of throwing exceptions. Check the HTTP status code on the response to check if the operation failed.
        /// </exception>
        /// <example>
        /// Replace an item in Cosmos
        /// <code language="c#">
        /// <![CDATA[
        /// using(CosmosResponseMessage response = this.cosmosContainer.ReplaceItemStreamAsync(partitionKey: "itemPartitionKey", id: "itemId", streamPayload: stream))
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
        public abstract Task<CosmosResponseMessage> ReplaceItemStreamAsync(
                    PartitionKey partitionKey,
                    string id,
                    Stream streamPayload,
                    ItemRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Replaces a item in the Azure Cosmos service as an asynchronous operation.
        /// </summary>        
        /// <param name="id">The cosmos item id, which is expected to match the value within T.</param>
        /// <param name="item">A JSON serializable object that must contain an id property. <see cref="CosmosJsonSerializer"/> to implement a custom serializer.</param>
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
        /// ItemResponse item = await this.cosmosContainer.ReplaceItemAsync<ToDoActivity>(test.status, test.id, test);
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<ItemResponse<T>> ReplaceItemAsync<T>(
            string id,
            T item,
            PartitionKey partitionKey = null,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Delete a item from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="partitionKey">The partition key for the item. <see cref="Microsoft.Azure.Documents.PartitionKey"/></param>
        /// <param name="id">The cosmos item id</param>
        /// <param name="requestOptions">(Optional) The options for the item request <see cref="ItemRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="CosmosResponseMessage"/> which wraps a <see cref="Stream"/> containing the delete resource record.
        /// </returns>
        /// <exception>
        /// The Stream operation only throws on client side exceptions. This is to increase performance and prevent the overhead of throwing exceptions. Check the HTTP status code on the response to check if the operation failed.
        /// </exception>
        /// <example>
        /// Delete an item from Cosmos
        /// <code language="c#">
        /// <![CDATA[
        /// using(CosmosResponseMessage response = this.cosmosContainer.DeleteItemStreamAsync(partitionKey: "itemPartitionKey", id: "itemId"))
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
        public abstract Task<CosmosResponseMessage> DeleteItemStreamAsync(
                    PartitionKey partitionKey,
                    string id,
                    ItemRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Delete a item from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="partitionKey">The partition key for the item. <see cref="Microsoft.Azure.Documents.PartitionKey"/></param>
        /// <param name="id">The cosmos item id</param>
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
        /// ItemResponse item = await this.cosmosContainer.DeleteItemAsync<ToDoActivity>("partitionKey", "id");
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<ItemResponse<T>> DeleteItemAsync<T>(
            PartitionKey partitionKey,
            string id,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

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
        /// FeedIterator<ToDoActivity> feedIterator = this.cosmosContainer.GetItemsIterator<ToDoActivity>();
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     foreach(ToDoActivity item in await feedIterator.FetchNextSetAsync())
        ///     {
        ///          Console.WriteLine(item.id); 
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <returns>An iterator to go through the items.</returns>
        public abstract FeedIterator<T> GetItemsIterator<T>(
            int? maxItemCount = null,
            string continuationToken = null);

        /// <summary>
        /// Gets an iterator to go through all the items for the container as the original CosmosResponseMessage
        /// </summary>
        /// <param name="maxItemCount">(Optional) The max item count to return as part of the query</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request <see cref="QueryRequestOptions"/></param>
        /// <example>
        /// Get an iterator for all the items under the cosmos container
        /// <code language="c#">
        /// <![CDATA[
        /// public class ToDoActivity{
        ///     public string id {get; set;}
        ///     public string status {get; set;}
        /// }
        /// 
        /// FeedIterator feedIterator = this.Container.Items.GetItemsStreamIterator();
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     using (CosmosResponseMessage iterator = await feedIterator.FetchNextSetAsync())
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
        /// <returns>An iterator to go through the items.</returns>
        public abstract FeedIterator GetItemsStreamIterator(
            int? maxItemCount = null,
            string continuationToken = null,
            ItemRequestOptions requestOptions = null);

        /// <summary>
        ///  This method creates a query for items under a container in an Azure Cosmos database using a SQL statement with parameterized values. It returns a CosmosResultSetStreamIterator.
        ///  For more information on preparing SQL statements with parameterized values, please see <see cref="CosmosSqlQueryDefinition"/>.
        /// </summary>
        /// <param name="sqlQueryDefinition">The cosmos SQL query definition.</param>
        /// <param name="maxConcurrency">The number of concurrent operations run client side during parallel query execution in the Azure Cosmos DB service.</param>
        /// <param name="partitionKey">The partition key for the item. <see cref="Microsoft.Azure.Documents.PartitionKey"/></param>
        /// <param name="maxItemCount">(Optional) The max item count to return as part of the query</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request <see cref="QueryRequestOptions"/></param>
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
        /// FeedIterator feedIterator = this.Container.CreateItemQueryStream(
        ///     sqlQueryDefinition: sqlQuery, 
        ///     partitionKey: "Error");
        ///     
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     using (CosmosResponseMessage response = await feedIterator.FetchNextSetAsync())
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
        /// <returns>An iterator to go through the items.</returns>
        public abstract FeedIterator CreateItemQueryStream(
            CosmosSqlQueryDefinition sqlQueryDefinition,
            int maxConcurrency,
            PartitionKey partitionKey = null,
            int? maxItemCount = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);

        /// <summary>
        ///  This method creates a query for items under a container in an Azure Cosmos database using a SQL statement with parameterized values. It returns a CosmosResultSetStreamIterator.
        ///  For more information on preparing SQL statements with parameterized values, please see <see cref="CosmosSqlQueryDefinition"/>.
        /// </summary>
        /// <param name="sqlQueryText">The cosmos SQL query string.</param>
        /// <param name="maxConcurrency">The number of concurrent operations run client side during parallel query execution in the Azure Cosmos DB service.</param>
        /// <param name="partitionKey">The partition key for the item. <see cref="Microsoft.Azure.Documents.PartitionKey"/></param>
        /// <param name="maxItemCount">(Optional) The max item count to return as part of the query</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request <see cref="QueryRequestOptions"/></param>
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
        /// FeedIterator feedIterator = this.Container.CreateItemQueryStream(
        ///     sqlQueryText: "select * from ToDos t where t.cost > 9000", 
        ///     partitionKey: "Error");
        ///     
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     using (CosmosResponseMessage response = await feedIterator.FetchNextSetAsync())
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
        /// <returns>An iterator to go through the items.</returns>
        public abstract FeedIterator CreateItemQueryStream(
            string sqlQueryText,
            int maxConcurrency,
            PartitionKey partitionKey = null,
            int? maxItemCount = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);

        /// <summary>
        ///  This method creates a query for items under a container in an Azure Cosmos database using a SQL statement with parameterized values. It returns a FeedIterator.
        ///  For more information on preparing SQL statements with parameterized values, please see <see cref="CosmosSqlQueryDefinition"/>.
        /// </summary>
        /// <param name="sqlQueryDefinition">The cosmos SQL query definition.</param>
        /// <param name="partitionKey">The partition key for the item. <see cref="Microsoft.Azure.Documents.PartitionKey"/></param>
        /// <param name="maxItemCount">(Optional) The max item count to return as part of the query</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request <see cref="QueryRequestOptions"/></param>
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
        /// FeedIterator<ToDoActivity> feedIterator = this.Container.CreateItemQuery<ToDoActivity>(
        ///     sqlQueryDefinition: sqlQuery, 
        ///     partitionKey: "Error");
        ///     
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     foreach(var item in await feedIterator.FetchNextSetAsync()){
        ///     {
        ///         Console.WriteLine(item.cost); 
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <returns>An iterator to go through the items.</returns>
        public abstract FeedIterator<T> CreateItemQuery<T>(
            CosmosSqlQueryDefinition sqlQueryDefinition,
            PartitionKey partitionKey,
            int? maxItemCount = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);

        /// <summary>
        ///  This method creates a query for items under a container in an Azure Cosmos database using a SQL statement with parameterized values. It returns a FeedIterator.
        ///  For more information on preparing SQL statements with parameterized values, please see <see cref="CosmosSqlQueryDefinition"/>.
        /// </summary>
        /// <param name="sqlQueryText">The cosmos SQL query text.</param>
        /// <param name="partitionKey">The partition key for the item. <see cref="Microsoft.Azure.Documents.PartitionKey"/></param>
        /// <param name="maxItemCount">(Optional) The max item count to return as part of the query</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request <see cref="QueryRequestOptions"/></param>
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
        /// FeedIterator<ToDoActivity> feedIterator = this.Container.CreateItemQuery<ToDoActivity>(
        ///     sqlQueryText: "select * from ToDos t where t.cost > 9000", 
        ///     partitionKey: "Error");
        ///     
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     foreach(var item in await feedIterator.FetchNextSetAsync()){
        ///     {
        ///         Console.WriteLine(item.cost); 
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <returns>An iterator to go through the items.</returns>
        public abstract FeedIterator<T> CreateItemQuery<T>(
            string sqlQueryText,
            PartitionKey partitionKey,
            int? maxItemCount = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);

        /// <summary>
        ///  This method creates a query for items under a container in an Azure Cosmos database using a SQL statement with parameterized values. It returns a FeedIterator.
        ///  For more information on preparing SQL statements with parameterized values, please see <see cref="CosmosSqlQueryDefinition"/>.
        /// </summary>
        /// <param name="sqlQueryDefinition">The cosmos SQL query definition.</param>
        /// <param name="maxConcurrency">The number of concurrent operations run client side during parallel query execution in the Azure Cosmos DB service.</param>
        /// <param name="maxItemCount">(Optional) The max item count to return as part of the query</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request <see cref="QueryRequestOptions"/></param>
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
        /// FeedIterator<ToDoActivity> feedIterator = this.Container.CreateItemQuery<ToDoActivity>(
        ///     sqlQuery,
        ///     maxConcurrency: 2);
        ///     
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     foreach(var item in await feedIterator.FetchNextSetAsync()){
        ///     {
        ///         Console.WriteLine(item.cost); 
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <returns>An iterator to go through the items.</returns>
        public abstract FeedIterator<T> CreateItemQuery<T>(
            CosmosSqlQueryDefinition sqlQueryDefinition,
            int maxConcurrency,
            int? maxItemCount = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);

        /// <summary>
        ///  This method creates a query for items under a container in an Azure Cosmos database using a SQL statement with parameterized values. It returns a FeedIterator.
        ///  For more information on preparing SQL statements with parameterized values, please see <see cref="CosmosSqlQueryDefinition"/>.
        /// </summary>
        /// <param name="sqlQueryText">The cosmos SQL query text.</param>
        /// <param name="maxConcurrency">The number of concurrent operations run client side during parallel query execution in the Azure Cosmos DB service.</param>
        /// <param name="maxItemCount">(Optional) The max item count to return as part of the query</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request <see cref="QueryRequestOptions"/></param>
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
        /// FeedIterator<ToDoActivity> feedIterator = this.Container.CreateItemQuery<ToDoActivity>(
        ///     "select * from ToDos t where t.cost > 9000",
        ///     maxConcurrency: 2);
        ///     
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     foreach(var item in await feedIterator.FetchNextSetAsync()){
        ///     {
        ///         Console.WriteLine(item.cost); 
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <returns>An iterator to go through the items.</returns>
        public abstract FeedIterator<T> CreateItemQuery<T>(
            string sqlQueryText,
            int maxConcurrency,
            int? maxItemCount = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);

        /// <summary>
        /// This method creates a LINQ query for items under a container in an Azure Cosmos DB service.
        /// IQueryable extension method ToSqlQueryText() should be use for asynchronous execution with FeedIterator, please refer to example 2.
        /// </summary>
        /// <typeparam name="T">The type of object to query.</typeparam>
        /// <param name="partitionKey">(Optional) The partition key to execute the query in a particular partition.</param>
        /// <param name="allowSynchronousQueryExecution">(Optional)the option which allows the query to be executed synchronously via IOrderedQueryable.</param>
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
        /// Book book = container.Items.CreateItemQuery<Book>(allowSynchronousQueryExecution = true)
        ///                      .Where(b => b.Title == "War and Peace")
        ///                      .AsEnumerable()
        ///                      .FirstOrDefault();
        /// 
        /// // Query a nested property
        /// Book otherBook = container.Items.CreateItemQuery<Book>(allowSynchronousQueryExecution = true)
        ///                           .Where(b => b.Author.FirstName == "Leo")
        ///                           .AsEnumerable()
        ///                           .FirstOrDefault();
        /// 
        /// // Perform iteration on books
        /// foreach (Book matchingBook in container.Items.CreateItemQuery<Book>(allowSynchronousQueryExecution = true).Where(b => b.Price > 100))
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
        /// IQueryable<Book> queryable = container.Items.CreateItemQuery<Book>()
        ///                      .Where(b => b.Title == "War and Peace");
        /// //Asynchronous query execution
        /// string sqlQueryText = queriable.ToSqlQueryText();
        /// FeedIterator<Book> setIterator = this.Container
        ///           .CreateItemQuery<Book>(sqlQueryText, maxConcurrency: 1);
        ///           while (setIterator.HasMoreResults)
        ///           {
        ///           FeedResponse<Book> queryResponse = await setIterator.FetchNextSetAsync();
        ///            var iter = queryResponse.GetEnumerator();
        ///            while (iter.MoveNext())
        ///            {
        ///             Book book = iter.Current;
        ///            }
        ///            }
        ///
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// The Azure Cosmos DB LINQ provider compiles LINQ to SQL statements. Refer to http://azure.microsoft.com/documentation/articles/documentdb-sql-query/#linq-to-documentdb-sql for the list of expressions supported by the Azure Cosmos DB LINQ provider. ToString() on the generated IQueryable returns the translated SQL statement. The Azure Cosmos DB provider translates JSON.NET and DataContract serialization attributes for members to their JSON property names.
        /// </remarks>
        public abstract IOrderedQueryable<T> CreateItemQuery<T>(object partitionKey = null, bool allowSynchronousQueryExecution = false, QueryRequestOptions requestOptions = null);

        /// <summary>
        /// Initializes a <see cref="ChangeFeedProcessorBuilder"/> for change feed processing.
        /// </summary>
        /// <param name="processorName">A name that identifies the Processor and the particular work it will do.</param>
        /// <param name="onChangesDelegate">Delegate to receive changes.</param>
        /// <returns>An instance of <see cref="ChangeFeedProcessorBuilder"/></returns>
        public abstract ChangeFeedProcessorBuilder CreateChangeFeedProcessorBuilder<T>(
            string processorName,
            Func<IReadOnlyCollection<T>, CancellationToken, Task> onChangesDelegate);

        /// <summary>
        /// Initializes a <see cref="ChangeFeedProcessorBuilder"/> for change feed monitoring.
        /// </summary>
        /// <param name="processorName">The name of the Processor the Estimator is going to measure.</param>
        /// <param name="estimationDelegate">Delegate to receive estimation.</param>
        /// <param name="estimationPeriod">Time interval on which to report the estimation.</param>
        /// <remarks>
        /// The goal of the Estimator is to measure progress of a particular processor. In order to do that, the <paramref name="processorName"/> and other parameters, like the leases container, need to match that of the Processor to measure.
        /// </remarks>
        /// <returns>An instance of <see cref="ChangeFeedProcessorBuilder"/></returns>
        public abstract ChangeFeedProcessorBuilder CreateChangeFeedEstimatorBuilder(
            string processorName,
            Func<long, CancellationToken, Task> estimationDelegate,
            TimeSpan? estimationPeriod = null);
    }
}