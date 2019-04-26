//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Operations for reading, replacing, or deleting a specific, existing user defined functions by id.
    /// 
    /// <see cref="CosmosUserDefinedFunctions"/> for creating new user defined functions, and reading/querying all user defined functions;
    /// </summary>
    internal class CosmosUserDefinedFunction : CosmosIdentifier
    {
        /// <summary>
        /// Create a <see cref="CosmosUserDefinedFunction"/>
        /// </summary>
        /// <param name="container">The <see cref="CosmosContainer"/></param>
        /// <param name="userDefinedFunctionId">The cosmos user defined function id.</param>
        /// <remarks>
        /// Note that the user defined function must be explicitly created, if it does not already exist, before
        /// you can read from it or write to it.
        /// </remarks>
        protected internal CosmosUserDefinedFunction(
            CosmosContainerCore container,
            string userDefinedFunctionId)
        {
            this.Id = userDefinedFunctionId;
            this.container = container;
            base.Initialize(
               client: container.Client,
               parentLink: container.LinkUri.OriginalString,
               uriPathSegment: Paths.UserDefinedFunctionsPathSegment);
        }

        public override string Id { get; }

        /// <summary>
        /// Reads a <see cref="CosmosUserDefinedFunctionSettings"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the user defined function request <see cref="CosmosRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="CosmosUserDefinedFunctionResponse"/> which wraps a <see cref="CosmosUserDefinedFunctionSettings"/> containing the read resource record.
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
        ///  This reads an existing user defined function.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosUserDefinedFunctionResponse response = await cosmosContainer.UserDefinedFunctions["ExistingId"].ReadAsync();
        /// CosmosUserDefinedFunctionSettings settings = response;
        /// ]]>
        /// </code>
        /// </example>
        public virtual Task<CosmosUserDefinedFunctionResponse> ReadAsync(
            CosmosRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessAsync(
                partitionKey: null,
                streamPayload: null,
                operationType: OperationType.Read,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Replaces a <see cref="CosmosUserDefinedFunctionSettings"/> in the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="userDefinedFunctionSettings">The <see cref="CosmosUserDefinedFunctionSettings"/> object.</param>
        /// <param name="requestOptions">(Optional) The options for the user defined function request <see cref="CosmosRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="CosmosUserDefinedFunctionResponse"/> which wraps a <see cref="CosmosUserDefinedFunctionSettings"/> containing the updated resource record.
        /// </returns>
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
        /// This examples replaces an existing user defined function.
        /// <code language="c#">
        /// <![CDATA[
        /// //Updated settings
        /// CosmosUserDefinedFunctionSettings settings = new CosmosUserDefinedFunctionSettings
        /// {
        ///     Id = "testUserDefinedFunId",
        ///     Body = "function(amt) { return amt * 0.15; }",
        /// };
        /// 
        /// CosmosUserDefinedFunctionResponse response = await this.cosmosUserDefinedFunction.ReplaceAsync(settings);
        /// CosmosUserDefinedFunctionSettings settings = response;
        /// ]]>
        /// </code>
        /// </example>
        public virtual Task<CosmosUserDefinedFunctionResponse> ReplaceAsync(
            CosmosUserDefinedFunctionSettings userDefinedFunctionSettings,
            CosmosRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessAsync(
                partitionKey: null,
                streamPayload: CosmosResource.ToStream(userDefinedFunctionSettings),
                operationType: OperationType.Replace,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Delete a <see cref="CosmosUserDefinedFunctionSettings"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the user defined function request <see cref="CosmosRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="CosmosUserDefinedFunctionResponse"/> which wraps a <see cref="CosmosUserDefinedFunctionSettings"/> which will contain information about the request issued.</returns>
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
        /// This examples gets a reference to an existing user defined function and deletes it.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosUserDefinedFunctionResponse response = await this.cosmosContainer.UserDefinedFunctions["taxUdfId"].DeleteAsync();
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// This examples containers an existing reference to a user defined function and deletes it.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosUserDefinedFunctionResponse response = await this.cosmosTaxUdf.DeleteAsync();
        /// ]]>
        /// </code>
        /// </example>
        public virtual Task<CosmosUserDefinedFunctionResponse> DeleteAsync(
            CosmosRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessAsync(
                partitionKey: null,
                streamPayload: null,
                operationType: OperationType.Delete,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        internal virtual Task<CosmosUserDefinedFunctionResponse> ProcessAsync(
            object partitionKey,
            Stream streamPayload,
            OperationType operationType,
            CosmosRequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            Task<CosmosResponseMessage> response = ExecUtils.ProcessResourceOperationStreamAsync(
                this.Client,
                this.LinkUri,
                ResourceType.UserDefinedFunction,
                operationType,
                requestOptions,
                this.container,
                partitionKey,
                streamPayload,
                null,
                cancellationToken);

            return this.Client.ResponseFactory.CreateUserDefinedFunctionResponse(this, response);
        }
        internal CosmosContainerCore container { get; }
    }
}
