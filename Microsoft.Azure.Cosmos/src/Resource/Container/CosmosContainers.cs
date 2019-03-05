//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Operations for creating new containers, and reading/querying all containers
    ///
    /// <see cref="CosmosContainer"/> for reading, replacing, or deleting an existing container.
    ///
    /// Note: all these operations make calls against a fixed budget.
    /// You should design your system such that these calls scale sub-linearly with your application.
    /// For instance, do not call `containers.GetContainerIterator()` before every single `item.read()` call, to ensure the container exists;
    /// do this once on application start up.
    /// </summary>
    public abstract class CosmosContainers
    {
        /// <summary>
        /// Creates a container as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <param name="containerSettings">The <see cref="CosmosContainerSettings"/> object.</param>
        /// <param name="throughput">(Optional) The throughput provisioned for a collection in measurement of Requests-per-Unit in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the container request <see cref="CosmosRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="CosmosContainerResponse"/> which wraps a <see cref="CosmosContainerSettings"/> containing the read resource record.</returns>
        /// <exception cref="ArgumentNullException">If either <paramref name="containerSettings"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occurred during async processing. Look within InnerExceptions to find the actual exception(s).</exception>
        /// <exception cref="CosmosException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a container are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the request supplied. It is likely that an id was not supplied for the new container.</description>
        ///     </item>
        ///     <item>
        ///         <term>403</term><description>Forbidden - This means you attempted to exceed your quota for containers. Contact support to have this quota increased.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a <see cref="CosmosContainerSettings"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosContainerSettings settings = new CosmosContainerSettings() 
        /// { 
        ///     Id = Guid.NewGuid().ToString(),
        ///     IndexingPolicy = new IndexingPolicy()
        ///    {
        ///         Automatic = false,
        ///         IndexingMode = IndexingMode.Lazy,
        ///    };
        /// };
        /// 
        /// CosmosContainerResponse response = this.cosmosDatabase.Containers.CreateContainerAsync(settings);
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<CosmosContainerResponse> CreateContainerAsync(
                    CosmosContainerSettings containerSettings,
                    int? throughput = null,
                    CosmosRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Creates a container as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <param name="id">The cosmos container id</param>
        /// <param name="partitionKeyPath">The path to the partition key. Example: /location</param>
        /// <param name="throughput">(Optional) The throughput provisioned for a collection in measurement of Requests-per-Unit in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the container request <see cref="CosmosRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="CosmosContainerResponse"/> which wraps a <see cref="CosmosContainerSettings"/> containing the read resource record.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="id"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occurred during async processing. Look within InnerExceptions to find the actual exception(s).</exception>
        /// <exception cref="CosmosException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a container are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the request supplied. It is likely that an id was not supplied for the new container.</description>
        ///     </item>
        ///     <item>
        ///         <term>403</term><description>Forbidden - This means you attempted to exceed your quota for containers. Contact support to have this quota increased.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a <see cref="CosmosContainerSettings"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosContainerResponse response = this.cosmosDatabase.Containers.CreateContainerAsync(Guid.NewGuid().ToString());
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<CosmosContainerResponse> CreateContainerAsync(
            string id,
            string partitionKeyPath,
            int? throughput = null,
            CosmosRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Check if a container exists, and if it doesn't, create it.
        /// This will make a read operation, and if the container is not found it will do a create operation.
        /// </summary>
        /// <param name="containerSettings">The <see cref="CosmosContainerSettings"/> object.</param>
        /// <param name="throughput">(Optional) The throughput provisioned for a collection in measurement of Requests-per-Unit in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the container request <see cref="CosmosRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="CosmosContainerResponse"/> which wraps a <see cref="CosmosContainerSettings"/> containing the read resource record.</returns>
        /// <exception cref="ArgumentNullException">If either <paramref name="containerSettings"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occurred during async processing. Look within InnerExceptions to find the actual exception(s).</exception>
        /// <exception cref="CosmosException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a container are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the request supplied. It is likely that an id was not supplied for the new container.</description>
        ///     </item>
        ///     <item>
        ///         <term>403</term><description>Forbidden - This means you attempted to exceed your quota for containers. Contact support to have this quota increased.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a <see cref="CosmosContainerSettings"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosContainerSettings settings = new CosmosContainerSettings() 
        /// { 
        ///     Id = Guid.NewGuid().ToString(),
        ///     IndexingPolicy = new IndexingPolicy()
        ///    {
        ///         Automatic = false,
        ///         IndexingMode = IndexingMode.Lazy,
        ///    };
        /// };
        /// 
        /// CosmosContainerResponse response = this.cosmosDatabase.Containers.CreateContainerIfNotExistsAsync(settings);
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<CosmosContainerResponse> CreateContainerIfNotExistsAsync(
            CosmosContainerSettings containerSettings,
            int? throughput = null,
            CosmosRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Check if a container exists, and if it doesn't, create it.
        /// This will make a read operation, and if the container is not found it will do a create operation.
        /// </summary>
        /// <param name="id">The cosmos container id</param>
        /// <param name="partitionKeyPath">The path to the partition key. Example: /location</param>
        /// <param name="throughput">(Optional) The throughput provisioned for a collection in measurement of Requests-per-Unit in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the container request <see cref="CosmosRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="CosmosContainerResponse"/> which wraps a <see cref="CosmosContainerSettings"/> containing the read resource record.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="id"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occurred during async processing. Look within InnerExceptions to find the actual exception(s).</exception>
        /// <exception cref="CosmosException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a container are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the request supplied. It is likely that an id was not supplied for the new container.</description>
        ///     </item>
        ///     <item>
        ///         <term>403</term><description>Forbidden - This means you attempted to exceed your quota for containers. Contact support to have this quota increased.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a <see cref="CosmosContainerSettings"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosContainerResponse response = this.cosmosDatabase.Containers.CreateContainerIfNotExistsAsync(Guid.NewGuid().ToString());
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<CosmosContainerResponse> CreateContainerIfNotExistsAsync(
            string id,
            string partitionKeyPath,
            int? throughput = null,
            CosmosRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Gets an iterator to go through all the containers for the database
        /// </summary>
        /// <param name="maxItemCount">(Optional) The max item count to return as part of the query</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <example>
        /// Get an iterator for all the containers under the database
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosResultSetIterator<CosmosContainerSettings> setIterator = this.cosmosDatabase.Containers.GetContainerIterator();
        /// while (setIterator.HasMoreResults)
        /// {
        ///     foreach(CosmosContainerSettings setting in await setIterator.FetchNextSetAsync())
        ///     {
        ///          Console.WriteLine(setting.Id); 
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract CosmosResultSetIterator<CosmosContainerSettings> GetContainerIterator(
            int? maxItemCount = null,
            string continuationToken = null);

        /// <summary>
        /// Returns a reference to a container object. 
        /// </summary>
        /// <param name="id">The cosmos container id.</param>
        /// <remarks>
        /// Note that the container must be explicitly created, if it does not already exist, before
        /// you can read from it or write to it.
        /// </remarks>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosDatabase db = this.cosmosClient.Databases["myDatabaseId"];
        /// CosmosDatabaseResponse response = await db.ReadAsync();
        /// ]]>
        /// </code>
        /// </example>
        public abstract CosmosContainer this[string id] { get; }

        /// <summary>
        /// Creates a container as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <param name="streamPayload">The <see cref="CosmosContainerSettings"/> object.</param>
        /// <param name="throughput">(Optional) The throughput provisioned for a collection in measurement of Requests-per-Unit in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the container request <see cref="CosmosRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="CosmosResponseMessage"/> containing the created resource record.</returns>
        public abstract Task<CosmosResponseMessage> CreateContainerStreamAsync(
            Stream streamPayload,
            int? throughput = null,
            CosmosRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Gets an iterator to go through all the containers for the database
        /// </summary>
        /// <param name="maxItemCount">(Optional) The max item count to return as part of the query</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the request <see cref="CosmosQueryRequestOptions"/></param>
        public abstract CosmosResultSetIterator GetContainerStreamIterator(
            int? maxItemCount = null,
            string continuationToken = null,
            CosmosQueryRequestOptions requestOptions = null);
    }
}
