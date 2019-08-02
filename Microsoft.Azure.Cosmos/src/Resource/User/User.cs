//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Operations for reading, replacing, or deleting a specific, existing user by id.   
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
        /// <param name="requestOptions">(Optional) The options for the user request <see cref="RequestOptions"/></param>
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
        /// User user = this.database.GetUser("userId");
        /// UserProperties userProperties = await user.ReadUserAsync();
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<UserResponse> ReadUserAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Reads a <see cref="UserProperties"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the user request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="ResponseMessage"/> containing the read resource record.
        /// </returns>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// User user = this.database.GetUser("userId");
        /// ResponseMessage response = await user.ReadUserStreamAsync();
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<ResponseMessage> ReadUserStreamAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Replace a <see cref="UserProperties"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="userProperties">The <see cref="UserProperties"/> object.</param>
        /// <param name="requestOptions">(Optional) The options for the user request <see cref="RequestOptions"/></param>
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
        /// UserProperties userProperties = userReadResponse;
        /// userProperties.Id = "newuser";
        /// UserResponse response = await user.ReplaceUserAsync(userProperties);
        /// UserProperties replacedProperties = response;
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<UserResponse> ReplaceUserAsync(
            UserProperties userProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Replace a <see cref="UserProperties"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="userProperties">The <see cref="UserProperties"/>.</param>
        /// <param name="requestOptions">(Optional) The options for the user request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="ResponseMessage"/> containing the replace resource record.
        /// </returns>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// UserProperties userProperties = userReadResponse;
        /// userProperties.Id = "newuser";
        /// ResponseMessage response = await user.ReplaceUserStreamAsync(userProperties);
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<ResponseMessage> ReplaceUserStreamAsync(
            UserProperties userProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Delete a <see cref="UserProperties"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the user request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="UserResponse"/> which will contain information about the request issued.</returns>
        /// <exception cref="CosmosException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a user are:
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
        /// User user = this.database.GetUser("userId");
        /// UserResponse response = await user.DeleteUserAsync();
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<UserResponse> DeleteUserAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Delete a <see cref="UserProperties"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the user request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// User user = this.database.GetUser("userId");
        /// ResponseMessage response = await user.DeleteUserStreamAsync();
        /// ]]>
        /// </code>
        /// </example>
        /// <returns>A <see cref="Task"/> containing a <see cref="ResponseMessage"/> which will contain information about the request issued.</returns>
        public abstract Task<ResponseMessage> DeleteUserStreamAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Returns a reference to a permission object. 
        /// </summary>
        /// <param name="id">The cosmos permission id.</param>
        /// <returns>Cosmos permission reference</returns>
        /// <remarks>
        /// Returns a Permission reference. Reference doesn't guarantees existence.
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
        /// <param name="requestOptions">(Optional) The options for the permission request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="PermissionResponse"/> which wraps a <see cref="PermissionProperties"/> containing the read resource record.</returns>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occurred during async processing. Look within InnerExceptions to find the actual exception(s).</exception>
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
        /// PermissionProperties permissionProperties = PermissionProperties.CreateDatabasePermission();
        /// 
        /// PermissionResponse response = await this.cosmosDatabase.GetUser("userId").CreatePermissionAsync(permissionProperties);
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<PermissionResponse> CreatePermissionAsync(
            PermissionProperties permissionProperties,
            PermissionRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Creates a permission as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <param name="permissionProperties">The <see cref="PermissionProperties"/> object.</param>
        /// <param name="requestOptions">(Optional) The options for the user request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="ResponseMessage"/> containing the created resource record.</returns>
        /// <example>
        /// Creates a permission as an asynchronous operation in the Azure Cosmos service and return stream response.
        /// <code language="c#">
        /// <![CDATA[
        /// PermissionProperties permissionProperties = PermissionProperties.CreateDatabasePermission();
        ///
        /// using(ResponseMessage response = await this.cosmosDatabase.GetUser("userId").CreatePermissionStreamAsync(permissionProperties))
        /// {
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<ResponseMessage> CreatePermissionStreamAsync(
            PermissionProperties permissionProperties,
            PermissionRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// This method creates a query for permissions under a database using a SQL statement. It returns a FeedIterator.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryDefinition">The cosmos SQL query definition.</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the user query request <see cref="QueryRequestOptions"/></param>
        /// <returns>An iterator to go through the permissions</returns>
        /// <example>
        /// This create the type feed iterator for permissions with queryDefinition as input.
        /// <code language="c#">
        /// <![CDATA[
        /// string queryText = "SELECT * FROM c where c.id like @testId";
        /// QueryDefinition queryDefinition = new QueryDefinition(queryText);
        /// queryDefinition.WithParameter("@testId", "testPermissionId");
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

        /// <summary>
        /// This method creates a query for permissions under a database using a SQL statement. It returns a FeedIterator.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryDefinition">The cosmos SQL query definition.</param>
        /// <param name="continuationToken">The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the user query request <see cref="QueryRequestOptions"/></param>
        /// <returns>An iterator to go through the permissions</returns>
        /// <example>
        /// This create the stream feed iterator for permissions with queryDefinition as input.
        /// <code language="c#">
        /// <![CDATA[
        /// string queryText = "SELECT * FROM c where c.id like '%testId%'";
        /// QueryDefinition queryDefinition = new QueryDefinition(queryText);
        /// FeedIterator resultSet = this.cosmosDatabase.GetUserQueryStreamIterator(queryDefinition);
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
        public abstract FeedIterator GetPermissionQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);

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
        /// string queryText = "SELECT * FROM c where c.id like '%testId%'";
        /// FeedIterator<PermissionProperties> resultSet = this.users.GetPermissionQueryIterator<PermissionProperties>(queryText);
        /// while (feedIterator.HasMoreResults)
        /// {
        /// FeedResponse<PermissionProperties> iterator =
        /// await feedIterator.ReadNextAsync(this.cancellationToken);
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// 2. This create the type feed iterator for permissions without queryText, retrieving all permissions.
        /// <code language="c#">
        /// <![CDATA[
        /// FeedIterator<PermissionProperties> resultSet = this.user.GetPermissionQueryIterator<PermissionProperties>();
        /// while (feedIterator.HasMoreResults)
        /// {
        /// FeedResponse<PermissionProperties> iterator =
        /// await feedIterator.ReadNextAsync(this.cancellationToken);
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract FeedIterator<T> GetPermissionQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);

        /// <summary>
        /// This method creates a query for permission under a user using a SQL statement. It returns a FeedIterator.
        /// For more information on preparing SQL statements with parameterized values, please see <see cref="QueryDefinition"/> overload.
        /// </summary>
        /// <param name="queryText">The cosmos SQL query text.</param>
        /// <param name="continuationToken">The continuation token in the Azure Cosmos DB service.</param>
        /// <param name="requestOptions">(Optional) The options for the user query request <see cref="QueryRequestOptions"/></param>
        /// <returns>An iterator to go through the permissions</returns>
        /// <example>
        /// 1. This create the stream feed iterator for permissions with queryText as input.
        /// <code language="c#">
        /// <![CDATA[
        /// string queryText = "SELECT * FROM c where c.id like '%testId%'";
        /// FeedIterator resultSet = this.user.GetPermissionQueryStreamIterator(queryText);
        /// while (feedIterator.HasMoreResults)
        /// {
        /// ResponseMessage iterator =
        /// await feedIterator.ReadNextAsync(this.cancellationToken);
        /// }
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// 2. This create the stream feed iterator for permissions without queryText, retrieving all container.
        /// <code language="c#">
        /// <![CDATA[
        /// FeedIterator resultSet = this.users.GetPermissionQueryStreamIterator();
        /// while (feedIterator.HasMoreResults)
        /// {
        /// ResponseMessage iterator =
        /// await feedIterator.ReadNextAsync(this.cancellationToken);
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public abstract FeedIterator GetPermissionQueryStreamIterator(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);
    }
}
