//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a batch of operations against items with the same <see cref="PartitionKey"/> in a container that
    /// will be performed in a transactional manner at the Azure Cosmos DB service.
    /// Use <see cref="Container.CreateTransactionalBatch(PartitionKey)"/> to create an instance of TransactionalBatch.
    /// </summary>
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
    /// ToDoActivity test4 = new ToDoActivity()
    /// {
    ///     type = activityType,
    ///     id = "running",
    ///     status = "ToBeDone"
    /// };
    ///
    /// List<PatchOperation> patchOperations = new List<PatchOperation>();
    /// patchOperations.Add(PatchOperation.Replace("/status", "InProgress");
    /// patchOperations.Add(PatchOperation.Add("/progress", 50);
    /// 
    /// using (TransactionalBatchResponse batchResponse = await container.CreateTransactionalBatch(new Cosmos.PartitionKey(activityType))
    ///     .CreateItem<ToDoActivity>(test1)
    ///     .ReplaceItem<ToDoActivity>(test2.id, test2)
    ///     .UpsertItem<ToDoActivity>(test3)
    ///     .PatchItem(test4.id, patchOperations)
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
    ///    TransactionalBatchOperationResult<ToDoActivity> replaceResult = batchResponse.GetOperationResultAtIndex<ToDoActivity>(0);
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
    /// using (TransactionalBatchResponse batchResponse = await container.CreateTransactionalBatch(new Cosmos.PartitionKey(activityType))
    ///    .ReadItem("playing")
    ///    .ReadItem("walking")
    ///    .ReadItem("jogging")
    ///    .ReadItem("running")
    ///    .ExecuteAsync())
    /// {
    ///     // Look up interested results - eg. via direct access to operation result stream
    ///     List<string> resultItems = new List<string>();
    ///     foreach (TransactionalBatchOperationResult operationResult in batchResponse)
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
    /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/concepts-limits">Limits on TransactionalBatch requests</seealso>
    public abstract class TransactionalBatch
    {
        /// <summary>
        /// Adds an operation to create an item into the batch.
        /// </summary>
        /// <param name="item">A JSON serializable object that must contain an id property. See <see cref="CosmosSerializer"/> to implement a custom serializer.</param>
        /// <param name="requestOptions">(Optional) The options for the item request.</param>
        /// <returns>The transactional batch instance with the operation added.</returns>
        /// <typeparam name="T">The type of item to be created.</typeparam>
        public abstract TransactionalBatch CreateItem<T>(
            T item,
            TransactionalBatchItemRequestOptions requestOptions = null);

        /// <summary>
        /// Adds an operation to create an item into the batch.
        /// </summary>
        /// <param name="streamPayload">
        /// A Stream containing the payload of the item.
        /// The stream must have a UTF-8 encoded JSON object which contains an id property.
        /// </param>
        /// <param name="requestOptions">(Optional) The options for the item request.</param>
        /// <returns>The transactional batch instance with the operation added.</returns>
        public abstract TransactionalBatch CreateItemStream(
            Stream streamPayload,
            TransactionalBatchItemRequestOptions requestOptions = null);

        /// <summary>
        /// Adds an operation to read an item into the batch.
        /// </summary>
        /// <param name="id">The unique id of the item.</param>
        /// <param name="requestOptions">(Optional) The options for the item request.</param>
        /// <returns>The transactional batch instance with the operation added.</returns>
        public abstract TransactionalBatch ReadItem(
            string id,
            TransactionalBatchItemRequestOptions requestOptions = null);

        /// <summary>
        /// Adds an operation to upsert an item into the batch.
        /// </summary>
        /// <param name="item">A JSON serializable object that must contain an id property. See <see cref="CosmosSerializer"/> to implement a custom serializer.</param>
        /// <param name="requestOptions">(Optional) The options for the item request.</param>
        /// <returns>The transactional batch instance with the operation added.</returns>
        /// <typeparam name="T">The type of item to be created.</typeparam>
        public abstract TransactionalBatch UpsertItem<T>(
            T item,
            TransactionalBatchItemRequestOptions requestOptions = null);

        /// <summary>
        /// Adds an operation to upsert an item into the batch.
        /// </summary>
        /// <param name="streamPayload">
        /// A Stream containing the payload of the item.
        /// The stream must have a UTF-8 encoded JSON object which contains an id property.
        /// </param>
        /// <param name="requestOptions">(Optional) The options for the item request.</param>
        /// <returns>The transactional batch instance with the operation added.</returns>
        public abstract TransactionalBatch UpsertItemStream(
            Stream streamPayload,
            TransactionalBatchItemRequestOptions requestOptions = null);

        /// <summary>
        /// Adds an operation to replace an item into the batch.
        /// </summary>
        /// <param name="id">The unique id of the item.</param>
        /// <param name="item">A JSON serializable object that must contain an id property. See <see cref="CosmosSerializer"/> to implement a custom serializer.</param>
        /// <param name="requestOptions">(Optional) The options for the item request.</param>
        /// <returns>The transactional batch instance with the operation added.</returns>
        /// <typeparam name="T">The type of item to be created.</typeparam>
        public abstract TransactionalBatch ReplaceItem<T>(
            string id,
            T item,
            TransactionalBatchItemRequestOptions requestOptions = null);

        /// <summary>
        /// Adds an operation to replace an item into the batch.
        /// </summary>
        /// <param name="id">The unique id of the item.</param>
        /// <param name="streamPayload">
        /// A Stream containing the payload of the item.
        /// The stream must have a UTF-8 encoded JSON object which contains an id property.
        /// </param>
        /// <param name="requestOptions">(Optional) The options for the item request.</param>
        /// <returns>The transactional batch instance with the operation added.</returns>
        public abstract TransactionalBatch ReplaceItemStream(
            string id,
            Stream streamPayload,
            TransactionalBatchItemRequestOptions requestOptions = null);

        /// <summary>
        /// Adds an operation to delete an item into the batch.
        /// </summary>
        /// <param name="id">The unique id of the item.</param>
        /// <param name="requestOptions">(Optional) The options for the item request.</param>
        /// <returns>The transactional batch instance with the operation added.</returns>
        public abstract TransactionalBatch DeleteItem(
            string id,
            TransactionalBatchItemRequestOptions requestOptions = null);

#if PREVIEW
        /// <summary>
        /// Adds an operation to patch an item into the batch.
        /// </summary>
        /// <param name="id">The unique id of the item.</param>
        /// <param name="patchOperations">Represents a list of operations to be sequentially applied to the referred Cosmos item.</param>
        /// <param name="requestOptions">(Optional) The options for the item request.</param>
        /// <returns>The transactional batch instance with the operation added.</returns>
        public abstract TransactionalBatch PatchItem(
                string id,
                System.Collections.Generic.IReadOnlyList<PatchOperation> patchOperations,
                TransactionalBatchPatchItemRequestOptions requestOptions = null);
#endif

        /// <summary>
        /// Executes the transactional batch at the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="cancellationToken">(Optional) Cancellation token representing request cancellation.</param>
        /// <returns>An awaitable response which contains details of execution of the transactional batch.
        /// <para>
        /// If the transactional batch executes successfully, the <see cref="TransactionalBatchResponse.StatusCode"/> on the response returned
        /// will be set to <see cref="HttpStatusCode.OK"/>.
        /// </para>
        /// <para>
        /// If an operation within the transactional batch fails during execution, no changes from the batch will be committed
        /// and the status of the failing operation is made available in the <see cref="TransactionalBatchResponse.StatusCode"/>.
        /// To get more details about the operation that failed, the response can be enumerated - this returns <see cref="TransactionalBatchOperationResult" />
        /// instances corresponding to each operation in the transactional batch in the order they were added into the transactional batch.
        /// For a result corresponding to an operation within the transactional batch, the <see cref="TransactionalBatchOperationResult.StatusCode"/> indicates
        /// the status of the operation - if the operation was not executed or it was aborted due to the failure of another operation within the transactional batch,
        /// the value of this field will be HTTP 424 (Failed Dependency); for the operation that caused the batch to abort, the value of this field will indicate
        /// the cause of failure as a HTTP status code.
        /// </para>
        /// <para>
        /// The <see cref="TransactionalBatchResponse.StatusCode"/> on the response returned may also have values such as HTTP 5xx in case of server errors and HTTP 429 (Too Many Requests).
        /// </para>
        /// </returns>
        /// <remarks>
        /// This API only throws on client side exceptions. This is to increase performance and prevent the overhead of throwing exceptions.
        /// Use <see cref="TransactionalBatchResponse.IsSuccessStatusCode"/> on the response returned to ensure that the transactional batch succeeded.
        /// </remarks>
        public abstract Task<TransactionalBatchResponse> ExecuteAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes the transactional batch at the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">Options that apply specifically to batch request.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token representing request cancellation.</param>
        /// <returns>An awaitable response which contains details of execution of the transactional batch.
        /// <para>
        /// If the transactional batch executes successfully, the <see cref="TransactionalBatchResponse.StatusCode"/> on the response returned
        /// will be set to <see cref="HttpStatusCode.OK"/>.
        /// </para>
        /// <para>
        /// If an operation within the transactional batch fails during execution, no changes from the batch will be committed
        /// and the status of the failing operation is made available in the <see cref="TransactionalBatchResponse.StatusCode"/>.
        /// To get more details about the operation that failed, the response can be enumerated - this returns <see cref="TransactionalBatchOperationResult" />
        /// instances corresponding to each operation in the transactional batch in the order they were added into the transactional batch.
        /// For a result corresponding to an operation within the transactional batch, the <see cref="TransactionalBatchOperationResult.StatusCode"/> indicates
        /// the status of the operation - if the operation was not executed or it was aborted due to the failure of another operation within the transactional batch,
        /// the value of this field will be HTTP 424 (Failed Dependency); for the operation that caused the batch to abort, the value of this field will indicate
        /// the cause of failure as a HTTP status code.
        /// </para>
        /// <para>
        /// The <see cref="TransactionalBatchResponse.StatusCode"/> on the response returned may also have values such as HTTP 5xx in case of server errors and HTTP 429 (Too Many Requests).
        /// </para>
        /// </returns>
        /// <remarks>
        /// This API only throws on client side exceptions. This is to increase performance and prevent the overhead of throwing exceptions.
        /// Use <see cref="TransactionalBatchResponse.IsSuccessStatusCode"/> on the response returned to ensure that the transactional batch succeeded.
        /// </remarks>
        public abstract Task<TransactionalBatchResponse> ExecuteAsync(
           TransactionalBatchRequestOptions requestOptions,
           CancellationToken cancellationToken = default);
    }
}
