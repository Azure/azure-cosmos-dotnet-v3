//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

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
    public abstract partial class CosmosDatabase
    {
        /// <summary>
        /// The Id of the Cosmos database
        /// </summary>
        public abstract string Id { get;  }

        /// <summary>
        /// Reads a <see cref="CosmosDatabaseSettings"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the container request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="DatabaseResponse"/> which wraps a <see cref="CosmosDatabaseSettings"/> containing the read resource record.
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
        /// Delete a <see cref="CosmosDatabaseSettings"/> from the Azure Cosmos DB service as an asynchronous operation.
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
        /// Gets provisioned database throughput in measurement of Requests-per-Unit in the Azure Cosmos service.
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
        /// <returns>The current provisioned throughput if any.</returns>
        public abstract Task<int?> ReadProvisionedThroughputAsync(
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Sets throughput provisioned for a database in measurement of Requests-per-Unit in the Azure Cosmos service.
        /// </summary>
        /// <param name="requestUints">The cosmos database throughput.</param>
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
        /// int? throughput = await this.cosmosDatabase.ReplaceProvisionedThroughputAsync(10000);
        /// ]]>
        /// </code>
        /// </example>
        /// <returns>A Task representing the asynchronoous operation.</returns>
        public abstract Task ReplaceProvisionedThroughputAsync(
            int requestUints,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Reads a <see cref="CosmosDatabaseSettings"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the container request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="CosmosResponseMessage"/> containing the read resource record.
        /// </returns>
        public abstract Task<CosmosResponseMessage> ReadAsStreamAsync(
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Delete a <see cref="CosmosDatabaseSettings"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the container request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="CosmosResponseMessage"/> which will contain information about the request issued.</returns>
        public abstract Task<CosmosResponseMessage> DeleteAsStreamAsync(
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken));
    }
}
