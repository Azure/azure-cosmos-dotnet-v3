//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Fluent;

    /// <summary>
    /// Operations for reading or deleting an existing database.
    ///
    /// <see cref="CosmosClient"/> for or creating new databases, and reading/querying all databases; use `client.Databases`.
    /// </summary>
    /// <remarks>
    /// Note: all these operations make calls against a fixed budget.
    /// You should design your system such that these calls scale sub-linearly with your application.
    /// For instance, do not call `database.ReadAsync()` before every single `item.ReadAsync()` call, to ensure the database exists;
    /// do this once on application start up.
    /// </remarks>
    public abstract class CosmosDatabase
    {
        /// <summary>
        /// The Id of the Cosmos database
        /// </summary>
        public abstract string Id { get;  }

        /// <summary>
        /// Reads a <see cref="CosmosDatabaseProperties"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the container request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="DatabaseResponse"/> which wraps a <see cref="CosmosDatabaseProperties"/> containing the read resource record.
        /// </returns>
        /// <exception cref="CosmosException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list>
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
        /// //Reads a Database resource where
        /// // - database_id is the ID property of the Database resource you wish to read.
        /// CosmosDatabase database = this.cosmosClient.GetDatabase(database_id);
        /// DatabaseResponse response = await database.ReadAsync();
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <para>
        /// Doing a read of a resource is the most efficient way to get a resource from the Database. If you know the resource's ID, do a read instead of a query by ID.
        /// </para>
        /// </remarks>
        public abstract Task<DatabaseResponse> ReadAsync(
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Delete a <see cref="CosmosDatabaseProperties"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the container request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="DatabaseResponse"/> which will contain information about the request issued.</returns>
        /// <exception cref="CosmosException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list>
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
        /// //Delete a cosmos database
        /// CosmosDatabase database = cosmosClient.GetDatabase("myDbId");
        /// DatabaseResponse response = await database.DeleteAsync();
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<DatabaseResponse> DeleteAsync(
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Gets database throughput in measurement of Requests-per-Unit in the Azure Cosmos service.
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
        /// int? throughput = await this.cosmosDatabase.ReadProvisionedThroughputAsync();
        /// ]]>
        /// </code>
        /// </example>
        /// <returns>The throughput response.</returns>
        public abstract Task<ThroughputResponse> ReadProvisionedThroughputAsync(
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Gets database throughput in measurement of Requests-per-Unit in the Azure Cosmos service.
        /// </summary>
        /// <param name="allowedMinThroughput">(Optional) if this flag is not set , ThroughputResponse won't contain minimum throughput.</param>
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
        /// int? throughput = await this.cosmosDatabase.ReadProvisionedThroughputAsync();
        /// ]]>
        /// </code>
        /// </example>
        /// <returns>The throughput response.</returns>
        internal abstract Task<ThroughputResponse> ReadProvisionedThroughputInternalAsync(bool allowedMinThroughput = true,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Sets throughput provisioned for a database in measurement of Requests-per-Unit in the Azure Cosmos service.
        /// </summary>
        /// <param name="requestUnitsPerSecond">The cosmos database throughput expressed in Request Units per second.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <value>
        /// The provisioned throughput for this database.
        /// </value>
        /// <example>
        /// The following example shows how to get the throughput.
        /// <code language="c#">
        /// <![CDATA[
        /// int? throughput = await this.cosmosDatabase.ReplaceProvisionedThroughputAsync(10000);
        /// ]]>
        /// </code>
        /// </example>
        /// <returns>A Task representing the asynchronous operation.</returns>
        /// <remarks>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units"/> for details on provision throughput.
        /// </remarks>
        public abstract Task ReplaceProvisionedThroughputAsync(
            int requestUnitsPerSecond,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Reads a <see cref="CosmosDatabaseProperties"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the container request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="CosmosResponseMessage"/> containing the read resource record.
        /// </returns>
        public abstract Task<CosmosResponseMessage> ReadStreamAsync(
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Delete a <see cref="CosmosDatabaseProperties"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the container request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="CosmosResponseMessage"/> which will contain information about the request issued.</returns>
        public abstract Task<CosmosResponseMessage> DeleteStreamAsync(
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken));

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
        /// CosmosDatabase db = this.cosmosClient.GetDatabase("myDatabaseId"];
        /// DatabaseResponse response = await db.GetContainer("testcontainer");
        /// ]]>
        /// </code>
        /// </example>
        /// <returns>Cosmos container proxy</returns>
        public abstract CosmosContainer GetContainer(string id);

        /// <summary>
        /// Creates a container as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <param name="containerProperties">The <see cref="CosmosContainerProperties"/> object.</param>
        /// <param name="requestUnitsPerSecond">(Optional) The throughput provisioned for a container in measurement of Requests Units per second in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the container request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="ContainerResponse"/> which wraps a <see cref="CosmosContainerProperties"/> containing the read resource record.</returns>
        /// <exception cref="ArgumentNullException">If either <paramref name="containerProperties"/> is not set.</exception>
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
        ///         <term>409</term><description>Conflict - This means a <see cref="CosmosContainerProperties"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosContainerProperties containerProperties = new CosmosContainerProperties() 
        /// { 
        ///     Id = Guid.NewGuid().ToString(),
        ///     IndexingPolicy = new IndexingPolicy()
        ///    {
        ///         Automatic = false,
        ///         IndexingMode = IndexingMode.Lazy,
        ///    };
        /// };
        /// 
        /// ContainerResponse response = this.cosmosDatabase.CreateContainerAsync(containerProperties);
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="DefineContainer(string, string)"/>
        /// <remarks>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units"/> for details on provision throughput.
        /// </remarks>
        public abstract Task<ContainerResponse> CreateContainerAsync(
                    CosmosContainerProperties containerProperties,
                    int? requestUnitsPerSecond = null,
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Creates a container as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <param name="id">The cosmos container id</param>
        /// <param name="partitionKeyPath">The path to the partition key. Example: /location</param>
        /// <param name="requestUnitsPerSecond">(Optional) The throughput provisioned for a container in measurement of Requests Units per second in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the container request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="ContainerResponse"/> which wraps a <see cref="CosmosContainerProperties"/> containing the read resource record.</returns>
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
        ///         <term>409</term><description>Conflict - This means a <see cref="CosmosContainerProperties"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// ContainerResponse response = this.cosmosDatabase.CreateContainerAsync(Guid.NewGuid().ToString());
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="DefineContainer(string, string)"/>
        /// <remarks>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units"/> for details on provision throughput.
        /// </remarks>
        public abstract Task<ContainerResponse> CreateContainerAsync(
            string id,
            string partitionKeyPath,
            int? requestUnitsPerSecond = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Check if a container exists, and if it doesn't, create it.
        /// This will make a read operation, and if the container is not found it will do a create operation.
        /// </summary>
        /// <param name="containerProperties">The <see cref="CosmosContainerProperties"/> object.</param>
        /// <param name="requestUnitsPerSecond">(Optional) The throughput provisioned for a container in measurement of Requests Units per second in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the container request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="ContainerResponse"/> which wraps a <see cref="CosmosContainerProperties"/> containing the read resource record.</returns>
        /// <exception cref="ArgumentNullException">If either <paramref name="containerProperties"/> is not set.</exception>
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
        ///         <term>409</term><description>Conflict - This means a <see cref="CosmosContainerProperties"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosContainerProperties containerProperties = new CosmosContainerProperties() 
        /// { 
        ///     Id = Guid.NewGuid().ToString(),
        ///     IndexingPolicy = new IndexingPolicy()
        ///    {
        ///         Automatic = false,
        ///         IndexingMode = IndexingMode.Lazy,
        ///    };
        /// };
        /// 
        /// ContainerResponse response = this.cosmosDatabase.CreateContainerIfNotExistsAsync(containerProperties);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units"/> for details on provision throughput.
        /// </remarks>
        public abstract Task<ContainerResponse> CreateContainerIfNotExistsAsync(
            CosmosContainerProperties containerProperties,
            int? requestUnitsPerSecond = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Check if a container exists, and if it doesn't, create it.
        /// This will make a read operation, and if the container is not found it will do a create operation.
        /// </summary>
        /// <param name="id">The cosmos container id</param>
        /// <param name="partitionKeyPath">The path to the partition key. Example: /location</param>
        /// <param name="requestUnitsPerSecond">(Optional) The throughput provisioned for a container in measurement of Request Units per second in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the container request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="ContainerResponse"/> which wraps a <see cref="CosmosContainerProperties"/> containing the read resource record.</returns>
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
        ///         <term>409</term><description>Conflict - This means a <see cref="CosmosContainerProperties"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// ContainerResponse response = this.cosmosDatabase.CreateContainerIfNotExistsAsync(Guid.NewGuid().ToString());
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units"/> for details on provision throughput.
        /// </remarks>
        public abstract Task<ContainerResponse> CreateContainerIfNotExistsAsync(
            string id,
            string partitionKeyPath,
            int? requestUnitsPerSecond = null,
            RequestOptions requestOptions = null,
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
        /// FeedIterator<CosmosContainerProperties> feedIterator = this.cosmosDatabase.GetContainersIterator();
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     foreach(CosmosContainerProperties containerProperties in await feedIterator.FetchNextSetAsync())
        ///     {
        ///          Console.WriteLine(containerProperties.Id); 
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <returns>An iterator to go through the containers</returns>
        public abstract FeedIterator<CosmosContainerProperties> GetContainersIterator(
            int? maxItemCount = null,
            string continuationToken = null);

        /// <summary>
        /// Creates a container as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <param name="containerProperties">The <see cref="CosmosContainerProperties"/> object.</param>
        /// <param name="requestUnitsPerSecond">(Optional) The throughput provisioned for a container in measurement of Request Units per second in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the container request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="CosmosResponseMessage"/> containing the created resource record.</returns>
        /// <seealso cref="DefineContainer(string, string)"/>
        /// <remarks>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units"/> for details on provision throughput.
        /// </remarks>
        public abstract Task<CosmosResponseMessage> CreateContainerStreamAsync(
            CosmosContainerProperties containerProperties,
            int? requestUnitsPerSecond = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Gets an iterator to go through all the containers for the database
        /// </summary>
        /// <param name="maxItemCount">(Optional) The max item count to return as part of the query</param>
        /// <param name="continuationToken">The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the container request <see cref="QueryRequestOptions"/></param>
        /// <returns>An iterator to go through the containers</returns>
        public abstract FeedIterator GetContainersStreamIterator(
            int? maxItemCount = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);

        /// <summary>
        /// Create an Azure Cosmos container through a Fluent API.
        /// </summary>
        /// <param name="name">Azure Cosmos container name to create.</param>
        /// <param name="partitionKeyPath">The path to the partition key. Example: /partitionKey</param>
        /// <returns>A fluent definition of an Azure Cosmos container.</returns>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosContainerResponse container = await this.cosmosDatabase.DefineContainer("TestContainer", "/partitionKey")
        ///     .UniqueKey()
        ///         .Path("/path1")
        ///         .Path("/path2")
        ///         .Attach()
        ///     .IndexingPolicy()
        ///         .IndexingMode(IndexingMode.Consistent)
        ///         .AutomaticIndexing(false)
        ///         .IncludedPaths()
        ///             .Path("/includepath1")
        ///             .Path("/includepath2")
        ///             .Attach()
        ///         .ExcludedPaths()
        ///             .Path("/excludepath1")
        ///             .Path("/excludepath2")
        ///             .Attach()
        ///         .CompositeIndex()
        ///             .Path("/root/leaf1")
        ///             .Path("/root/leaf2", CompositePathSortOrder.Descending)
        ///             .Attach()
        ///         .CompositeIndex()
        ///             .Path("/root/leaf3")
        ///             .Path("/root/leaf4")
        ///             .Attach()
        ///         .Attach()
        ///     .CreateAsync(5000 /* throughput /*); 
        /// ]]>
        /// </code>
        /// </example>
        public abstract CosmosContainerFluentDefinitionForCreate DefineContainer(
            string name,
            string partitionKeyPath);
    }
}
