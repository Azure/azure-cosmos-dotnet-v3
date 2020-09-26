//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Operations for reading, replacing, or deleting a specific permission by id. Permissions are used to create ResourceTokens. Resource tokens provide access to the application resources within a database. Resource tokens:
    /// <list type="bullet">
    /// <item>
    /// <description>Provide access to specific containers, partition keys, documents, attachments, stored procedures, triggers, and UDFs.</description>
    /// </item>
    /// <item>
    /// <description>Are created when a user is granted permissions to a specific resource.</description>
    /// </item>
    /// <item>
    /// <description>Are recreated when a permission resource is acted upon on by POST, GET, or PUT call.</description>
    /// </item>
    /// <item>
    /// <description>Use a hash resource token specifically constructed for the user, resource, and permission.</description>
    /// </item>
    /// <item>
    /// <description>Are time bound with a customizable validity period. The default valid timespan is one hour. Token lifetime, however, may be explicitly specified, up to a maximum of 24 hours.</description>
    /// </item>
    /// <item>
    /// <description>Provide a safe alternative to giving out the master key.</description>
    /// </item>
    /// <item>
    /// <description>Enable clients to read, write, and delete resources in the Cosmos DB account according to the permissions they've been granted.</description>
    /// </item>
    /// </list>
    /// </summary>
    public abstract class Permission
    {
        /// <summary>
        /// The Id of the Cosmos Permission
        /// </summary>
        public abstract string Id { get; }

        /// <summary>
        /// Reads a <see cref="PermissionProperties"/> from the Azure Cosmos service as an asynchronous operation. Each read will return a new ResourceToken with its respective expiration. 
        /// </summary>
        /// <param name="tokenExpiryInSeconds">(Optional) The expiry time for resource token in seconds. This value can range from 10 seconds, to 24 hours (or 86,400 seconds). The default value for this is 1 hour (or 3,600 seconds). This does not change the default value for future tokens.</param>
        /// <param name="requestOptions">(Optional) The options for the permission request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="PermissionResponse"/> which wraps a <see cref="PermissionProperties"/> containing the read resource record.
        /// </returns>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions</exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// User user = this.database.GetUser("userId");
        /// Permission permission= user.GetPermission("permissionId");
        /// PermissionProperties permissionProperties = await permission.ReadAsync(tokenExpiryInSeconds: 9000);
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<PermissionResponse> ReadAsync(
            int? tokenExpiryInSeconds = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Replace a <see cref="PermissionProperties"/> from the Azure Cosmos service as an asynchronous operation. This will not revoke existing ResourceTokens.
        /// </summary>
        /// <param name="permissionProperties">The <see cref="PermissionProperties"/> object.</param>
        /// <param name="tokenExpiryInSeconds">(Optional) The expiry time for resource token in seconds. This value can range from 10 seconds, to 24 hours (or 86,400 seconds). The default value for this is 1 hour (or 3,600 seconds). This does not change the default value for future tokens.</param>
        /// <param name="requestOptions">(Optional) The options for the user request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="PermissionResponse"/> which wraps a <see cref="PermissionProperties"/> containing the replace resource record.
        /// </returns>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions</exception>
        /// <example>        
        /// <code language="c#">
        /// <![CDATA[
        /// PermissionProperties permissionProperties = permissionReadResponse;
        /// permissionProperties.Id = "newuser";
        /// PermissionResponse response = await permission.ReplaceAsync(permissionProperties, tokenExpiryInSeconds: 9000);
        /// PermissionProperties replacedProperties = response;
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<PermissionResponse> ReplaceAsync(
            PermissionProperties permissionProperties,
            int? tokenExpiryInSeconds = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete a <see cref="PermissionProperties"/> from the Azure Cosmos DB service as an asynchronous operation. This will not revoke existing ResourceTokens.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the user request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="PermissionResponse"/> which will contain information about the request issued.</returns>
        /// <exception>https://aka.ms/cosmosdb-dot-net-exceptions</exception>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// User user = this.database.GetUser("userId");
        /// Permission permission = user.GetPermission("permissionId");
        /// PermissionResponse response = await permission.DeleteAsync();
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<PermissionResponse> DeleteAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default);
    }
}
