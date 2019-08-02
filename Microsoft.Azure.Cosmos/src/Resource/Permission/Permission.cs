//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Operations for reading, replacing, or deleting a specific, existing permission by id.
    /// </summary>
    public abstract class Permission
    {
        /// <summary>
        /// The Id of the Cosmos user
        /// </summary>
        public abstract string Id { get; }

        /// <summary>
        /// Reads a <see cref="PermissionProperties"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the permission request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="PermissionResponse"/> which wraps a <see cref="PermissionProperties"/> containing the read resource record.
        /// </returns>
        /// <exception cref="CosmosException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a permission are:
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
        /// Permission permission= user.GetPermission("permissionId");
        /// PermissionProperties permissionProperties = await user.ReadPermissionAsync();
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<PermissionResponse> ReadPermissionAsync(
            PermissionRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Reads a <see cref="PermissionProperties"/> from the Azure Cosmos service as an asynchronous operation.
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
        /// Permission permission = this.user.GetPermission("permissionId");
        /// ResponseMessage response = await permission.ReadPermissionStreamAsync();
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<ResponseMessage> ReadPermissionStreamAsync(
            PermissionRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Replace a <see cref="PermissionProperties"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="permissionProperties">The <see cref="PermissionProperties"/> object.</param>
        /// <param name="requestOptions">(Optional) The options for the user request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="PermissionResponse"/> which wraps a <see cref="PermissionProperties"/> containing the replace resource record.
        /// </returns>
        /// <exception cref="CosmosException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a permission are:
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
        /// PermissionProperties permissionProperties = permissionReadResponse;
        /// permissionProperties.Id = "newuser";
        /// PermissionResponse response = await permission.ReplacePermissionAsync(permissionProperties);
        /// PermissionProperties replacedProperties = response;
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<PermissionResponse> ReplacePermissionAsync(
            PermissionProperties permissionProperties,
            PermissionRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Replace a <see cref="PermissionProperties"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="permissionProperties">The <see cref="PermissionProperties"/>.</param>
        /// <param name="requestOptions">(Optional) The options for the user request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="ResponseMessage"/> containing the replace resource record.
        /// </returns>
        /// <example>
        ///
        /// <code language="c#">
        /// <![CDATA[
        /// PermissionProperties permissionProperties = permissionReadResponse;
        /// permissionProperties.Id = "newuser";
        /// ResponseMessage response = await user.ReplacePermissionStreamAsync(permissionProperties);
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<ResponseMessage> ReplacePermissionStreamAsync(
            PermissionProperties permissionProperties,
            PermissionRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Delete a <see cref="PermissionProperties"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the user request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="PermissionResponse"/> which will contain information about the request issued.</returns>
        /// <exception cref="CosmosException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a permission are:
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
        /// Permission permission = user.GetPermission("permissionId");
        /// PermissionResponse response = await permission.DeletePermissionAsync();
        /// ]]>
        /// </code>
        /// </example>
        public abstract Task<PermissionResponse> DeletePermissionAsync(
            PermissionRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Delete a <see cref="PermissionProperties"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the user request <see cref="RequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// User user = this.database.GetUser("userId");
        /// Permission permission = user.GetPermission("permissionId");
        /// ResponseMessage response = await permission.DeletePermissionStreamAsync();
        /// ]]>
        /// </code>
        /// </example>
        /// <returns>A <see cref="Task"/> containing a <see cref="ResponseMessage"/> which will contain information about the request issued.</returns>
        public abstract Task<ResponseMessage> DeletePermissionStreamAsync(
            PermissionRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));
    }
}
