//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Parser;
    using Microsoft.Azure.Cosmos.SqlObjects;

    internal sealed class EncryptionFeedIterator : FeedIterator
    {
        private readonly FeedIterator feedIterator;
        private readonly EncryptionContainer encryptionContainer;
        private readonly RequestOptions requestOptions;
        private readonly QueryDefinition queryDefinition;

        public EncryptionFeedIterator(
            FeedIterator feedIterator,
            EncryptionContainer encryptionContainer,
            RequestOptions requestOptions,
            QueryDefinition queryDefinition = null)
        {
            this.feedIterator = feedIterator ?? throw new ArgumentNullException(nameof(feedIterator));
            this.encryptionContainer = encryptionContainer ?? throw new ArgumentNullException(nameof(encryptionContainer));
            this.requestOptions = requestOptions ?? throw new ArgumentNullException(nameof(requestOptions));
            this.queryDefinition = queryDefinition;
        }

        public override bool HasMoreResults => this.feedIterator.HasMoreResults;

        public override async Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(options: null);
            using (diagnosticsContext.CreateScope("FeedIterator.ReadNext"))
            {
                EncryptionSettings encryptionSettings = await this.encryptionContainer.GetOrUpdateEncryptionSettingsFromCacheAsync(obsoleteEncryptionSettings: null, cancellationToken: cancellationToken);
                encryptionSettings.SetRequestHeaders(this.requestOptions);

                ResponseMessage responseMessage = await this.feedIterator.ReadNextAsync(cancellationToken);

                // check for Bad Request and Wrong RID intended and update the cached RID and Client Encryption Policy.
                if (responseMessage.StatusCode == HttpStatusCode.BadRequest
                    && string.Equals(responseMessage.Headers.Get(Constants.SubStatusHeader), Constants.IncorrectContainerRidSubStatus))
                {
                    await this.encryptionContainer.GetOrUpdateEncryptionSettingsFromCacheAsync(
                       obsoleteEncryptionSettings: encryptionSettings,
                       cancellationToken: cancellationToken);

                    throw new CosmosException(
                        "Operation has failed due to a possible mismatch in Client Encryption Policy configured on the container. Please refer to https://aka.ms/CosmosClientEncryption for more details. " + responseMessage.ErrorMessage,
                        responseMessage.StatusCode,
                        int.Parse(Constants.IncorrectContainerRidSubStatus),
                        responseMessage.Headers.ActivityId,
                        responseMessage.Headers.RequestCharge);
                }

                if (responseMessage.IsSuccessStatusCode && responseMessage.Content != null)
                {
                    Dictionary<string, string> rootPaths = null;
                    if (this.queryDefinition != null)
                    {
                        rootPaths = this.BuildRootPathsForQuery(this.queryDefinition);
                    }

                    Stream decryptedContent = await this.encryptionContainer.DeserializeAndDecryptResponseAsync(
                        responseMessage.Content,
                        encryptionSettings,
                        cancellationToken,
                        rootPaths: rootPaths);

                    return new DecryptedResponseMessage(responseMessage, decryptedContent);
                }

                return responseMessage;
            }
        }

        private Dictionary<string, string> BuildRootPathsForQuery(QueryDefinition queryDefinition)
        {
            Dictionary<string, string> keyValuePairs = null;
            if (SqlQueryParser.TryParse(queryDefinition.QueryText, out SqlQuery sqlQuery))
            {
                // select values from documents.
                if (sqlQuery.SelectClause.SelectSpec is SqlSelectListSpec sqlSelectListSpec)
                {
                    keyValuePairs = new Dictionary<string, string>();
                    foreach (SqlSelectItem item in sqlSelectListSpec.Items)
                    {
                        if (item.Alias != null)
                        {
                            throw new NotSupportedException("Encryption Package currently does not support queries with Alias. Please refer to https://aka.ms/CosmosClientEncryption for more details. ");
                        }

                        if (item.Expression is SqlPropertyRefScalarExpression sqlPropertyRefScalarExpression)
                        {
                            string rootPath = SqlScalarExpressionVisitor(sqlPropertyRefScalarExpression);
                            keyValuePairs.Add(sqlPropertyRefScalarExpression.ToString(), rootPath);
                        }

                        if (item.Expression is SqlFunctionCallScalarExpression)
                        {
                            // Count() etc
                        }
                    }

                    return keyValuePairs;
                }

                // select * from c/objects.
                if (sqlQuery.SelectClause.SelectSpec is SqlSelectStarSpec && sqlQuery.FromClause.Expression is SqlAliasedCollectionExpression sqlAliasedCollectionExpression)
                {
                    if (sqlAliasedCollectionExpression.Alias != null)
                    {
                        throw new NotSupportedException("Encryption Package currently does not support queries with Alias. Please refer to https://aka.ms/CosmosClientEncryption for more details. ");
                    }

                    if (sqlAliasedCollectionExpression.Collection is SqlInputPathCollection sqlInputPathCollection)
                    {
                        if (sqlInputPathCollection.RelativePath != null)
                        {
                            keyValuePairs = new Dictionary<string, string>();
                            string rootPath = SqlPathExpressionVisitor(sqlInputPathCollection.RelativePath);
                            keyValuePairs.Add(sqlInputPathCollection.RelativePath.ToString(), rootPath);
                            return keyValuePairs;
                        }
                    }
                }

                // select * from arrays in document.
                if (sqlQuery.SelectClause.SelectSpec is SqlSelectStarSpec && sqlQuery.FromClause.Expression is SqlArrayIteratorCollectionExpression sqlArrayIteratorCollectionExpression)
                {
                    if (sqlArrayIteratorCollectionExpression.Collection is SqlInputPathCollection sqlInputPathCollection)
                    {
                        if (sqlInputPathCollection.RelativePath != null)
                        {
                            keyValuePairs = new Dictionary<string, string>();
                            string rootPath = SqlPathExpressionVisitor(sqlInputPathCollection.RelativePath);
                            keyValuePairs.Add(sqlInputPathCollection.RelativePath.ToString(), rootPath);
                            return keyValuePairs;
                        }
                    }
                }

                // select distinct/values from c.
                if (sqlQuery.SelectClause.SelectSpec is SqlSelectValueSpec sqlSelectValueSpec)
                {
                    if (sqlSelectValueSpec.Expression is SqlPropertyRefScalarExpression sqlPropertyRefScalarExpression)
                    {
                        keyValuePairs = new Dictionary<string, string>();
                        string rootPath = SqlScalarExpressionVisitor(sqlPropertyRefScalarExpression);
                        keyValuePairs.Add(sqlPropertyRefScalarExpression.ToString(), rootPath);
                        return keyValuePairs;
                    }
                }
            }

            return keyValuePairs;
        }

        private static string SqlScalarExpressionVisitor(SqlPropertyRefScalarExpression sqlScalarExpression)
        {
            if (sqlScalarExpression == null)
            {
                return null;
            }

            if (sqlScalarExpression.Member == null)
            {
                return null;
            }

            SqlPropertyRefScalarExpression sqlPropertyRefScalarExpression = (SqlPropertyRefScalarExpression)sqlScalarExpression.Member;
            if (sqlPropertyRefScalarExpression.Member == null)
            {
                return sqlScalarExpression.Identifier.Value;
            }

            return SqlScalarExpressionVisitor((SqlPropertyRefScalarExpression)sqlScalarExpression.Member);
        }

        private static string SqlPathExpressionVisitor(SqlPathExpression sqlPathExpression)
        {
            if (sqlPathExpression == null)
            {
                return null;
            }

            if (sqlPathExpression.ParentPath == null)
            {
                SqlIdentifierPathExpression sqlIdentifierPathExpression = (SqlIdentifierPathExpression)sqlPathExpression;
                return sqlIdentifierPathExpression.Value.Value;
            }

            return SqlPathExpressionVisitor((SqlPathExpression)sqlPathExpression.ParentPath);
        }
    }
}
