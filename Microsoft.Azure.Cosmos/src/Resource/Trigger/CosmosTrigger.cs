//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Internal;

    /// <summary>
    /// Operations for reading, replacing, or deleting a specific, existing triggers by id.
    /// 
    /// <see cref="CosmosTriggers"/> for creating new triggers, and reading/querying all triggers;
    /// </summary>
    internal class CosmosTrigger : CosmosIdentifier
    {
        /// <summary>
        /// Create a <see cref="CosmosTrigger"/>
        /// </summary>
        /// <param name="container">The <see cref="CosmosContainer"/></param>
        /// <param name="triggerId">The cosmos trigger id.</param>
        /// <remarks>
        /// Note that the trigger must be explicitly created, if it does not already exist, before
        /// you can read from it or write to it.
        /// </remarks>
        protected internal CosmosTrigger(
            CosmosContainer container,
            string triggerId)
        {
            this.Id = triggerId;
            this.Client = container.Client;
            this.LinkUri = GetLink(container.LinkUri.OriginalString, Paths.TriggersPathSegment);
        }

        public override string Id { get; }

        internal override CosmosClient Client { get; }

        internal override Uri LinkUri { get; }

        /// <summary>
        /// Reads a <see cref="CosmosTriggerSettings"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the trigger request <see cref="CosmosRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="CosmosTriggerResponse"/> which wraps a <see cref="CosmosTriggerSettings"/> containing the read resource record.
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
        ///  This reads an existing trigger
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosTriggerResponse response = await cosmosContainer.Triggers["ExistingId"].ReadAsync();
        /// CosmosTriggerSettings settings = response;
        /// ]]>
        /// </code>
        /// </example>
        public virtual Task<CosmosTriggerResponse> ReadAsync(
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
        /// Replaces a <see cref="CosmosTriggerSettings"/> in the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="triggerSettings">The <see cref="CosmosTriggerSettings"/> object.</param>
        /// <param name="requestOptions">(Optional) The options for the trigger request <see cref="CosmosRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="CosmosTriggerResponse"/> which wraps a <see cref="CosmosTriggerSettings"/> containing the updated resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="triggerSettings"/> is not set.</exception>
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
        /// This examples replaces an existing trigger.
        /// <code language="c#">
        /// <![CDATA[
        /// //Updated settings
        /// CosmosTriggerSettings settings = new CosmosTriggerSettings
        /// {
        ///     Id = "testTriggerId",
        ///     Body = @"function AddTax() {
        ///         var item = getContext().getRequest().getBody();
        ///
        ///         // Validate/calculate the tax.
        ///         item.tax = item.cost* .15;
        ///
        ///         // Update the request -- this is what is going to be inserted.
        ///         getContext().getRequest().setBody(item);
        ///     }",
        ///     TriggerOperation = TriggerOperation.All,
        ///     TriggerType = TriggerType.Post
        /// };
        /// 
        /// CosmosTriggerResponse response = await this.cosmosTrigger.ReplaceAsync(settings);
        /// ]]>
        /// </code>
        /// </example>
        public virtual Task<CosmosTriggerResponse> ReplaceAsync(
                    CosmosTriggerSettings triggerSettings,
                    CosmosRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessAsync(
                partitionKey: null,
                streamPayload: triggerSettings.GetResourceStream(),
                operationType: OperationType.Replace,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Delete a <see cref="CosmosTriggerSettings"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the trigger request <see cref="CosmosRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="CosmosTriggerResponse"/> which wraps a <see cref="CosmosTriggerSettings"/> which will contain information about the request issued.</returns>
        /// <exception cref="DocumentClientException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>404</term><description>NotFound - This means the resource you tried to delete did not exist.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// /// <example>
        /// This examples gets a reference to an existing trigger and deletes it.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosTriggerResponse response = await this.cosmosContainer.Triggers["taxUdfId"].DeleteAsync();
        /// ]]>
        /// </code>
        /// </example>
        /// <example>
        /// This examples containers an existing reference to a trigger and deletes it.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosTriggerResponse response = await this.cosmosTaxUdf.DeleteAsync();
        /// ]]>
        /// </code>
        /// </example>
        public virtual Task<CosmosTriggerResponse> DeleteAsync(
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

        internal virtual Task<CosmosTriggerResponse> ProcessAsync(
            object partitionKey,
            Stream streamPayload,
            OperationType operationType,
            CosmosRequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            Task<CosmosResponseMessage> response = ExecUtils.ProcessResourceOperationStreamAsync(
                this.Client,
                this.LinkUri,
                ResourceType.Trigger,
                operationType,
                requestOptions,
                partitionKey,
                streamPayload,
                null,
                cancellationToken);

            return this.Client.ResponseFactory.CreateTriggerResponse(this, response);
        }
    }
}
