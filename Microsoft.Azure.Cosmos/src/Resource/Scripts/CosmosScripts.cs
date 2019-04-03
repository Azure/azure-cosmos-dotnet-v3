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

        public Task<CosmosItemResponse<TOutput>> ExecuteAsync<TInput, TOutput>(
            string id,
            CosmosScriptType? scriptType,
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
            Uri linkURI = this.ContcatCachedUriWithId(id, scriptType);//Need to discuss, 
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
