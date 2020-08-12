//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Newtonsoft.Json.Linq;

    internal sealed class EncryptionFeedIterator : FeedIterator
    {
        private readonly Encryptor encryptor;
        private readonly Container container;
        private readonly Action<DecryptionResult> decryptionResultHandler;
        private readonly QueryDefinition queryDefinition;
        private readonly QueryRequestOptions queryRequestOptions;
        private readonly string continuationToken;
        private readonly IReadOnlyDictionary<List<string>, string> propertiesToEncrypt = new Dictionary<List<string>, string>();
        private FeedIterator feedIterator;

        public EncryptionFeedIterator(
            FeedIterator feedIterator,
            Encryptor encryptor,
            IReadOnlyDictionary<List<string>, string> propertiesToEncrypt,
            Action<DecryptionResult> decryptionResultHandler = null)
        {
            this.feedIterator = feedIterator;
            this.encryptor = encryptor;
            this.propertiesToEncrypt = propertiesToEncrypt;
            this.decryptionResultHandler = decryptionResultHandler;
        }

        public EncryptionFeedIterator(
            QueryDefinition queryDefinition,
            IReadOnlyDictionary<List<string>, string> propertiesToEncrypt,
            QueryRequestOptions queryRequestOptions,
            Encryptor encryptor,
            Container container,
            Action<DecryptionResult> decryptionResultHandler,
            string continuationToken = null)
        {
            this.queryDefinition = queryDefinition;
            this.propertiesToEncrypt = propertiesToEncrypt;
            this.queryRequestOptions = queryRequestOptions;
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
                    if (this.propertiesToEncrypt != null)
                    {
                        decryptedDocument = await PropertyEncryptionProcessor.DecryptAsync(
                            document,
                            this.encryptor,
                            diagnosticsContext,
                            this.propertiesToEncrypt,
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

            // if paramaterized Query was sent else build one.
            if (queryDefinition.Parameters.Count == 0)
            {
                 newqueryDefinition = this.CreateDefinition(this.queryDefinition.QueryText);
            }

            QueryDefinition queryWithEncryptedParameters = new QueryDefinition(newqueryDefinition.QueryText);

            // when passed via QueryDefinition we need to replace with actual Values.
            string queryTextWithValues = newqueryDefinition.QueryText;
            foreach (KeyValuePair<string, Query.Core.SqlParameter> parameters in newqueryDefinition.Parameters)
            {
                queryTextWithValues = queryTextWithValues.Replace((string)parameters.Value.Name, parameters.Value.Value.ToString());

                // if the query definition had a bunch of encrypted and plain text values
                // lets replace it initially with plain text and the configured encrypted properties get replaced with encrypted values.
                queryWithEncryptedParameters.WithParameter(parameters.Value.Name, parameters.Value.Value);
            }

            if (SqlQuery.TryParse(queryTextWithValues, out SqlQuery sqlQuery)
                    && (sqlQuery.WhereClause != null)
                    && (sqlQuery.WhereClause.FilterExpression != null)
                    && (sqlQuery.WhereClause.FilterExpression is SqlBinaryScalarExpression expression)
                    && (expression.OperatorKind == SqlBinaryScalarOperatorKind.Equal))
            {
                // we have bunch of parameters that we have indentified,these need to be encrypted before we send out a
                // query request.
                foreach (KeyValuePair<string, Query.Core.SqlParameter> parameters in newqueryDefinition.Parameters)
                {
                    string propertyValue = string.Empty;
                    string propertyName = parameters.Value.Name.Substring(1);
                    int pos = queryTextWithValues.IndexOf(propertyName);

                    // TODO handle this and formats.
                    if (pos >= 0)
                    {
                        string temp = queryTextWithValues.Substring(pos + propertyName.Length).Trim();
                        string[] parts = temp.Split(' ');
                        propertyValue = parts[1];
                    }

                    foreach (List<string> paths in this.propertiesToEncrypt.Keys)
                    {
                        if (paths.Contains("/" + propertyName))
                        {
                            // get the Data Encryption Key configured for this path set.
                            this.propertiesToEncrypt.TryGetValue(paths, out string dEK);

                            byte[] plaintext = System.Text.Encoding.UTF8.GetBytes(propertyValue);
                            byte[] ciphertext = await this.encryptor.EncryptAsync(
                                plaintext,
                                dEK,
                                CosmosEncryptionAlgorithm.AEADAes256CbcHmacSha256Deterministic);

                            // replace the parameter values with the encrypted value.
                            queryWithEncryptedParameters.WithParameter("@" + propertyName, ciphertext);
                        }
                    }
                }
            }

            return this.container.GetItemQueryStreamIterator(
                                    queryWithEncryptedParameters,
                                    this.continuationToken,
                                    this.queryRequestOptions);
        }

        private QueryDefinition CreateDefinition(string queryText)
        {
            QueryDefinition queryDefinition = new QueryDefinition(queryText);
            string newExpression = queryText;
            Dictionary<string, string> parameterKeyValue = new Dictionary<string, string>();

            if (SqlQuery.TryParse(queryText, out SqlQuery sqlQuery)
                    && (sqlQuery.WhereClause != null)
                    && (sqlQuery.WhereClause.FilterExpression != null)
                    && (sqlQuery.WhereClause.FilterExpression is SqlBinaryScalarExpression expression)
                    && (expression.OperatorKind == SqlBinaryScalarOperatorKind.Equal))
            {
                // for each of the paths to be encrypted identify the properties in
                // the current query and build a new query.
                foreach (List<string> paths in this.propertiesToEncrypt.Keys)
                {
                    string propertyValue = null;
                    string propertyName = null;
                    foreach (string path in paths)
                    {
                        propertyValue = string.Empty;
                        propertyName = path.Substring(1);
                        int pos = queryText.IndexOf(propertyName);

                        // TODO handle this and formats.
                        if (pos >= 0)
                        {
                            string temp = queryText.Substring(pos + propertyName.Length).Trim();
                            string[] parts = temp.Split(' ');
                            propertyValue = parts[1];

                            if (paths.Contains("/" + propertyName))
                            {
                                // build the list of parameters and their values to build query definition with these parameters.
                                newExpression = newExpression.Replace(propertyValue, "@" + propertyName);
                                parameterKeyValue.Add(propertyName, propertyValue);
                            }
                        }
                    }
                }

                // Build a new QueryDefinition for the new expression
                // with paramertes identified.
                queryDefinition = new QueryDefinition(newExpression);
                foreach (KeyValuePair<string, string> entry in parameterKeyValue)
                {
                    queryDefinition.WithParameter("@" + entry.Key, entry.Value);
                }
            }

            return queryDefinition;
        }
    }
}
