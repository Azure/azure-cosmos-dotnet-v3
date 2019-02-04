//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Routing;

    /// <summary>
    /// Operations for reading, replacing, or deleting a specific, existing cosmosContainer by id.
    /// 
    /// <see cref="CosmosContainers"/> for creating new containers, and reading/querying all containers;
    /// </summary>
    /// <remarks>
    ///  Note: all these operations make calls against a fixed budget.
    ///  You should design your system such that these calls scale sub linearly with your application.
    ///  For instance, do not call `cosmosContainer(id).read()` before every single `item.read()` call, to ensure the cosmosContainer exists;
    ///  do this once on application start up.
    /// </remarks>
    public class CosmosContainer : CosmosIdentifier
    {
        /// <summary>
        /// Create a <see cref="CosmosContainer"/>
        /// </summary>
        /// <param name="database">The <see cref="CosmosDatabase"/></param>
        /// <param name="containerId">The cosmos container id.</param>
        /// <remarks>
        /// Note that the container must be explicitly created, if it does not already exist, before
        /// you can read from it or write to it.
        /// </remarks>
        protected internal CosmosContainer(
            CosmosDatabase database,
            string containerId) :
            base(database.Client,
                database.Link,
                containerId)
        {
            this.Database = database;
            this.Items = new CosmosItems(this);
            this.StoredProcedures = new CosmosStoredProcedures(this);
            this.DocumentClient = this.Database.Client.DocumentClient;
            this.Triggers = new CosmosTriggers(this);
            this.UserDefinedFunctions = new CosmosUserDefinedFunctions(this);
        }

        /// <summary>
        /// Returns the parent database reference
        /// </summary>
        public virtual CosmosDatabase Database { get; private set; }

        /// <summary>
        /// Operations for creating new items, and reading/querying all items
        /// </summary>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosItemResponse<MyCustomObject> response = await this.container.Items.CreateItemAsync<MyCustomObject>(user1);
        /// ]]>
        /// </code>
        /// </example>
        public virtual CosmosItems Items { get; }

        /// <summary>
        /// Operations for creating, reading/querying all stored procedures
        /// </summary>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosStoredProcedureSettings settings = new CosmosStoredProcedureSettings
        ///{
        ///    Id = "testSProcId",
        ///    Body = "function() { { var x = 42; } }"
        ///};
        ///
        /// CosmosStoredProcedureResponse response = await cosmosContainer.StoredProcedures.CreateStoredProcedureAsync(settings);
        /// ]]>
        /// </code>
        /// </example>
        public virtual CosmosStoredProcedures StoredProcedures { get; }


        /// <summary>
        /// Operations for creating, reading/querying all triggers
        /// </summary>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosTriggerSettings settings = new CosmosTriggerSettings
        ///{
        ///    Id = "testSProcId",
        ///    Body = "function() { { var x = 42; } }"
        ///};
        ///
        /// CosmosTriggerResponse response = await cosmosContainer.Triggers.CreateTriggerAsync(settings);
        /// ]]>
        /// </code>
        /// </example>
        internal CosmosTriggers Triggers { get; }

        /// <summary>
        /// Operations for creating, reading/querying all user defined functions
        /// </summary>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        ///  CosmosUserDefinedFunctionSettings settings = new CosmosUserDefinedFunctionSettings
        ///  {
        ///      Id = "testUserDefinedFunId",
        ///      Body = "function() { { var x = 42; } }",
        ///  };
        ///
        /// CosmosUserDefinedFunctionsResponse response = await cosmosContainer.UserDefinedFunctions.CreateUserDefinedFunctionAsync(settings);
        /// ]]>
        /// </code>
        /// </example>
        internal CosmosUserDefinedFunctions UserDefinedFunctions { get; }

        internal DocumentClient DocumentClient { get; private set; }

        internal override string UriPathSegment => Paths.CollectionsPathSegment;

        /// <summary>
        /// Reads a <see cref="CosmosContainerSettings"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the container request <see cref="CosmosRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="CosmosContainerResponse"/> which wraps a <see cref="CosmosContainerSettings"/> containing the read resource record.
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
        /// CosmosContainer cosmosContainer = this.database.Containers["containerId"];
        /// CosmosContainerSettings settings = cosmosContainer.ReadAsync();
        /// ]]>
        /// </code>
        /// </example>
        public virtual Task<CosmosContainerResponse> ReadAsync(
            CosmosContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<CosmosResponseMessage> response = this.ReadStreamAsync(
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.Client.ResponseFactory.CreateContainerResponse(this, response);
        }

        /// <summary>
        /// Replace a <see cref="CosmosContainerSettings"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="containerSettings">The <see cref="CosmosContainerSettings"/> object.</param>
        /// <param name="requestOptions">(Optional) The options for the container request <see cref="CosmosRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="CosmosContainerResponse"/> which wraps a <see cref="CosmosContainerSettings"/> containing the replace resource record.
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
        /// ContainerSettings setting = containerReadResponse;
        /// setting.IndexingPolicy.Automatic = false;
        /// CosmosContainerResponse response = cosmosContainer.ReplaceAsync(setting);
        /// ContainerSettings settings = response;
        /// ]]>
        /// </code>
        /// </example>
        public virtual Task<CosmosContainerResponse> ReplaceAsync(
            CosmosContainerSettings containerSettings,
            CosmosContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            this.Database.Client.DocumentClient.ValidateResource(containerSettings);

            Task<CosmosResponseMessage> response = this.ReplaceStreamAsync(
                streamPayload: containerSettings.GetResourceStream(),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.Client.ResponseFactory.CreateContainerResponse(this, response);
        }

        /// <summary>
        /// Delete a <see cref="CosmosContainerSettings"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the container request <see cref="CosmosRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="CosmosContainerResponse"/> which will contain information about the request issued.</returns>
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
        /// CosmosContainerResponse response = cosmosContainer.DeleteAsync();
        ///]]>
        /// </code>
        /// </example>
        public virtual Task<CosmosContainerResponse> DeleteAsync(
            CosmosContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<CosmosResponseMessage> response = this.DeleteStreamAsync(
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.Client.ResponseFactory.CreateContainerResponse(this, response);
        }

        /// <summary>
        /// Gets throughput provisioned for a container in measurement of Requests-per-Unit in the Azure Cosmos service.
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
        public virtual async Task<int?> ReadProvisionedThroughputAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            CosmosOfferResult offerResult = await this.ReadProvisionedThroughputIfExistsAsync(cancellationToken);
            if (offerResult.StatusCode == HttpStatusCode.OK || offerResult.StatusCode == HttpStatusCode.NotFound)
            {
                return offerResult.Throughput;
            }

            throw offerResult.CosmosException;
        }

        /// <summary>
        /// Sets throughput provisioned for a container in measurement of Requests-per-Unit in the Azure Cosmos service.
        /// </summary>
        /// <param name="throughput">The cosmos container throughput</param>
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
        /// int? throughput = await this.cosmosContainer.ReplaceProvisionedThroughputAsync(400);
        /// ]]>
        /// </code>
        /// </example>
        public virtual async Task ReplaceProvisionedThroughputAsync(
            int throughput,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            CosmosOfferResult offerResult = await this.ReplaceProvisionedThroughputIfExistsAsync(throughput, cancellationToken);
            if (offerResult.StatusCode != HttpStatusCode.OK)
            {
                throw offerResult.CosmosException;
            }
        }

        /// <summary>
        /// Delete a <see cref="CosmosContainerSettings"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the container request <see cref="CosmosRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="CosmosResponseMessage"/> which will contain information about the request issued.</returns>
        internal virtual Task<CosmosResponseMessage> DeleteStreamAsync(
            CosmosContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessStreamAsync(
               streamPayload: null,
               operationType: OperationType.Delete,
               requestOptions: requestOptions,
               cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Replace a <see cref="CosmosContainerSettings"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="streamPayload">The <see cref="CosmosContainerSettings"/> stream.</param>
        /// <param name="requestOptions">(Optional) The options for the container request <see cref="CosmosRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        internal virtual Task<CosmosResponseMessage> ReplaceStreamAsync(
            Stream streamPayload,
            CosmosContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessStreamAsync(
                streamPayload: streamPayload,
                operationType: OperationType.Replace,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Reads a <see cref="CosmosContainerSettings"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the container request <see cref="CosmosRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="CosmosContainerResponse"/> which wraps a <see cref="CosmosContainerSettings"/> containing the read resource record.
        /// </returns>
        internal virtual Task<CosmosResponseMessage> ReadStreamAsync(
            CosmosContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessStreamAsync(
                streamPayload: null,
                operationType: OperationType.Read,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        internal virtual Task<CosmosOfferResult> ReadProvisionedThroughputIfExistsAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.GetRID(cancellationToken)
                .ContinueWith(task => task.Result == null ?
                    Task.FromResult(new CosmosOfferResult(
                        statusCode: HttpStatusCode.Found,
                        cosmosRequestException: new CosmosException(
                            message: RMResources.NotFound,
                            statusCode: HttpStatusCode.Found,
                            subStatusCode: (int)SubStatusCodes.Unknown,
                            activityId: null,
                            requestCharge: 0))) :
                    this.Database.Client.Offers.ReadProvisionedThroughputIfExistsAsync(task.Result, cancellationToken),
                    cancellationToken)
                .Unwrap();
        }

        internal virtual Task<CosmosOfferResult> ReplaceProvisionedThroughputIfExistsAsync(
            int targetThroughput,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.GetRID(cancellationToken)
                 .ContinueWith(task => this.Database.Client.Offers.ReplaceThroughputIfExistsAsync(task.Result, targetThroughput, cancellationToken), cancellationToken)
                 .Unwrap();
        }

        /// <summary>
        /// Gets the container's settings by using the internal cache.
        /// In case the cache does not have information about this container, it may end up making a server call to fetch the data.
        /// </summary>
        /// <param name="cancellationToken"><see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing the <see cref="CosmosContainerSettings"/> for this container.</returns>
        internal Task<CosmosContainerSettings> GetCachedContainerSettingsAsync(CancellationToken cancellationToken)
        {
            return this.DocumentClient.GetCollectionCacheAsync()
                .ContinueWith(collectionCacheTask => collectionCacheTask.Result.ResolveByNameAsync(this.Link, cancellationToken), cancellationToken)
                .Unwrap();
        }

        // Name based look-up, needs re-computation and can't be cached
        internal Task<string> GetRID(CancellationToken cancellationToken)
        {
            return this.GetCachedContainerSettingsAsync(cancellationToken)
                            .ContinueWith(containerSettingsTask => containerSettingsTask.Result?.ResourceId, cancellationToken);
        }

        internal Task<PartitionKeyDefinition> GetPartitionKeyDefinitionAsync(CancellationToken cancellationToken)
        {
            return this.GetCachedContainerSettingsAsync(cancellationToken)
                            .ContinueWith(containerSettingsTask => containerSettingsTask.Result?.PartitionKey, cancellationToken);
        }

        internal Task<CollectionRoutingMap> GetRoutingMapAsync(CancellationToken cancellationToken)
        {
            string collectionRID = null;
            return this.GetRID(cancellationToken)
                .ContinueWith(ridTask =>
                {
                    collectionRID = ridTask.Result;
                    return this.DocumentClient.GetPartitionKeyRangeCacheAsync();
                })
                .Unwrap()
                .ContinueWith(partitionKeyRangeCachetask =>
                {
                    PartitionKeyRangeCache partitionKeyRangeCache = partitionKeyRangeCachetask.Result;
                    return partitionKeyRangeCache.TryLookupAsync(
                            collectionRID,
                            null,
                            null,
                            false,
                            cancellationToken);
                })
                .Unwrap();
        }

        private Task<CosmosResponseMessage> ProcessStreamAsync(
            Stream streamPayload,
            OperationType operationType,
            CosmosContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return ExecUtils.ProcessResourceOperationStreamAsync(
              client: this.Client,
              resourceUri: this.LinkUri,
              resourceType: ResourceType.Collection,
              operationType: operationType,
              partitionKey: null,
              streamPayload: streamPayload,
              requestOptions: requestOptions,
              requestEnricher: null,
              cancellationToken: cancellationToken);
        }
    }
}
