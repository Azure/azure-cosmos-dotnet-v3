//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Parser;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Newtonsoft.Json.Linq;

    internal sealed class EncryptionFeedIterator : FeedIterator
    {
        private FeedIterator feedIterator;
        private readonly Encryptor encryptor;
        private readonly Action<DecryptionResult> decryptionResultHandler;
        private readonly Container container;
        private readonly ClientEncryptionPolicy clientEncryptionPolicy;
        private readonly QueryRequestOptions queryRequestOptions;
        private readonly string continuationToken;
        private readonly QueryDefinition queryDefinition;

        public EncryptionFeedIterator(
            FeedIterator feedIterator,
            Encryptor encryptor,
            Action<DecryptionResult> decryptionResultHandler = null)
        {
            this.feedIterator = feedIterator;
            this.encryptor = encryptor;
            this.decryptionResultHandler = decryptionResultHandler;
        }

        public EncryptionFeedIterator(
            QueryDefinition queryDefinition,
            ClientEncryptionPolicy clientEncryptionPolicy,
            QueryRequestOptions queryRequestOptions,
            Encryptor encryptor,
            Container container,
            Action<DecryptionResult> decryptionResultHandler,
            string continuationToken = null)
        {
            this.queryDefinition = queryDefinition;
            this.clientEncryptionPolicy = clientEncryptionPolicy;
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
                    if (this.clientEncryptionPolicy != null)
                    {
                        decryptedDocument = await PropertyEncryptionProcessor.DecryptAsync(
                            document,
                            this.encryptor,
                            diagnosticsContext,
                            this.clientEncryptionPolicy,
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
            Dictionary<string, string> passedParamaterNameMap = null;

            // if paramaterized Query was sent else build one.
            if (queryDefinition.Parameters.Count == 0)
            {
                queryDefinition = this.CreateDefinition(this.queryDefinition.QueryText);
            }

            QueryDefinition queryWithEncryptedParameters = new QueryDefinition(queryDefinition.QueryText);

            // when passed via QueryDefinition we need to replace with actual Values.
            string queryTextWithValues = queryDefinition.QueryText;

            if (SqlQueryParser.TryParse(queryTextWithValues, out SqlQuery sqlQuery)
                   && (sqlQuery.WhereClause != null)
                   && (sqlQuery.WhereClause.FilterExpression != null)
                   && (sqlQuery.WhereClause.FilterExpression is SqlBinaryScalarExpression exp)
                   && (queryDefinition.Parameters.Count != 0))
            {
                bool supportedQuery = false;

                // this map is required to identify the Parameter name passed against the SQL parameters in WithParameter Option.
                passedParamaterNameMap = SqlQueryPropertyValueVisitor(exp, ref supportedQuery);
            }

            foreach (KeyValuePair<string, Query.Core.SqlParameter> parameters in queryDefinition.Parameters)
            {
                if (!string.IsNullOrEmpty(parameters.Value.Name) && parameters.Value.Value != null)
                {
                    queryTextWithValues = queryTextWithValues.Replace((string)parameters.Value.Name, parameters.Value.Value.ToString());

                    // if the query definition with parameters had a bunch of encrypted and plain text values
                    // lets replace it initially with plain text and the configured encrypted properties get replaced with encrypted values.
                    queryWithEncryptedParameters.WithParameter(parameters.Value.Name, parameters.Value.Value);
                }
            }

            if (SqlQueryParser.TryParse(queryTextWithValues, out sqlQuery)
                    && (sqlQuery.WhereClause != null)
                    && (sqlQuery.WhereClause.FilterExpression != null)
                    && (sqlQuery.WhereClause.FilterExpression is SqlBinaryScalarExpression expression))
            {
                bool supportedQuery = false;

                // Parse through the sqlQuery and build store of property and its value.Verify if we support the query
                Dictionary<string, string> queryPropertyKeyValueStore = SqlQueryPropertyValueVisitor(expression, ref supportedQuery);

                if (queryPropertyKeyValueStore.Count != 0 && supportedQuery)
                {
                    // we have bunch of parameters that we have indentified,these need to be encrypted before we send out a
                    // query request.
                    // propertyValue should be derived from Our Parsed Query Key. But we lose what we need to replace
                    Dictionary<List<string>, KeyValuePair<List<string>, PropertyEncryptionSetting>> encryptionPolicy = this.clientEncryptionPolicy.ClientEncryptionSetting.ToDictionary(kvp => kvp.Key);

                    foreach (List<string> paths in encryptionPolicy.Keys)
                    {
                        string propertyValue = string.Empty;
                        string propertyName = string.Empty;
                        string passedpropertyName = string.Empty;
                        foreach (string path in paths)
                        {
                            propertyName = path.Substring(1);

                            if (queryPropertyKeyValueStore.ContainsKey(propertyName))
                            {
                                queryPropertyKeyValueStore.TryGetValue(propertyName, out propertyValue);

                                // get the Data Encryption Key configured for this path set.
                                encryptionPolicy.TryGetValue(paths, out KeyValuePair<List<string>, PropertyEncryptionSetting> propertyEncryptionSetting);

                                // string to be converted to the required orginal format stored in encryption settings.
                                dynamic typeConvertedValue = Convert.ChangeType(propertyValue.Trim('"'), propertyEncryptionSetting.Value.PropertyDataType);

                                byte[] plaintext = EncryptionSerializer.GetEncryptionSerializer(
                                    propertyEncryptionSetting.Value.PropertyDataType,
                                    propertyEncryptionSetting.Value.IsSqlCompatible).GetSerializer().Serialize(typeConvertedValue);

                                byte[] ciphertext = await this.encryptor.EncryptAsync(
                                    plaintext,
                                    propertyEncryptionSetting.Value.DataEncryptionKeyId,
                                    propertyEncryptionSetting.Value.EncryptionAlgorithm);

                                if (queryDefinition.Parameters.Count == 0)
                                {
                                    queryWithEncryptedParameters.WithParameter("@" + propertyName, ciphertext);
                                }
                                else
                                {
                                    queryWithEncryptedParameters.WithParameter(passedParamaterNameMap[propertyName], ciphertext);
                                }
                            }
                        }
                    }
                }
            }

            return this.container.GetItemQueryStreamIterator(
                                    queryWithEncryptedParameters,
                                    this.continuationToken,
                                    this.queryRequestOptions);
        }

        internal static Dictionary<string, string> SqlQueryPropertyValueVisitor(
            SqlBinaryScalarExpression query,
            ref bool supportedQuery,
            Dictionary<string, string> queryPropertyKeyValueStore = null)
        {
            if (query == null)
            {
                return queryPropertyKeyValueStore;
            }

            if (queryPropertyKeyValueStore == null)
            {
                queryPropertyKeyValueStore = new Dictionary<string, string>();
                supportedQuery = true;
            }

            if ((query.OperatorKind != SqlBinaryScalarOperatorKind.And && query.OperatorKind != SqlBinaryScalarOperatorKind.Or && query.OperatorKind != SqlBinaryScalarOperatorKind.Equal)
                || supportedQuery == false)
            {
                queryPropertyKeyValueStore.Clear();
                supportedQuery = false;

                // exit.
                return queryPropertyKeyValueStore;
            }

            if ((query.OperatorKind == SqlBinaryScalarOperatorKind.And || query.OperatorKind == SqlBinaryScalarOperatorKind.Or) && supportedQuery == true)
            {
                // update the existing key value store.
                SqlQueryPropertyValueVisitor((SqlBinaryScalarExpression)query.LeftExpression, ref supportedQuery, queryPropertyKeyValueStore);
                SqlQueryPropertyValueVisitor((SqlBinaryScalarExpression)query.RightExpression, ref supportedQuery, queryPropertyKeyValueStore);
            }
            else if (query.OperatorKind == SqlBinaryScalarOperatorKind.Equal && !queryPropertyKeyValueStore.ContainsKey(query.LeftExpression.ToString().Substring(2)) && supportedQuery == true)
            {
                queryPropertyKeyValueStore.Add(query.LeftExpression.ToString().Substring(2), query.RightExpression.ToString());
            }

            return queryPropertyKeyValueStore;
        }

        private QueryDefinition CreateDefinition(string queryText)
        {
            QueryDefinition queryDefinition = new QueryDefinition(queryText);
            string newExpression = queryText;
            Dictionary<string, string> parameterKeyValue = new Dictionary<string, string>();

            if (SqlQueryParser.TryParse(queryText, out SqlQuery sqlQuery)
                    && (sqlQuery.WhereClause != null)
                    && (sqlQuery.WhereClause.FilterExpression != null)
                    && (sqlQuery.WhereClause.FilterExpression is SqlBinaryScalarExpression expression))
            {
                Dictionary<List<string>, KeyValuePair<List<string>, PropertyEncryptionSetting>> encryptionPolicy = this.clientEncryptionPolicy.ClientEncryptionSetting.ToDictionary(kvp => kvp.Key);

                bool supportedQuery = true;

                // Parse through the sqlQuery and build store of property and its values.Verify if we support the query
                Dictionary<string, string> queryPropertyKeyValueStore = SqlQueryPropertyValueVisitor(expression, ref supportedQuery);

                if (queryPropertyKeyValueStore.Count == 0 && supportedQuery)
                {
                    return queryDefinition;
                }

                // for each of the paths to be encrypted identify the properties in
                // the current query and build a new query.
                foreach (List<string> paths in encryptionPolicy.Keys)
                {
                    string propertyValue = null;
                    string propertyName = null;
                    foreach (string path in paths)
                    {
                        propertyValue = string.Empty;
                        propertyName = path.Substring(1);

                        if (queryText.Contains(propertyName))
                        {
                            queryPropertyKeyValueStore.TryGetValue(propertyName, out propertyValue);

                            if (!string.IsNullOrEmpty(propertyValue))
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
