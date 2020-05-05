//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Scripts
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    internal sealed class ScriptsCore : CosmosScripts
    {
        private readonly ContainerCore container;
        private readonly CosmosClientContext clientContext;

        internal ScriptsCore(
            ContainerCore container,
            CosmosClientContext clientContext)
        {
            this.container = container;
            this.clientContext = clientContext;
        }

        public override Task<Response<StoredProcedureProperties>> CreateStoredProcedureAsync(
                    StoredProcedureProperties storedProcedureProperties,
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessStoredProcedureOperationAsync(
                linkUri: this.container.LinkUri,
                operationType: OperationType.Create,
                streamPayload: this.clientContext.PropertiesSerializer.ToStream(storedProcedureProperties),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public override IAsyncEnumerable<Response> GetStoredProcedureQueryStreamResultsAsync(
           string queryText = null,
           string continuationToken = null,
           QueryRequestOptions requestOptions = null,
           CancellationToken cancellationToken = default(CancellationToken))
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return this.GetStoredProcedureQueryStreamResultsAsync(
                queryDefinition,
                continuationToken,
                requestOptions,
                cancellationToken);
        }

        public override AsyncPageable<T> GetStoredProcedureQueryResultsAsync<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return this.GetStoredProcedureQueryResultsAsync<T>(
                queryDefinition,
                continuationToken,
                requestOptions,
                cancellationToken);
        }

        public override async IAsyncEnumerable<Response> GetStoredProcedureQueryStreamResultsAsync(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default(CancellationToken))
        {
            FeedIterator iterator = this.GetStoredProcedureQueryIterator(queryDefinition, continuationToken, requestOptions);
            while (iterator.HasMoreResults)
            {
                yield return await iterator.ReadNextAsync(cancellationToken);
            }
        }

        public override AsyncPageable<T> GetStoredProcedureQueryResultsAsync<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            FeedIterator iterator = this.GetStoredProcedureQueryIterator(
                queryDefinition,
                continuationToken,
                requestOptions);

            PageIteratorCore<T> pageIterator = new PageIteratorCore<T>(
                feedIterator: iterator,
                responseCreator: this.clientContext.ResponseFactory.CreateQueryFeedResponseWithPropertySerializer<T>);

            return PageResponseEnumerator.CreateAsyncPageable(continuation => pageIterator.GetPageAsync(continuation, cancellationToken));
        }

        public override Task<Response<StoredProcedureProperties>> ReadStoredProcedureAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            return this.ProcessStoredProcedureOperationAsync(
                id: id,
                operationType: OperationType.Read,
                streamPayload: null,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public override Task<Response<StoredProcedureProperties>> ReplaceStoredProcedureAsync(
            StoredProcedureProperties storedProcedureProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessStoredProcedureOperationAsync(
                id: storedProcedureProperties.Id,
                operationType: OperationType.Replace,
                streamPayload: this.clientContext.PropertiesSerializer.ToStream(storedProcedureProperties),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public override Task<Response<StoredProcedureProperties>> DeleteStoredProcedureAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            return this.ProcessStoredProcedureOperationAsync(
                id: id,
                operationType: OperationType.Delete,
                streamPayload: null,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public override Task<StoredProcedureExecuteResponse<TOutput>> ExecuteStoredProcedureAsync<TOutput>(
            string storedProcedureId,
            Cosmos.PartitionKey partitionKey,
            dynamic[] parameters,
            StoredProcedureRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<Response> response = this.ExecuteStoredProcedureStreamAsync(
                storedProcedureId: storedProcedureId,
                partitionKey: partitionKey,
                parameters: parameters,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.clientContext.ResponseFactory.CreateStoredProcedureExecuteResponseAsync<TOutput>(response, cancellationToken);
        }

        public override Task<Response> ExecuteStoredProcedureStreamAsync(
            string storedProcedureId,
            Cosmos.PartitionKey partitionKey,
            dynamic[] parameters,
            StoredProcedureRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(storedProcedureId))
            {
                throw new ArgumentNullException(nameof(storedProcedureId));
            }

            ContainerCore.ValidatePartitionKey(partitionKey, requestOptions);

            Stream streamPayload = null;
            if (parameters != null)
            {
                streamPayload = this.clientContext.CosmosSerializer.ToStream<dynamic[]>(parameters);
            }

            Uri linkUri = this.clientContext.CreateLink(
                parentLink: this.container.LinkUri.OriginalString,
                uriPathSegment: Paths.StoredProceduresPathSegment,
                id: storedProcedureId);

            return this.ProcessStreamOperationAsync(
                resourceUri: linkUri,
                resourceType: ResourceType.StoredProcedure,
                operationType: OperationType.ExecuteJavaScript,
                partitionKey: partitionKey,
                streamPayload: streamPayload,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public override Task<Response<TriggerProperties>> CreateTriggerAsync(
            TriggerProperties triggerProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (triggerProperties == null)
            {
                throw new ArgumentNullException(nameof(triggerProperties));
            }

            if (string.IsNullOrEmpty(triggerProperties.Id))
            {
                throw new ArgumentNullException(nameof(triggerProperties.Id));
            }

            if (string.IsNullOrEmpty(triggerProperties.Body))
            {
                throw new ArgumentNullException(nameof(triggerProperties.Body));
            }

            return this.ProcessTriggerOperationAsync(
                linkUri: this.container.LinkUri,
                operationType: OperationType.Create,
                streamPayload: this.clientContext.PropertiesSerializer.ToStream(triggerProperties),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public override IAsyncEnumerable<Response> GetTriggerQueryStreamResultsAsync(
           string queryText = null,
           string continuationToken = null,
           QueryRequestOptions requestOptions = null,
           CancellationToken cancellationToken = default(CancellationToken))
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return this.GetTriggerQueryStreamResultsAsync(
                queryDefinition,
                continuationToken,
                requestOptions,
                cancellationToken);
        }

        public override AsyncPageable<T> GetTriggerQueryResultsAsync<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return this.GetTriggerQueryResultsAsync<T>(
                queryDefinition,
                continuationToken,
                requestOptions,
                cancellationToken);
        }

        public override async IAsyncEnumerable<Response> GetTriggerQueryStreamResultsAsync(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default(CancellationToken))
        {
            FeedIterator iterator = this.GetTriggerQueryIterator(
                queryDefinition,
                continuationToken,
                requestOptions);
            while (iterator.HasMoreResults)
            {
                yield return await iterator.ReadNextAsync(cancellationToken);
            }
        }

        public override AsyncPageable<T> GetTriggerQueryResultsAsync<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            FeedIterator iterator = this.GetTriggerQueryIterator(
                queryDefinition,
                continuationToken,
                requestOptions);

            PageIteratorCore<T> pageIterator = new PageIteratorCore<T>(
                feedIterator: iterator,
                responseCreator: this.clientContext.ResponseFactory.CreateQueryFeedResponseWithPropertySerializer<T>);

            return PageResponseEnumerator.CreateAsyncPageable(continuation => pageIterator.GetPageAsync(continuation, cancellationToken));
        }

        public override Task<Response<TriggerProperties>> ReadTriggerAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            return this.ProcessTriggerOperationAsync(
                id: id,
                operationType: OperationType.Read,
                streamPayload: null,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public override Task<Response<TriggerProperties>> ReplaceTriggerAsync(
            TriggerProperties triggerProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (triggerProperties == null)
            {
                throw new ArgumentNullException(nameof(triggerProperties));
            }

            if (string.IsNullOrEmpty(triggerProperties.Id))
            {
                throw new ArgumentNullException(nameof(triggerProperties.Id));
            }

            if (string.IsNullOrEmpty(triggerProperties.Body))
            {
                throw new ArgumentNullException(nameof(triggerProperties.Body));
            }

            return this.ProcessTriggerOperationAsync(
                id: triggerProperties.Id,
                operationType: OperationType.Replace,
                streamPayload: this.clientContext.PropertiesSerializer.ToStream(triggerProperties),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public override Task<Response<TriggerProperties>> DeleteTriggerAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            return this.ProcessTriggerOperationAsync(
                id: id,
                operationType: OperationType.Delete,
                streamPayload: null,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public override Task<Response<UserDefinedFunctionProperties>> CreateUserDefinedFunctionAsync(
            UserDefinedFunctionProperties userDefinedFunctionProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (userDefinedFunctionProperties == null)
            {
                throw new ArgumentNullException(nameof(userDefinedFunctionProperties));
            }

            if (string.IsNullOrEmpty(userDefinedFunctionProperties.Id))
            {
                throw new ArgumentNullException(nameof(userDefinedFunctionProperties.Id));
            }

            if (string.IsNullOrEmpty(userDefinedFunctionProperties.Body))
            {
                throw new ArgumentNullException(nameof(userDefinedFunctionProperties.Body));
            }

            return this.ProcessUserDefinedFunctionOperationAsync(
                linkUri: this.container.LinkUri,
                operationType: OperationType.Create,
                streamPayload: this.clientContext.PropertiesSerializer.ToStream(userDefinedFunctionProperties),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public override IAsyncEnumerable<Response> GetUserDefinedFunctionQueryStreamResultsAsync(
           string queryText = null,
           string continuationToken = null,
           QueryRequestOptions requestOptions = null,
           CancellationToken cancellationToken = default(CancellationToken))
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return this.GetUserDefinedFunctionQueryStreamResultsAsync(
                queryDefinition,
                continuationToken,
                requestOptions,
                cancellationToken);
        }

        public override AsyncPageable<T> GetUserDefinedFunctionQueryResultsAsync<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return this.GetUserDefinedFunctionQueryResultsAsync<T>(
                queryDefinition,
                continuationToken,
                requestOptions,
                cancellationToken);
        }

        public override async IAsyncEnumerable<Response> GetUserDefinedFunctionQueryStreamResultsAsync(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default(CancellationToken))
        {
            FeedIterator iterator = new FeedIteratorCore(
               this.clientContext,
               this.container.LinkUri,
               ResourceType.UserDefinedFunction,
               queryDefinition,
               continuationToken,
               requestOptions);
            while (iterator.HasMoreResults)
            {
                yield return await iterator.ReadNextAsync(cancellationToken);
            }
        }

        public override AsyncPageable<T> GetUserDefinedFunctionQueryResultsAsync<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            FeedIterator iterator = this.GetUserDefinedFunctionQueryIterator(
                queryDefinition,
                continuationToken,
                requestOptions);

            PageIteratorCore<T> pageIterator = new PageIteratorCore<T>(
                feedIterator: iterator,
                responseCreator: this.clientContext.ResponseFactory.CreateQueryFeedResponseWithPropertySerializer<T>);

            return PageResponseEnumerator.CreateAsyncPageable(continuation => pageIterator.GetPageAsync(continuation, cancellationToken));
        }

        public override Task<Response<UserDefinedFunctionProperties>> ReadUserDefinedFunctionAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            return this.ProcessUserDefinedFunctionOperationAsync(
                id: id,
                operationType: OperationType.Read,
                streamPayload: null,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public override Task<Response<UserDefinedFunctionProperties>> ReplaceUserDefinedFunctionAsync(
            UserDefinedFunctionProperties userDefinedFunctionProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (userDefinedFunctionProperties == null)
            {
                throw new ArgumentNullException(nameof(userDefinedFunctionProperties));
            }

            if (string.IsNullOrEmpty(userDefinedFunctionProperties.Id))
            {
                throw new ArgumentNullException(nameof(userDefinedFunctionProperties.Id));
            }

            if (string.IsNullOrEmpty(userDefinedFunctionProperties.Body))
            {
                throw new ArgumentNullException(nameof(userDefinedFunctionProperties.Body));
            }

            return this.ProcessUserDefinedFunctionOperationAsync(
                id: userDefinedFunctionProperties.Id,
                operationType: OperationType.Replace,
                streamPayload: this.clientContext.PropertiesSerializer.ToStream(userDefinedFunctionProperties),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public override Task<Response<UserDefinedFunctionProperties>> DeleteUserDefinedFunctionAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            return this.ProcessUserDefinedFunctionOperationAsync(
                id: id,
                operationType: OperationType.Delete,
                streamPayload: null,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        private Task<Response<StoredProcedureProperties>> ProcessStoredProcedureOperationAsync(
            string id,
            OperationType operationType,
            Stream streamPayload,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            Uri linkUri = this.clientContext.CreateLink(
                parentLink: this.container.LinkUri.OriginalString,
                uriPathSegment: Paths.StoredProceduresPathSegment,
                id: id);

            return this.ProcessStoredProcedureOperationAsync(
                linkUri: linkUri,
                operationType: operationType,
                streamPayload: streamPayload,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        private Task<Response<StoredProcedureProperties>> ProcessStoredProcedureOperationAsync(
            Uri linkUri,
            OperationType operationType,
            Stream streamPayload,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            Task<Response> response = this.ProcessStreamOperationAsync(
                resourceUri: linkUri,
                resourceType: ResourceType.StoredProcedure,
                operationType: operationType,
                requestOptions: requestOptions,
                partitionKey: null,
                streamPayload: streamPayload,
                cancellationToken: cancellationToken);

            return this.clientContext.ResponseFactory.CreateStoredProcedureResponseAsync(response, cancellationToken);
        }

        private Task<Response<TriggerProperties>> ProcessTriggerOperationAsync(
            string id,
            OperationType operationType,
            Stream streamPayload,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            Uri linkUri = this.clientContext.CreateLink(
                parentLink: this.container.LinkUri.OriginalString,
                uriPathSegment: Paths.TriggersPathSegment,
                id: id);

            return this.ProcessTriggerOperationAsync(
                linkUri: linkUri,
                operationType: operationType,
                streamPayload: streamPayload,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        private Task<Response<TriggerProperties>> ProcessTriggerOperationAsync(
            Uri linkUri,
            OperationType operationType,
            Stream streamPayload,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            Task<Response> response = this.ProcessStreamOperationAsync(
                resourceUri: linkUri,
                resourceType: ResourceType.Trigger,
                operationType: operationType,
                requestOptions: requestOptions,
                partitionKey: null,
                streamPayload: streamPayload,
                cancellationToken: cancellationToken);

            return this.clientContext.ResponseFactory.CreateTriggerResponseAsync(response, cancellationToken);
        }

        private Task<Response> ProcessStreamOperationAsync(
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            Cosmos.PartitionKey? partitionKey,
            Stream streamPayload,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            return this.clientContext.ProcessResourceOperationStreamAsync(
                resourceUri: resourceUri,
                resourceType: resourceType,
                operationType: operationType,
                requestOptions: requestOptions,
                cosmosContainerCore: this.container,
                partitionKey: partitionKey,
                streamPayload: streamPayload,
                requestEnricher: null,
                cancellationToken: cancellationToken);
        }

        private Task<Response<UserDefinedFunctionProperties>> ProcessUserDefinedFunctionOperationAsync(
            string id,
            OperationType operationType,
            Stream streamPayload,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            Uri linkUri = this.clientContext.CreateLink(
                parentLink: this.container.LinkUri.OriginalString,
                uriPathSegment: Paths.UserDefinedFunctionsPathSegment,
                id: id);

            return this.ProcessUserDefinedFunctionOperationAsync(
                linkUri: linkUri,
                operationType: operationType,
                streamPayload: streamPayload,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        private Task<Response<UserDefinedFunctionProperties>> ProcessUserDefinedFunctionOperationAsync(
            Uri linkUri,
            OperationType operationType,
            Stream streamPayload,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            Task<Response> response = this.ProcessStreamOperationAsync(
                resourceUri: linkUri,
                resourceType: ResourceType.UserDefinedFunction,
                operationType: operationType,
                requestOptions: requestOptions,
                partitionKey: null,
                streamPayload: streamPayload,
                cancellationToken: cancellationToken);

            return this.clientContext.ResponseFactory.CreateUserDefinedFunctionResponseAsync(response, cancellationToken);
        }

        private FeedIterator GetTriggerQueryIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorCore(
               this.clientContext,
               this.container.LinkUri,
               ResourceType.Trigger,
               queryDefinition,
               continuationToken,
               requestOptions);
        }

        private FeedIterator GetUserDefinedFunctionQueryIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorCore(
               this.clientContext,
               this.container.LinkUri,
               ResourceType.UserDefinedFunction,
               queryDefinition,
               continuationToken,
               requestOptions);
        }

        private FeedIterator GetStoredProcedureQueryIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorCore(
               this.clientContext,
               this.container.LinkUri,
               ResourceType.StoredProcedure,
               queryDefinition,
               continuationToken,
               requestOptions);
        }
    }
}
