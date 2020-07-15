//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.Serialization.Formatters.Binary;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal sealed class EncryptionFeedIterator : FeedIterator
    {
        private readonly Encryptor encryptor;
        private readonly Container container;
        private readonly Action<DecryptionResult> decryptionResultHandler;
        private readonly QueryDefinition queryDefinition;
        private readonly QueryRequestOptions requestOptions;
        private readonly string continuationToken;
        private readonly IReadOnlyDictionary<List<string>, string> pathsToEncrypt = new Dictionary<List<string>, string>();
        private FeedIterator feedIterator;

        public EncryptionFeedIterator(
            FeedIterator feedIterator,
            Encryptor encryptor,
            IReadOnlyDictionary<List<string>, string> pathsToEncrypt,
            Action<DecryptionResult> decryptionResultHandler = null)
        {
            this.feedIterator = feedIterator;
            this.encryptor = encryptor;
            this.pathsToEncrypt = pathsToEncrypt;
            this.decryptionResultHandler = decryptionResultHandler;
        }

        public EncryptionFeedIterator(
            QueryDefinition queryDefinition,
            IReadOnlyDictionary<List<string>, string> pathsToEncrypt,
            QueryRequestOptions requestOptions,
            Encryptor encryptor,
            Container container,
            Action<DecryptionResult> decryptionResultHandler,
            string continuationToken = null)
        {
            this.queryDefinition = queryDefinition;
            this.pathsToEncrypt = pathsToEncrypt;
            this.requestOptions = requestOptions;
            this.encryptor = encryptor;
            this.container = container;
            this.decryptionResultHandler = decryptionResultHandler;
            this.continuationToken = continuationToken;
        }

        public override bool HasMoreResults => this.feedIterator.HasMoreResults;

        public override async Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            if (this.feedIterator == null)
            {
                if (this.queryDefinition != null)
                {
                    this.feedIterator = await this.InitializeInternalFeedIteratorAsync(this.queryDefinition);
                }
            }

            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(options: null);
            using (diagnosticsContext.CreateScope("FeedIterator.ReadNext"))
            {
                ResponseMessage responseMessage = await this.feedIterator.ReadNextAsync(cancellationToken);

                if (responseMessage.IsSuccessStatusCode && responseMessage.Content != null)
                {
                    Stream decryptedContent = await this.DeserializeAndDecryptResponseAsync(
                        responseMessage.Content,
                        diagnosticsContext,
                        cancellationToken);

                    return new DecryptedResponseMessage(responseMessage, decryptedContent);
                }

                return responseMessage;
            }
        }

        private async Task<Stream> DeserializeAndDecryptResponseAsync(
            Stream content,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            JObject contentJObj = EncryptionProcessor.BaseSerializer.FromStream<JObject>(content);
            JArray result = new JArray();

            if (!(contentJObj.SelectToken(Constants.DocumentsResourcePropertyName) is JArray documents))
            {
                throw new InvalidOperationException("Feed response Body Contract was violated. Feed response did not have an array of Documents");
            }

            foreach (JToken value in documents)
            {
                if (!(value is JObject document))
                {
                    result.Add(value);
                    continue;
                }

                try
                {
                    JObject decryptedDocument;
                    if (this.pathsToEncrypt != null)
                    {
                        decryptedDocument = await PropertyEncryptionProcessor.DecryptAsync(
                            document,
                            this.encryptor,
                            diagnosticsContext,
                            this.pathsToEncrypt,
                            cancellationToken);
                    }
                    else
                    {
                        decryptedDocument = await EncryptionProcessor.DecryptAsync(
                           document,
                           this.encryptor,
                           diagnosticsContext,
                           cancellationToken);
                    }

                    result.Add(decryptedDocument);
                }
                catch (Exception exception)
                {
                    if (this.decryptionResultHandler == null)
                    {
                        throw;
                    }

                    result.Add(document);

                    MemoryStream memoryStream = EncryptionProcessor.BaseSerializer.ToStream(document);
                    Debug.Assert(memoryStream != null);
                    bool wasBufferReturned = memoryStream.TryGetBuffer(out ArraySegment<byte> encryptedStream);
                    Debug.Assert(wasBufferReturned);

                    this.decryptionResultHandler(
                        DecryptionResult.CreateFailure(
                            encryptedStream,
                            exception));
                }
            }

            JObject decryptedResponse = new JObject();
            foreach (JProperty property in contentJObj.Properties())
            {
                if (property.Name.Equals(Constants.DocumentsResourcePropertyName))
                {
                    decryptedResponse.Add(property.Name, (JToken)result);
                }
                else
                {
                    decryptedResponse.Add(property.Name, property.Value);
                }
            }

            return EncryptionProcessor.BaseSerializer.ToStream(decryptedResponse);
        }

        private async Task<FeedIterator> InitializeInternalFeedIteratorAsync(QueryDefinition queryDefinition)
        {
            QueryDefinition newqueryDefinition = queryDefinition;
            if (queryDefinition.Parameters.Count == 0)
            {
                newqueryDefinition = this.CreateDefinition(this.queryDefinition.QueryText);
            }

            QueryDefinition queryWithEncryptedParameters = new QueryDefinition(newqueryDefinition.QueryText);
            string modifiedText = newqueryDefinition.QueryText;

            foreach (KeyValuePair<string, Query.Core.SqlParameter> parameters in newqueryDefinition.Parameters)
            {
                modifiedText = modifiedText.Replace((string)parameters.Value.Name, (string)parameters.Value.Value);
            }

            foreach (KeyValuePair<string, Query.Core.SqlParameter> parameters in newqueryDefinition.Parameters)
            {
                foreach (List<string> paths in this.pathsToEncrypt.Keys)
                {
                    if (SqlQuery.TryParse(modifiedText, out SqlQuery sqlQuery)
                        && (sqlQuery.WhereClause != null)
                        && (sqlQuery.WhereClause.FilterExpression != null)
                        && (sqlQuery.WhereClause.FilterExpression is SqlBinaryScalarExpression expression)
                        && (expression.OperatorKind == SqlBinaryScalarOperatorKind.Equal))
                    {
                        foreach (string path in paths)
                        {
                            string leftExpression = expression.LeftExpression.ToString();
                            if (leftExpression.Contains(path.Substring(1)))
                            {
                                byte[] plaintext = System.Text.Encoding.UTF8.GetBytes((string)parameters.Value.Value);
                                byte[] ciphertext = await this.encryptor.EncryptAsync(
                                    plaintext,
                                    this.pathsToEncrypt[paths],
                                    CosmosEncryptionAlgorithm.AEAD_AES_256_CBC_HMAC_SHA256);

                                queryWithEncryptedParameters.WithParameter(parameters.Key, ciphertext);
                            }
                        }
                    }
                }
            }

            return this.container.GetItemQueryStreamIterator(
                                    queryWithEncryptedParameters,
                                    this.continuationToken,
                                    this.requestOptions);
        }

        private QueryDefinition CreateDefinition(string queryText)
        {
            QueryDefinition queryDefinition = new QueryDefinition(queryText);
            foreach (List<string> paths in this.pathsToEncrypt.Keys)
            {
                if (SqlQuery.TryParse(queryText, out SqlQuery sqlQuery)
                    && (sqlQuery.WhereClause != null)
                    && (sqlQuery.WhereClause.FilterExpression != null)
                    && (sqlQuery.WhereClause.FilterExpression is SqlBinaryScalarExpression expression)
                    && (expression.OperatorKind == SqlBinaryScalarOperatorKind.Equal))
                {
                    foreach (string path in paths)
                    {
                        string rightExpression = expression.RightExpression.ToString();
                        string leftExpression = expression.LeftExpression.ToString();
                        if (leftExpression.Contains(path.Substring(1)))
                        {
                            string newExpression = queryText.Replace(rightExpression, "@" + path.Substring(1));
                            queryDefinition = new QueryDefinition(newExpression);
                            queryDefinition.WithParameter("@" + path.Substring(1), rightExpression);
                        }
                    }
                }
            }

            return queryDefinition;
        }
    }
}
