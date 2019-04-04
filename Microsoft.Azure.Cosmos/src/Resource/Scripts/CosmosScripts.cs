//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Operations for creating new scripts,reading, replacing, or deleting a specific, existing scripts by id,
    /// reading/querying all stored scripts
    /// </summary>
    public class CosmosScripts
    {
        private readonly CosmosContainer container;
        private readonly CosmosClient client;
        private readonly static string ScriptType = "scriptType";
        private readonly static string ExecuteAsynError = "Scripts.ExecuteAsync() only work with CosmosScriptType.StoredProcedure";

        protected internal CosmosScripts(CosmosContainer container)
        {
            this.container = container;
            this.client = container.Client;
        }

        /// <summary>
        /// Creates a script(Stored Prodecure, Trigger, UDFs) as an asynchronous operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="id">The cosmos scripts id</param>
        /// <param name="body">The JavaScript function that is the body of the scripts</param>
        /// <param name="requestOptions">(Optional) The options for the script request <see cref="CosmosScriptRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The <see cref="CosmosScriptsSettings"/> that was created contained within a <see cref="Task"/> object representing the service response for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">If either <paramref name="id"/> or <paramref name="body"/> is not set.</exception>
        /// <exception cref="System.AggregateException">Represents a consolidation of failures that occurred during async processing. Look within InnerExceptions to find the actual exception(s)</exception>
        /// <exception cref="CosmosException">This exception can encapsulate many different types of errors. To determine the specific error always look at the StatusCode property. Some common codes you may get when creating a Document are:
        /// <list type="table">
        ///     <listheader>
        ///         <term>StatusCode</term><description>Reason for exception</description>
        ///     </listheader>
        ///     <item>
        ///         <term>400</term><description>BadRequest - This means something was wrong with the request supplied. It is likely that an Id was not supplied for the script or the Body was malformed.</description>
        ///     </item>
        ///     <item>
        ///         <term>403</term><description>Forbidden - You have reached your quota of scripts for the collection supplied. Contact support to have this quota increased.</description>
        ///     </item>
        ///     <item>
        ///         <term>409</term><description>Conflict - This means a <see cref="CosmosScriptSettings"/> with an id matching the id you supplied already existed.</description>
        ///     </item>
        ///     <item>
        ///         <term>413</term><description>RequestEntityTooLarge - This means the body of the <see cref="CosmosScriptSettings"/> you tried to create was too large.</description>
        ///     </item>
        /// </list>
        /// </exception>
        /// <example>
        ///  This creates and executes a script that appends a string to the first item returned from the query.
        /// <code language="c#">
        /// <![CDATA[
        /// string sprocBody = @"function simple(prefix)
        ///    {
        ///        var collection = getContext().getCollection();
        ///
        ///        // Query documents and take 1st item.
        ///        var isAccepted = collection.queryDocuments(
        ///        collection.getSelfLink(),
        ///        'SELECT * FROM root r',
        ///        function(err, feed, options) {
        ///            if (err)throw err;
        ///
        ///            // Check the feed and if it's empty, set the body to 'no docs found',
        ///            // Otherwise just take 1st element from the feed.
        ///            if (!feed || !feed.length) getContext().getResponse().setBody(""no docs found"");
        ///            else getContext().getResponse().setBody(prefix + JSON.stringify(feed[0]));
        ///        });
        ///
        ///        if (!isAccepted) throw new Error(""The query wasn't accepted by the server. Try again/use continuation token between API and script."");
        ///    }";
        ///
        ///  CosmosScriptResponse scriptResponse = await this.container.Scripts.CreateAsync(scriptSettings: new CosmosScriptSettings(Id: "appendString", Body: sprocBody, CosmosScriptType.StoredProcedure));
        ///
        /// // Execute the stored procedure
        /// CosmosItemResponse<string> sprocResponse = await this.container.Scripts.ExecuteAsync<string, string>(scriptResponse.ScriptSettings.Id, scriptResponse.ScriptSettings.Type, testPartitionId, "Item as a string: ");
        /// Console.WriteLine("sprocResponse.Resource");
        /// ]]>
        /// </code>
        /// </example>
        public virtual Task<CosmosScriptResponse> CreateAsync(
                    CosmosScriptSettings scriptSettings,
                    CosmosScriptRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            ValidateCosmosScriptSettings(scriptSettings);
            CheckForTrigger(ref scriptSettings);
            Task<CosmosResponseMessage> response = ExecUtils.ProcessResourceOperationStreamAsync(
                this.container.Database.Client,
                this.container.LinkUri,
                GetResourceType(scriptSettings.Type),
                OperationType.Create,
                requestOptions,
                partitionKey: null,
                streamPayload: CosmosResource.ToStream(scriptSettings),
                requestEnricher: null,
                cancellationToken: cancellationToken);

            return this.client.ResponseFactory.CreateScriptResponse(response, scriptSettings.Type);

        }

        /// <summary>
        /// Reads a <see cref="CosmosScriptSettings"/> from the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the stored procedure request <see cref="CosmosScriptRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="CosmosScriptResponse"/> which wraps a <see cref="CosmosScriptSettings"/> containing the read resource record.
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
        ///  <example>
        ///  This reads an existing script.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosScriptResponse response = await this.container.Scripts.ReadAsync("ExistingId", CosmosScriptType.StoredProcedure);
        /// CosmosScriptSettings settings = response;
        /// ]]>
        /// </code>
        /// </example>
        public virtual Task<CosmosScriptResponse> ReadAsync(
                    string id,
                    CosmosScriptType? scriptType,
                    CosmosScriptRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            return ProcessAsync(
                id,
                scriptType,
                partitionKey: null,
                streamPayload: null,
                operationType: OperationType.Read,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Replaces a <see cref="CosmosScriptSettings"/> in the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="body">The JavaScript function to replace the existing resource with.</param>
        /// <param name="requestOptions">(Optional) The options for the script request <see cref="CosmosScriptRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="CosmosScriptResponse"/> which wraps a <see cref="CosmosScriptSettings"/> containing the updated resource record.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="body"/> is not set.</exception>
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
        /// <example>
        /// This examples replaces an existing stored procedure.
        /// <code language="c#">
        /// <![CDATA[
        /// //Updated settings
        /// CosmosScriptSettings settings = new CosmosScriptSettings
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
        ///     Type =  CosmosScriptType.StoredProcedure
        /// };
        ///
        /// CosmosScriptResponse response = await this.container.Scripts.ReplaceAsync(settings);
        /// ]]>
        /// </code>
        /// </example>
        public virtual Task<CosmosScriptResponse> ReplaceAsync(
                    CosmosScriptSettings scriptSettings,
                    CosmosScriptRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            ValidateCosmosScriptSettings(scriptSettings);

            return ProcessAsync(
                scriptSettings.Id,
                scriptSettings.Type,
                partitionKey: null,
                streamPayload: CosmosResource.ToStream(scriptSettings),
                operationType: OperationType.Replace,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Delete a <see cref="CosmosScriptSettings"/> from the Azure Cosmos DB service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">(Optional) The options for the script request <see cref="CosmosScriptRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="CosmosScriptResponse"/> which wraps a <see cref="CosmosScriptSettings"/> which will contain information about the request issued.</returns>
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
        /// <example>
        /// This examples delete the exisiting script.
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosScriptResponse response =  await this.container.Scripts.DeleteAsync("taxUdfId", CosmosScriptType.UserDefinedFunction);
        /// ]]>
        /// </code>
        /// </example>
        public virtual Task<CosmosScriptResponse> DeleteAsync(
                    string id,
                    CosmosScriptType? scriptType,
                    CosmosScriptRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            return ProcessAsync(
                id,
                scriptType,
                partitionKey: null,
                streamPayload: null,
                operationType: OperationType.Delete,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Executes a stored procedure against a container as an asynchronous operation in the Azure Cosmos service.
        /// </summary>
        /// <typeparam name="TInput">The input type that is JSON serializable.</typeparam>
        /// <typeparam name="TOutput">The return type that is JSON serializable.</typeparam>
        /// <param name="id">The script id.<see cref="PartitionKey"/></param>
        /// <param name="scriptType">The script type.</param>
        /// <param name="partitionKey">The partition key for the item. <see cref="PartitionKey"/></param>
        /// <param name="input">The JSON serializable input parameters.</param>
        /// <param name="requestOptions">(Optional) The options for the stored procedure request <see cref="CosmosScriptExecuteRequestOptions"/></param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The task object representing the service response for the asynchronous operation which would contain any response set in the stored procedure.</returns>
        /// <example>
        ///  This creates and executes a stored procedure that appends a string to the first item returned from the query.
        /// <code language="c#">
        /// <![CDATA[
        /// string sprocBody = @"function simple(prefix)
        ///    {
        ///        var collection = getContext().getCollection();
        ///
        ///        // Query documents and take 1st item.
        ///        var isAccepted = collection.queryDocuments(
        ///        collection.getSelfLink(),
        ///        'SELECT * FROM root r',
        ///        function(err, feed, options) {
        ///            if (err)throw err;
        ///
        ///            // Check the feed and if it's empty, set the body to 'no docs found',
        ///            // Otherwise just take 1st element from the feed.
        ///            if (!feed || !feed.length) getContext().getResponse().setBody(""no docs found"");
        ///            else getContext().getResponse().setBody(prefix + JSON.stringify(feed[0]));
        ///        });
        ///
        ///        if (!isAccepted) throw new Error(""The query wasn't accepted by the server. Try again/use continuation token between API and script."");
        ///    }";
        ///
        ///  CosmosScriptResponse scriptResponse = await this.container.Scripts.CreateAsync(scriptSettings: new CosmosScriptSettings(Id: "appendString", Body: sprocBody, CosmosScriptType.StoredProcedure));
        /// // Execute the stored procedure
        /// CosmosItemResponse<string> sprocResponse = await this.container.Scripts.ExecuteAsync<string, string>(scriptResponse.ScriptSettings.Id, scriptResponse.ScriptSettings.Type, testPartitionId, "Item as a string: ");
        /// Console.WriteLine("sprocResponse.Resource");
        /// ]]>
        /// </code>
        /// </example>
        public Task<CosmosItemResponse<TOutput>> ExecuteAsync<TInput, TOutput>(
            string id,
            CosmosScriptType? scriptType, //Need to discuss
            object partitionKey,
            TInput input,
            CosmosScriptExecuteRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (scriptType == null || !scriptType.Equals(CosmosScriptType.StoredProcedure))
            {
                throw new ArgumentException(ExecuteAsynError, nameof(scriptType));
            }

            CosmosItemsCore.ValidatePartitionKey(partitionKey, requestOptions);
            Stream parametersStream;
            if (input != null && !input.GetType().IsArray)
            {
                parametersStream = this.client.CosmosJsonSerializer.ToStream<TInput[]>(new TInput[1] { input });
            }
            else
            {
                parametersStream = this.client.CosmosJsonSerializer.ToStream<TInput>(input);
            }
            Uri linkURI = this.ContcatCachedUriWithId(id, scriptType);
            Task<CosmosResponseMessage> response = ExecUtils.ProcessResourceOperationStreamAsync(
                this.client,
                linkURI,
                GetResourceType(scriptType),
                OperationType.ExecuteJavaScript,
                requestOptions,
                partitionKey,
                parametersStream,
                null,
                cancellationToken);

            return this.client.ResponseFactory.CreateItemResponse<TOutput>(response);
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets an iterator to go through all the scripts for the container
        /// </summary>
        /// <param name="cosmosScriptType">The script type i.e StoredProcedure, UserDefinedFunction, Trigger  </param>
        /// <param name="maxItemCount">(Optional) The max item count to return as part of the query</param>
        /// <param name="continuationToken">(Optional) The continuation token in the Azure Cosmos DB service.</param>
        /// <example>
        /// Get an iterator for all the scripts under the cosmos container
        /// <code language="c#">
        /// <![CDATA[
        /// CosmosResultSetIterator<CosmosScriptSettings> setIterator = this.container.Scripts.GetScriptIterator(CosmosScriptType.UserDefinedFunction);
        /// while (setIterator.HasMoreResults)
        /// {
        ///     foreach(CosmosScriptSettings settings in await setIterator.FetchNextSetAsync())
        ///     {
        ///          Console.WriteLine(settings.Id);
        ///     }
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public virtual CosmosResultSetIterator<CosmosScriptSettings> GetScriptIterator(
            CosmosScriptType cosmosScriptType,
            int? maxItemCount = null,
            string continuationToken = null)
        {
            CosmosRequestOptions requestOptions = new CosmosRequestOptions();
            requestOptions.Properties = new Dictionary<string, object>();
            requestOptions.Properties.Add(ScriptType, cosmosScriptType);
            return new CosmosDefaultResultSetIterator<CosmosScriptSettings>(
                maxItemCount,
                continuationToken,
                requestOptions,
                this.ScriptFeedRequestExecutor);
        }

        private Uri GetResourceUri(CosmosRequestOptions requestOptions, OperationType operationType, string itemId, CosmosScriptType cosmosScriptType)
        {
            if (requestOptions != null && requestOptions.TryGetResourceUri(out Uri resourceUri))
            {
                return resourceUri;
            }

            switch (operationType)
            {
                case OperationType.Create:
                case OperationType.Upsert:
                    return this.container.LinkUri;

                default:
                    return this.ContcatCachedUriWithId(itemId, cosmosScriptType);
            }
        }
        private Task<CosmosQueryResponse<CosmosScriptSettings>> ScriptFeedRequestExecutor(
           int? maxItemCount,
           string continuationToken,
           CosmosRequestOptions options,
           object state,
           CancellationToken cancellationToken)
        {
            Uri resourceUri = this.container.LinkUri;
            return ExecUtils.ProcessResourceOperationAsync<CosmosQueryResponse<CosmosScriptSettings>>(
                this.container.Database.Client,
                resourceUri,
                GetResourceType((CosmosScriptType)options.Properties[ScriptType]),
                OperationType.ReadFeed,
                options,
                request =>
                {
                    CosmosQueryRequestOptions.FillContinuationToken(request, continuationToken);
                    CosmosQueryRequestOptions.FillMaxItemCount(request, maxItemCount);
                },
                response => this.client.ResponseFactory.CreateResultSetQueryResponse<CosmosScriptSettings>(response),
                cancellationToken);
        }
        private ResourceType GetResourceType(CosmosScriptType? type)
        {
            switch (type)
            {
                case CosmosScriptType.StoredProcedure:
                    return ResourceType.StoredProcedure;
                case CosmosScriptType.UserDefinedFunction:
                    return ResourceType.UserDefinedFunction;
                case CosmosScriptType.PostTrigger:
                    return ResourceType.Trigger;
                case CosmosScriptType.PreTrigger:
                    return ResourceType.Trigger;
                default:
                    return ResourceType.StoredProcedure;
            }
        }

        private void CheckForTrigger(ref CosmosScriptSettings scriptSettings)
        {
            if (scriptSettings.Type.Equals(CosmosScriptType.PostTrigger))
            {
                scriptSettings.TriggerType = TriggerType.Post;
            }
            else if (scriptSettings.Type.Equals(CosmosScriptType.PreTrigger))
            {
                scriptSettings.TriggerType = TriggerType.Pre;
            }
        }

        private void ValidateCosmosScriptSettings(CosmosScriptSettings scriptSettings)
        {
            if (scriptSettings == null)
            {
                throw new ArgumentNullException(nameof(scriptSettings));
            }

            if (string.IsNullOrEmpty(scriptSettings.Id))
            {
                throw new ArgumentNullException(nameof(scriptSettings.Id));
            }

            if (string.IsNullOrEmpty(scriptSettings.Body))
            {
                throw new ArgumentNullException(nameof(scriptSettings.Body));
            }

            if (scriptSettings.Type == null)
            {
                throw new ArgumentNullException(nameof(scriptSettings.Type));
            }
        }

        /// <summary>
        /// Gets the full resource segment URI without the last id.
        /// </summary>
        /// <returns>Example: /dbs/*/colls/*/{this.pathSegment}/ </returns>
        private string GetResourceSegmentUriWithoutId(CosmosScriptType? cosmosScriptType)
        {
            // StringBuilder is roughly 2x faster than string.Format
            StringBuilder stringBuilder = new StringBuilder(this.container.LinkUri.OriginalString.Length +
                                                            Paths.DocumentsPathSegment.Length + 2);
            stringBuilder.Append(this.container.LinkUri.OriginalString);
            stringBuilder.Append("/");
            switch (cosmosScriptType)
            {
                case CosmosScriptType.StoredProcedure:
                    stringBuilder.Append(Paths.StoredProceduresPathSegment);
                    break;
                case CosmosScriptType.UserDefinedFunction:
                    stringBuilder.Append(Paths.UserDefinedFunctionsPathSegment);
                    break;
                case CosmosScriptType.PreTrigger:
                    stringBuilder.Append(Paths.TriggersPathSegment);
                    break;
                case CosmosScriptType.PostTrigger:
                    stringBuilder.Append(Paths.TriggersPathSegment);
                    break;
            }
            stringBuilder.Append("/");
            return stringBuilder.ToString();
        }

        /// <summary>
        /// Gets the full resource URI using the cached resource URI segment 
        /// </summary>
        /// <param name="resourceId">The resource id</param>
        /// <returns>
        /// A document link in the format of {CachedUriSegmentWithoutId}/{0}/ with {0} being a Uri escaped version of the <paramref name="resourceId"/>
        /// </returns>
        /// <remarks>Would be used when creating an <see cref="Attachment"/>, or when replacing or deleting a item in Azure Cosmos DB.</remarks>
        /// <seealso cref="Uri.EscapeUriString"/>
        private Uri ContcatCachedUriWithId(string resourceId, CosmosScriptType? cosmosScriptType)
        {
            return new Uri(this.GetResourceSegmentUriWithoutId(cosmosScriptType) + Uri.EscapeUriString(resourceId), UriKind.Relative);
        }

        internal Task<CosmosScriptResponse> ProcessAsync(
            String id,
            CosmosScriptType? scriptType,
            object partitionKey,
            Stream streamPayload,
            OperationType operationType,
            CosmosRequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            Uri linkURI = this.ContcatCachedUriWithId(id, scriptType);//Need to discuss, 
            Task<CosmosResponseMessage> response = ExecUtils.ProcessResourceOperationStreamAsync(
                this.client,
                linkURI,
                GetResourceType(scriptType),
                operationType,
                requestOptions,
                partitionKey,
                streamPayload,
                null,
                cancellationToken);

            return this.client.ResponseFactory.CreateScriptResponse(response, scriptType);
        }
    }
}
