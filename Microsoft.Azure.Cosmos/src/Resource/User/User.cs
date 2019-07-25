//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
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
    }
}
