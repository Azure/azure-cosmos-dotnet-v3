//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Operations for creating new trigger, and reading/querying all triggers
    ///
    /// <see cref="CosmosTrigger"/> for reading, replacing, or deleting an existing triggers.
    /// </summary>
    internal class CosmosTriggers
    {
        private readonly CosmosContainerCore container;
        private readonly CosmosClientContext clientContext;

        /// <summary>
        /// Create a <see cref="CosmosTriggers"/>
        /// </summary>
        /// <param name="clientContext">The client context</param>
        /// <param name="container">The <see cref="CosmosContainer"/> the triggers set is related to.</param>
        protected internal CosmosTriggers(
            CosmosClientContext clientContext,
            CosmosContainerCore container)
        {
            this.container = container;
            this.clientContext = clientContext;
        }

        /// <summary>
        /// Creates a trigger as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="triggerSettings">The <see cref="CosmosTriggerSettings"/> object.</param>
        /// <param name="requestOptions">(Optional) The options for the stored procedure request <see cref="CosmosRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A task object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="triggerSettings"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occurred during async processing. Look within InnerExceptions to find the actual exception(s)</exception>
        /// <exception cref="CosmosException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the request supplied. It is likely that an Id was not supplied for the new trigger or that the Body was malformed.</description>
        ///     </item>
        ///     <item>
        ///         <term>403</term><description>Forbidden - You have reached your quota of triggers for the collection supplied. Contact support to have this quota increased.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a <see cref="CosmosTriggerSettings"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        ///     <item>
        ///         <term>413</term><description>RequestEntityTooLarge - This means the body of the <see cref="CosmosTriggerSettings"/> you tried to create was too large.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///  This creates a trigger then uses the trigger in a create item.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosTrigger cosmosTrigger = await this.container.Triggers.CreateTriggerAsync(
        ///     new CosmosTriggerSettings
        ///     {
        ///         Id = "addTax",
        ///         Body = @"function AddTax() {
        ///             var item = getContext().getRequest().getBody();
        ///
        ///             // calculate the tax.
        ///             item.tax = item.cost * .15;
        ///
        ///             // Update the request -- this is what is going to be inserted.
        ///             getContext().getRequest().setBody(item);
        ///         }",
        ///         TriggerOperation = TriggerOperation.All,
        ///         TriggerType = TriggerType.Pre
        ///     });
        ///
        /// CosmosItemRequestOptions options = new CosmosItemRequestOptions()
        /// {
        ///     PreTriggers = new List<string>() { cosmosTrigger.Id },
        /// };
        ///
        /// // Create a new item with trigger set in the request options
        /// CosmosItemResponse<dynamic> createdItem = await this.container.Items.CreateItemAsync<dynamic>(item.status, item, options);
        /// double itemTax = createdItem.Resource.tax;
        /// ]]>
        /// </code>
        /// </example>
        public virtual Task<CosmosTriggerResponse> CreateTriggerAsync(
            CosmosTriggerSettings triggerSettings,
            CosmosRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<CosmosResponseMessage> response = this.clientContext.ProcessResourceOperationStreamAsync(
                resourceUri: this.container.LinkUri,
                resourceType: ResourceType.Trigger,
                operationType: OperationType.Create,
                requestOptions: requestOptions,
                cosmosContainerCore: null,
                partitionKey: null,
                streamPayload: CosmosResource.ToStream(triggerSettings),
                requestEnricher: null,
                cancellationToken: cancellationToken);

            return this.clientContext.ResponseFactory.CreateTriggerResponse(this[triggerSettings.Id], response);
        }

        /// <summary>
        /// Gets an iterator to go through all the triggers for the container
        /// </summary>
        /// <param name="maxItemCount">(Optional) The max item count to return as part of the query</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <example>
        /// Get an iterator for all the triggers under the cosmos container
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosResultSetIterator<CosmosTriggerSettings> setIterator = this.container.Triggers.GetTriggerIterator();
        /// while (setIterator.HasMoreResults)
        /// {
        ///     foreach(CosmosTriggerSettings settings in await setIterator.FetchNextSetAsync())
        ///     {
        ///          Console.WriteLine(settings.Id); 
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public CosmosFeedIterator<CosmosTriggerSettings> GetTriggerIterator(
            int? maxItemCount = null,
            string continuationToken = null)
        {
            return new CosmosDefaultResultSetIterator<CosmosTriggerSettings>(
                maxItemCount,
                continuationToken, 
                null, 
                this.ContainerFeedRequestExecutor);
        }

        /// <summary>
        /// Returns a reference to a trigger object. 
        /// </summary>
        /// <param name="id">The cosmos trigger id.</param>
        /// <remarks>
        /// Note that the trigger must be explicitly created, if it does not already exist, before
        /// you can read from it or write to it.
        /// </remarks>
        /// <example>
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosTrigger trigger = this.cosmosContainer.Tirggers["myTriggerId"];
        /// CosmosTriggerResponse response = await trigger.ReadAsync();
        /// ]]>
        /// </code>
        /// </example>
        public CosmosTrigger this[string id] => new CosmosTrigger(this.clientContext, this.container, id);

        private Task<CosmosFeedResponse<CosmosTriggerSettings>> ContainerFeedRequestExecutor(
            int? maxItemCount,
            string continuationToken,
            CosmosRequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            Debug.Assert(state == null);

            return this.clientContext.ProcessResourceOperationAsync<CosmosFeedResponse<CosmosTriggerSettings>>(
                resourceUri: this.container.LinkUri,
                resourceType: ResourceType.Trigger,
                operationType: OperationType.ReadFeed,
                requestOptions: options,
                cosmosContainerCore: null,
                partitionKey: null,
                streamPayload: null,
                requestEnricher: request =>
                {
                    CosmosQueryRequestOptions.FillContinuationToken(request, continuationToken);
                    CosmosQueryRequestOptions.FillMaxItemCount(request, maxItemCount);
                },
                responseCreator: response => this.clientContext.ResponseFactory.CreateResultSetQueryResponse<CosmosTriggerSettings>(response),
                cancellationToken: cancellationToken);
        }
    }
}
