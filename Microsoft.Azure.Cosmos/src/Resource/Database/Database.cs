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
    /// See <see cref="CosmosClient"/> for creating new databases, and reading/querying all databases; use `client.Databases`.
    /// </summary>
    /// <remarks>
    /// Note: all these operations make calls against a fixed budget.
    /// You should design your system such that these calls scale sub-linearly with your application.
    /// For instance, do not call `database.ReadAsync()` before every single `container.ReadItemAsync()` call to ensure the database exists;
    /// do this once on application start up.
    /// </remarks>
    public abstract class Database
    {
        /// <summary>
        /// The Id of the Cosmos database
        /// </summary>
        public abstract string Id { get; }

        /// <summary>
        /// Reads a <see cref="DatabaseProperties"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="DatabaseResponse"/> which wraps a <see cref="DatabaseProperties"/> containing the read resource record.
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
        /// // Reads a Database resource where database_id is the ID property of the Database resource you wish to read.
        /// Database database = this.cosmosClient.GetDatabase(database_id);
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
        /// Delete a Database from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="DatabaseResponse"/> which will contain information about the request issued.</returns>
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
        /// // Delete a Cosmos database
        /// Database database = cosmosClient.GetDatabase("myDbId");
        /// DatabaseResponse response = await database.DeleteAsync();
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<DatabaseResponse> DeleteAsync(
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken));
        /// <summary>
        /// Gets database throughput in measurement of request units per second in the Azure Cosmos service.
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>Provisioned throughput in request units per second</returns>
        /// <value>
        /// The provisioned throughput for this database.
        /// </value>
        /// <remarks>
        /// Null value indicates a database with no throughput provisioned.
        /// </remarks>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units">Request Units</seealso>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/set-throughput#set-throughput-on-a-database">Set throughput on a database</seealso>
        /// <example>
        /// The following example shows how to get database throughput.
        /// <code language="c#">
        /// <![CDATA[
        /// int? throughput = await database.ReadThroughputAsync();
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<int?> ReadThroughputAsync(
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Gets database throughput in measurement of request units per second in the Azure Cosmos service.
        /// </summary>
        /// <param name="requestOptions">The options for the throughput request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The throughput response.</returns>
        /// <value>
        /// The provisioned throughput for this database.
        /// </value>
        /// <remarks>
        /// Null value indicates a database with no throughput provisioned.
        /// </remarks>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units">Request Units</seealso>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/set-throughput#set-throughput-on-a-database">Set throughput on a database</seealso>
        /// <example>
        /// The following example shows how to get the throughput
        /// <code language="c#">
        /// <![CDATA[
        ///  RequestOptions requestOptions = new RequestOptions();
        /// ThroughputProperties throughputProperties = await database.ReadThroughputAsync(requestOptions);
        /// Console.WriteLine($"Throughput: {throughputProperties?.Throughput}");
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// The following example shows how to get throughput, MinThroughput and is replace in progress
        /// <code language="c#">
        /// <![CDATA[
        /// RequestOptions requestOptions = new RequestOptions();
        /// ThroughputResponse response = await database.ReadThroughputAsync(requestOptions);
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
        /// Sets throughput provisioned for a database in measurement of request units per second in the Azure Cosmos service.
        /// </summary>
        /// <param name="throughputProperties">The Cosmos database throughput expressed in Request Units per second.</param>
        /// <param name="requestOptions">(Optional) The options for the throughput request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The throughput response.</returns>
        /// <value>
        /// The provisioned throughput for this database.
        /// </value>
        /// <example>
        /// The following example shows how to replace the manual throughput.
        /// <code language="c#">
        /// <![CDATA[
        /// ThroughputResponse throughput = await this.cosmosDatabase.ReplaceThroughputAsync(
        ///     ThroughputProperties.CreateManualThroughput(10000));
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// The following example shows how to replace the autoscale provisioned throughput.
        /// <code language="c#">
        /// <![CDATA[
        /// ThroughputResponse throughput = await this.cosmosDatabase.ReplaceThroughputAsync(
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
        /// Creates a container as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <param name="containerProperties">The <see cref="ContainerProperties"/> object.</param>
        /// <param name="throughputProperties">(Optional) The throughput provisioned for a container in measurement of Requests Units per second in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="ContainerResponse"/> which wraps a <see cref="ContainerProperties"/> containing the read resource record.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="containerProperties"/> is not set.</exception>
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
        ///         <term>409</term><description>Conflict - This means a <see cref="ContainerProperties"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// ContainerProperties containerProperties = new ContainerProperties()
        /// {
        ///     Id = Guid.NewGuid().ToString(),
        ///     PartitionKeyPath = "/pk",
        ///     IndexingPolicy = new IndexingPolicy()
        ///    {
        ///         Automatic = false,
        ///         IndexingMode = IndexingMode.Lazy,
        ///    };
        /// };
        ///
        /// ContainerResponse response = await this.cosmosDatabase.CreateContainerAsync(
        ///     containerProperties,
        ///     ThroughputProperties.CreateAutoscaleThroughput(10000));
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units">Request Units</seealso>
        public abstract Task<ContainerResponse> CreateContainerAsync(
                    ContainerProperties containerProperties,
                    ThroughputProperties throughputProperties,
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// <para>Check if a container exists, and if it doesn't, create it.
        /// Only the container id is used to verify if there is an existing container. Other container properties such as throughput are not validated and can be different then the passed properties.</para>
        /// </summary>
        /// <param name="containerProperties">The <see cref="ContainerProperties"/> object.</param>
        /// <param name="throughputProperties">(Optional) The throughput provisioned for a container in measurement of Requests Units per second in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="ContainerResponse"/> which wraps a <see cref="ContainerProperties"/> containing the read resource record.
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Common success StatusCodes for the CreateDatabaseIfNotExistsAsync operation</description>
        ///     </listheader>
        ///     <item>
        ///         <term>201</term><description>Created - New database is created.</description>
        ///     </item>
        ///     <item>
        ///         <term>200</term><description>Accepted - This means the database already exists.</description>
        ///     </item>
        /// </list>
        /// </returns>
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
        ///         <term>409</term><description>Conflict - This means a <see cref="ContainerProperties"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// ContainerProperties containerProperties = new ContainerProperties()
        /// {
        ///     Id = Guid.NewGuid().ToString(),
        ///     PartitionKeyPath = "/pk",
        ///     IndexingPolicy = new IndexingPolicy()
        ///    {
        ///         Automatic = false,
        ///         IndexingMode = IndexingMode.Lazy,
        ///    };
        /// };
        ///
        /// ContainerResponse response = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(
        ///      containerProperties,
        ///      ThroughputProperties.CreateAutoscaleThroughput(5000));
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units">Request Units</seealso>
        public abstract Task<ContainerResponse> CreateContainerIfNotExistsAsync(
            ContainerProperties containerProperties,
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Creates a container as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <param name="containerProperties">The <see cref="ContainerProperties"/> object.</param>
        /// <param name="throughputProperties">(Optional) The throughput provisioned for a container in measurement of Request Units per second in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the container request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="ResponseMessage"/> containing the created resource record.</returns>
        /// <example>
        /// Creates a container as an asynchronous operation in the Azure Cosmos service and return stream response.
        /// <code language="c#">
        /// <![CDATA[
        /// ContainerProperties containerProperties = new ContainerProperties()
        /// {
        ///     Id = Guid.NewGuid().ToString(),
        ///     PartitionKeyPath = "/pk",
        /// };
        ///
        /// using(ResponseMessage response = await this.cosmosDatabase.CreateContainerStreamAsync(
        ///     containerProperties,
        ///     ThroughputProperties.CreateAutoscaleThroughput(10000)))
        /// {
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="DefineContainer(string, string)"/>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units">Request Units</seealso>
        public abstract Task<ResponseMessage> CreateContainerStreamAsync(
            ContainerProperties containerProperties,
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Sets throughput provisioned for a database in measurement of request units per second in the Azure Cosmos service.
        /// </summary>
        /// <param name="throughput">The Cosmos database throughput expressed in Request Units per second.</param>
        /// <param name="requestOptions">(Optional) The options for the throughput request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The throughput response.</returns>
        /// <value>
        /// The provisioned throughput for this database.
        /// </value>
        /// <example>
        /// The following example shows how to get the throughput.
        /// <code language="c#">
        /// <![CDATA[
        /// ThroughputResponse throughput = await this.cosmosDatabase.ReplaceThroughputAsync(10000);
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units">Request Units</seealso>
        /// </remarks>
        public abstract Task<ThroughputResponse> ReplaceThroughputAsync(
            int throughput,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Reads a <see cref="DatabaseProperties"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="ResponseMessage"/> containing the read resource record.
        /// </returns>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// // Reads a Database resource where database_id is the ID property of the Database resource you wish to read.
        /// Database database = this.cosmosClient.GetDatabase(database_id);
        /// ResponseMessage response = await database.ReadContainerStreamAsync();
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<ResponseMessage> ReadStreamAsync(
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Delete a <see cref="DatabaseProperties"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="ResponseMessage"/> which will contain information about the request issued.</returns>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// // Delete a Database resource where database_id is the ID property of the Database resource you wish to delete.
        /// Database database = this.cosmosClient.GetDatabase(database_id);
        /// await database.DeleteStreamAsync();
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<ResponseMessage> DeleteStreamAsync(
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Returns a reference to a container object. 
        /// </summary>
        /// <param name="id">The Cosmos container id.</param>
        /// <returns>Cosmos container reference</returns>
        /// <remarks>
        /// Returns a Container reference. Reference doesn't guarantee existence.
        /// Please ensure container already exists or is created through a create operation.
        /// </remarks>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// Database db = this.cosmosClient.GetDatabase("myDatabaseId");
        /// Container container = db.GetContainer("testcontainer");
        /// ]]>
        /// </code>
        /// </example>
        public abstract Container GetContainer(string id);

        /// <summary>
        /// Creates a container as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <param name="containerProperties">The <see cref="ContainerProperties"/> object.</param>
        /// <param name="throughput">(Optional) The throughput provisioned for a container in measurement of Requests Units per second in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="ContainerResponse"/> which wraps a <see cref="ContainerProperties"/> containing the read resource record.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="containerProperties"/> is not set.</exception>
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
        ///         <term>409</term><description>Conflict - This means a <see cref="ContainerProperties"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// ContainerProperties containerProperties = new ContainerProperties()
        /// {
        ///     Id = Guid.NewGuid().ToString(),
        ///     PartitionKeyPath = "/pk",
        ///     IndexingPolicy = new IndexingPolicy()
        ///    {
        ///         Automatic = false,
        ///         IndexingMode = IndexingMode.Lazy,
        ///    };
        /// };
        ///
        /// ContainerResponse response = await this.cosmosDatabase.CreateContainerAsync(containerProperties);
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="DefineContainer(string, string)"/>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units">Request Units</seealso>
        public abstract Task<ContainerResponse> CreateContainerAsync(
                    ContainerProperties containerProperties,
                    int? throughput = null,
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Creates a container as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <param name="id">The Cosmos container id</param>
        /// <param name="partitionKeyPath">The path to the partition key. Example: /location</param>
        /// <param name="throughput">(Optional) The throughput provisioned for a container in measurement of Requests Units per second in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="ContainerResponse"/> which wraps a <see cref="ContainerProperties"/> containing the read resource record.</returns>
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
        ///         <term>409</term><description>Conflict - This means a <see cref="ContainerProperties"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// ContainerResponse response = await this.cosmosDatabase.CreateContainerAsync(Guid.NewGuid().ToString(), "/pk");
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="DefineContainer(string, string)"/>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units">Request Units</seealso>
        public abstract Task<ContainerResponse> CreateContainerAsync(
            string id,
            string partitionKeyPath,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// <para>Check if a container exists, and if it doesn't, create it.
        /// Only the container id is used to verify if there is an existing container. Other container properties such as throughput are not validated and can be different then the passed properties.</para>
        /// </summary>
        /// <param name="containerProperties">The <see cref="ContainerProperties"/> object.</param>
        /// <param name="throughput">(Optional) The throughput provisioned for a container in measurement of Requests Units per second in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="ContainerResponse"/> which wraps a <see cref="ContainerProperties"/> containing the read resource record.
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Common success StatusCodes for the CreateDatabaseIfNotExistsAsync operation</description>
        ///     </listheader>
        ///     <item>
        ///         <term>201</term><description>Created - New database is created.</description>
        ///     </item>
        ///     <item>
        ///         <term>200</term><description>Accepted - This means the database already exists.</description>
        ///     </item>
        /// </list>
        /// </returns>
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
        ///         <term>409</term><description>Conflict - This means a <see cref="ContainerProperties"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// ContainerProperties containerProperties = new ContainerProperties()
        /// {
        ///     Id = Guid.NewGuid().ToString(),
        ///     PartitionKeyPath = "/pk",
        ///     IndexingPolicy = new IndexingPolicy()
        ///    {
        ///         Automatic = false,
        ///         IndexingMode = IndexingMode.Lazy,
        ///    };
        /// };
        ///
        /// ContainerResponse response = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(containerProperties);
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units">Request Units</seealso>
        public abstract Task<ContainerResponse> CreateContainerIfNotExistsAsync(
            ContainerProperties containerProperties,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Check if a container exists, and if it doesn't, create it.
        /// This will make a read operation, and if the container is not found it will do a create operation.
        /// </summary>
        /// <param name="id">The Cosmos container id</param>
        /// <param name="partitionKeyPath">The path to the partition key. Example: /location</param>
        /// <param name="throughput">(Optional) The throughput provisioned for a container in measurement of Request Units per second in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="ContainerResponse"/> which wraps a <see cref="ContainerProperties"/> containing the read resource record.</returns>
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
        ///         <term>409</term><description>Conflict - This means a <see cref="ContainerProperties"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// ContainerResponse response = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(Guid.NewGuid().ToString(), "/pk");
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units">Request Units</seealso>
        public abstract Task<ContainerResponse> CreateContainerIfNotExistsAsync(
            string id,
            string partitionKeyPath,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Creates a container as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <param name="containerProperties">The <see cref="ContainerProperties"/> object.</param>
        /// <param name="throughput">(Optional) The throughput provisioned for a container in measurement of Request Units per second in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the container request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="ResponseMessage"/> containing the created resource record.</returns>
        /// <example>
        /// Creates a container as an asynchronous operation in the Azure Cosmos service and return stream response.
        /// <code language="c#">
        /// <![CDATA[
        /// ContainerProperties containerProperties = new ContainerProperties()
        /// {
        ///     Id = Guid.NewGuid().ToString(),
        ///     PartitionKeyPath = "/pk",
        /// };
        ///
        /// using(ResponseMessage response = await this.cosmosDatabase.CreateContainerStreamAsync(containerProperties))
        /// {
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <seealso cref="DefineContainer(string, string)"/>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units">Request Units</seealso>
        public abstract Task<ResponseMessage> CreateContainerStreamAsync(
            ContainerProperties containerProperties,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Returns a reference to a user object.
        /// </summary>
        /// <param name="id">The Cosmos user id.</param>
        /// <returns>Cosmos user reference</returns>
        /// <remarks>
        /// Returns a User reference. Reference doesn't guarantee existence.
        /// Please ensure user already exists or is created through a create operation.
        /// </remarks>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// Database db = this.cosmosClient.GetDatabase("myDatabaseId");
        /// User user = await db.GetUser("userId");
        /// UserResponse response = await user.ReadAsync();
        /// ]]>
        /// </code>
        /// </example>
        public abstract User GetUser(string id);

        /// <summary>
        /// Creates a user as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <param name="id">The Cosmos user id</param>
        /// <param name="requestOptions">(Optional) The options for the user request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="UserResponse"/> which wraps a <see cref="UserProperties"/> containing the read resource record.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="id"/> is not set.</exception>
        /// <exception cref="CosmosException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a user are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the request supplied. It is likely that an id was not supplied for the new user.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a <see cref="UserProperties"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// UserResponse response = await this.cosmosDatabase.CreateUserAsync(Guid.NewGuid().ToString());
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<UserResponse> CreateUserAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Upserts a user as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <param name="id">The Cosmos user id.</param>
        /// <param name="requestOptions">(Optional) The options for the user request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="UserResponse"/> which wraps a <see cref="UserProperties"/> containing the read resource record.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="id"/> is not set.</exception>
        /// <exception cref="CosmosException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a user are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the request supplied. It is likely that an id was not supplied for the new user.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// UserResponse response = await this.cosmosDatabase.UpsertUserAsync(Guid.NewGuid().ToString());
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<UserResponse> UpsertUserAsync(string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// This method creates a query for containers under an database using a SQL statement. It returns a FeedIterator.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryDefinition">The Cosmos SQL query definition.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request.</param>
        /// <returns>An iterator to go through the containers</returns>
        /// <example>
        /// This create the type feed iterator for containers with queryDefinition as input.
        /// <code language="c#">
        /// <![CDATA[
        /// QueryDefinition queryDefinition = new QueryDefinition("SELECT * FROM c where c.status like @status");
        ///     .WithParameter("@status", "start%");
        /// FeedIterator<ContainerProperties> feedIterator = this.cosmosDatabase.GetContainerQueryIterator<ContainerProperties>(queryDefinition);
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     FeedResponse<ContainerProperties> response = await feedIterator.ReadNextAsync();
        ///     foreach (var container in response)
        ///     {
        ///         Console.WriteLine(container);
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// Refer to https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.
        /// <para>
        /// <see cref="Container.ReadContainerAsync(ContainerRequestOptions, CancellationToken)" /> is recommended for single container look-up.
        /// </para>
        /// </remarks>
        public abstract FeedIterator<T> GetContainerQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);

        /// <summary>
        /// This method creates a query for containers under an database using a SQL statement. It returns a FeedIterator.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryDefinition">The Cosmos SQL query definition.</param>
        /// <param name="continuationToken">The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the container request.</param>
        /// <returns>An iterator to go through the containers</returns>
        /// <example>
        /// This create the stream feed iterator for containers with queryDefinition as input.
        /// <code language="c#">
        /// <![CDATA[
        /// string queryText = "SELECT * FROM c where c.status like 'start%'";
        /// QueryDefinition queryDefinition = new QueryDefinition(queryText);
        /// FeedIterator feedIterator = this.cosmosDatabase.GetContainerQueryStreamIterator(queryDefinition);
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
        /// <remarks>
        /// Refer to https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.
        /// <para>
        /// <see cref="Container.ReadContainerStreamAsync(ContainerRequestOptions, CancellationToken)" /> is recommended for single container look-up.
        /// </para>
        /// </remarks>
        public abstract FeedIterator GetContainerQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);

        /// <summary>
        /// This method creates a query for containers under an database using a SQL statement. It returns a FeedIterator.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryText">The Cosmos SQL query text.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the item query request.</param>
        /// <returns>An iterator to go through the containers</returns>
        /// <example>
        /// 1. This create the type feed iterator for containers with queryText as input,
        /// <code language="c#">
        /// <![CDATA[
        /// string queryText = "SELECT * FROM c where c.status like 'start%'";
        /// FeedIterator<ContainerProperties> feedIterator = this.cosmosDatabase.GetContainerQueryIterator<ContainerProperties>(queryText);
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     FeedResponse<ContainerProperties> response = await feedIterator.ReadNextAsync();
        ///     foreach (var container in response)
        ///     {
        ///         Console.WriteLine(container);
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// 2. This create the type feed iterator for containers without queryText, retrieving all containers.
        /// <code language="c#">
        /// <![CDATA[
        /// FeedIterator<ContainerProperties> feedIterator = this.cosmosDatabase.GetContainerQueryIterator<ContainerProperties>();
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     FeedResponse<ContainerProperties> response = await feedIterator.ReadNextAsync();
        ///     foreach (var container in response)
        ///     {
        ///         Console.WriteLine(container);
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// Refer to https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.
        /// <para>
        /// <see cref="Container.ReadContainerAsync(ContainerRequestOptions, CancellationToken)" /> is recommended for single container look-up.
        /// </para>
        /// </remarks>
        public abstract FeedIterator<T> GetContainerQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);

        /// <summary>
        /// This method creates a query for containers under an database using a SQL statement. It returns a FeedIterator.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryText">The Cosmos SQL query text.</param>
        /// <param name="continuationToken">The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the container request.</param>
        /// <returns>An iterator to go through the containers</returns>
        /// <example>
        /// 1. This create the stream feed iterator for containers with queryText as input.
        /// <code language="c#">
        /// <![CDATA[
        /// FeedIterator feedIterator = this.Container.GetItemQueryStreamIterator(
        ///     "SELECT * FROM c where c.status like 'start%'");
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     // Stream iterator returns a response with status for errors
        ///     using(ResponseMessage response = await feedIterator.ReadNextAsync())
        ///     {
        ///         // Handle failure scenario. 
        ///         if(!response.IsSuccessStatusCode)
        ///         {
        ///             // Log the response.Diagnostics and handle the error
        ///             throw new Exception(response.Message);
        ///         }
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// 2. This create the stream feed iterator for containers without queryText, retrieving all container.
        /// <code language="c#">
        /// <![CDATA[
        /// FeedIterator feedIterator = this.cosmosDatabase.GetContainerQueryStreamIterator();
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     // Stream iterator returns a response with status
        ///     using(ResponseMessage response = await feedIterator.ReadNextAsync())
        ///     {
        ///         // Handle failure scenario. 
        ///         if(!response.IsSuccessStatusCode)
        ///         {
        ///             // Log the response.Diagnostics and handle the error
        ///             throw new Exception(response.Message);
        ///         }
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <remarks>
        /// Refer to https://docs.microsoft.com/azure/cosmos-db/sql-query-getting-started for syntax and examples.
        /// <para>
        /// <see cref="Container.ReadContainerStreamAsync(ContainerRequestOptions, CancellationToken)" /> is recommended for single container look-up.
        /// </para>
        /// </remarks>
        public abstract FeedIterator GetContainerQueryStreamIterator(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);

        /// <summary>
        /// This method creates a query for users under an database using a SQL statement. It returns a FeedIterator.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryText">The Cosmos SQL query text.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the user query request.</param>
        /// <returns>An iterator to go through the users</returns>
        /// <example>
        /// 1. This create the type feed iterator for users with queryText as input,
        /// <code language="c#">
        /// <![CDATA[
        /// string queryText = "SELECT * FROM c where c.status like 'start%'";
        /// FeedIterator<UserProperties> HasMoreResults = this.cosmosDatabase.GetUserQueryIterator<UserProperties>(queryText);
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     FeedResponse<UserProperties> response = await feedIterator.ReadNextAsync();
        ///     foreach (var user in response)
        ///     {
        ///         Console.WriteLine(user);
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// 2. This create the type feed iterator for users without queryText, retrieving all users.
        /// <code language="c#">
        /// <![CDATA[
        /// FeedIterator<UserProperties> feedIterator = this.cosmosDatabase.GetUserQueryIterator<ContainerProperties>();
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     FeedResponse<UserProperties> response = await feedIterator.ReadNextAsync();
        ///     foreach (var user in response)
        ///     {
        ///         Console.WriteLine(user);
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract FeedIterator<T> GetUserQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);

        /// <summary>
        /// This method creates a query for users under an database using a SQL statement. It returns a FeedIterator.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryDefinition">The Cosmos SQL query definition.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the user query request.</param>
        /// <returns>An iterator to go through the users</returns>
        /// <example>
        /// This create the type feed iterator for users with queryDefinition as input.
        /// <code language="c#">
        /// <![CDATA[
        /// QueryDefinition queryDefinition = new QueryDefinition("SELECT * FROM c where c.status like @status")
        ///     .WithParameter("@status", "start%");
        /// FeedIterator<UserProperties> resultSet = this.cosmosDatabase.GetUserQueryIterator<UserProperties>(queryDefinition);
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     foreach (UserProperties properties in await feedIterator.ReadNextAsync())
        ///     {
        ///         Console.WriteLine(properties.Id);
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract FeedIterator<T> GetUserQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);

        /// <summary>
        /// Creates a containerBuilder.
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
        public abstract ContainerBuilder DefineContainer(
            string name,
            string partitionKeyPath);
    }
}
