//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Operations for reading, replacing, or deleting a specific existing user by id and query a user's permissions.
    /// </summary>
    public abstract class User
    {
        /// <summary>
        /// The Id of the Cosmos user
        /// </summary>
        public abstract string Id { get; }

        /// <summary>
        /// Reads a <see cref="UserProperties"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the user request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="UserResponse"/> which wraps a <see cref="UserProperties"/> containing the read resource record.
        /// </returns>
        /// <exception cref="CosmosException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a user are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource or parent resource you tried to read did not exist.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// User user = this.database.GetUser("userId");
        /// UserProperties userProperties = await user.ReadUserAsync();
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<UserResponse> ReadAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Replace a <see cref="UserProperties"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="userProperties">The <see cref="UserProperties"/> object.</param>
        /// <param name="requestOptions">(Optional) The options for the user request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="UserResponse"/> which wraps a <see cref="UserProperties"/> containing the replace resource record.
        /// </returns>
        /// <exception cref="CosmosException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a user are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource or parent resource you tried to read did not exist.</description>
        ///     </item>
        ///     <item>
        ///         <term>429</term><description>TooManyRequests - This means you have exceeded the number of request units per second. Consult the DocumentClientException.RetryAfter value to see how long you should wait before retrying this operation.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// UserProperties userProperties = userReadResponse;
        /// userProperties.Id = "newuser";
        /// UserResponse response = await user.ReplaceUserAsync(userProperties);
        /// UserProperties replacedProperties = response;
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<UserResponse> ReplaceAsync(
            UserProperties userProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete a <see cref="UserProperties"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the user request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="UserResponse"/> which will contain information about the request issued.</returns>
        /// <exception cref="CosmosException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a user are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource or parent resource you tried to delete did not exist.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// User user = this.database.GetUser("userId");
        /// UserResponse response = await user.DeleteUserAsync();
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<UserResponse> DeleteAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns a reference to a permission object. 
        /// </summary>
        /// <param name="id">The cosmos permission id.</param>
        /// <returns>Cosmos permission reference</returns>
        /// <remarks>
        /// Returns a Permission reference. Reference doesn't guarantee existence.
        /// Please ensure permssion already exists or is created through a create operation.
        /// </remarks>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// User user = this.cosmosClient.GetDatabase("myDatabaseId").GetUser("userId");
        /// PermissionResponse response = await user.GetPermssion("permissionId");
        /// ]]>
        /// </code>
        /// </example>
        public abstract Permission GetPermission(string id);

        /// <summary>
        /// Creates a permission as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <param name="permissionProperties">The <see cref="PermissionProperties"/> object.</param>
        /// <param name="tokenExpiryInSeconds">(Optional) The expiry time for resource token in seconds. This value can range from 10 seconds, to 24 hours (or 86,400 seconds). The default value for this is 1 hour (or 3,600 seconds). This does not change the default value for future tokens.</param>
        /// <param name="requestOptions">(Optional) The options for the permission request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="PermissionResponse"/> which wraps a <see cref="PermissionProperties"/> containing the read resource record.</returns>
        /// <exception cref="CosmosException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a permission are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the request supplied. It is likely that an id was not supplied for the new permission.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a <see cref="PermissionProperties"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// PermissionProperties permissionProperties = new PermissionProperties("permissionId", PermissionMode.All, database.GetContainer("containerId"), new PartitionKey("tenantId"))";
        /// 
        /// PermissionResponse response = await this.cosmosDatabase.GetUser("userId").CreatePermissionAsync(permissionProperties, tokenExpiryInSeconds: 9000);
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<PermissionResponse> CreatePermissionAsync(
            PermissionProperties permissionProperties,
            int? tokenExpiryInSeconds = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Upsert a permission as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <param name="permissionProperties">The <see cref="PermissionProperties"/> object.</param>
        /// <param name="tokenExpiryInSeconds">(Optional) The expiry time for resource token in seconds. This value can range from 10 seconds, to 24 hours (or 86,400 seconds). The default value for this is 1 hour (or 3,600 seconds). This does not change the default value for future tokens.</param>
        /// <param name="requestOptions">(Optional) The options for the permission request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="PermissionResponse"/> which wraps a <see cref="PermissionProperties"/> containing the read resource record.</returns>
        /// <exception cref="CosmosException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a permission are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the request supplied. It is likely that an id was not supplied for the new permission.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// PermissionProperties permissionProperties = new PermissionProperties("permissionId", PermissionMode.All, database.GetContainer("containerId"), new PartitionKey("tenantId"))";
        /// 
        /// PermissionResponse response = await this.cosmosDatabase.GetUser("userId").UpsertPermissionAsync(permissionProperties, tokenExpiryInSeconds: 9000);
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<PermissionResponse> UpsertPermissionAsync(
            PermissionProperties permissionProperties,
            int? tokenExpiryInSeconds = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// This method creates a query for permission under a user using a SQL statement. It returns a FeedIterator.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryText">The cosmos SQL query text.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the user query request <see cref="QueryRequestOptions"/></param>
        /// <returns>An iterator to go through the permission</returns>
        /// <example>
        /// 1. This create the type feed iterator for permission with queryText as input,
        /// <code language="c#">
        /// <![CDATA[
        /// string queryText = "SELECT * FROM c where c.status like 'start%'";
        /// FeedIterator<PermissionProperties> feedIterator = this.users.GetPermissionQueryIterator<PermissionProperties>(queryText);
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     FeedResponse<PermissionProperties> response = await feedIterator.ReadNextAsync();
        ///     foreach (var permission in response)
        ///     {
        ///         Console.WriteLine(permission);
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// 2. This create the type feed iterator for permissions without queryText, retrieving all permissions.
        /// <code language="c#">
        /// <![CDATA[
        /// FeedIterator<PermissionProperties> feedIterator = this.user.GetPermissionQueryIterator<PermissionProperties>();
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     FeedResponse<PermissionProperties> response = await feedIterator.ReadNextAsync();
        ///     foreach (var permission in response)
        ///     {
        ///         Console.WriteLine(permission);
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract FeedIterator<T> GetPermissionQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);

        /// <summary>
        /// This method creates a query for permissions under a database using a SQL statement. It returns a FeedIterator.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <remarks>
        /// Reading permissions will generate a new ResourceTokens. Prior ResourceTokens will still be valid.
        /// </remarks>
        /// <param name="queryDefinition">The cosmos SQL query definition.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the user query request <see cref="QueryRequestOptions"/></param>
        /// <returns>An iterator to go through the permissions</returns>
        /// <example>
        /// This create the type feed iterator for permissions with queryDefinition as input.
        /// <code language="c#">
        /// <![CDATA[
        /// QueryDefinition queryDefinition = new QueryDefinition("SELECT * FROM c where c.status like @status")
        ///     .WithParameter("@status", "start%");
        /// FeedIterator<PermissionProperties> resultSet = this.user.GetPermissionQueryIterator<PermissionProperties>(queryDefinition);
        /// while (feedIterator.HasMoreResults)
        /// {
        ///     foreach (PermissionProperties properties in await feedIterator.ReadNextAsync())
        ///     {
        ///         Console.WriteLine(properties.Id);
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract FeedIterator<T> GetPermissionQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);
    }
}
