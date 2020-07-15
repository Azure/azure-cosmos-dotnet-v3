//-----------------------------------------------------------------------
// <copyright file="DataSourceEvaluator.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests.Query.OfflineEngine
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Microsoft.Azure.Cosmos.SqlObjects.Visitors;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// This class takes a stream of documents and wraps them with the appropriate identifiers 
    /// so that the query execution downstream does not need a binder.
    /// </summary>
    internal sealed class DataSourceEvaluator : SqlCollectionExpressionVisitor<IEnumerable<JToken>, IEnumerable<JToken>>
    {
        private readonly CollectionConfigurations collectionConfigurations;

        private DataSourceEvaluator(CollectionConfigurations collectionConfigurations)
        {
            if (collectionConfigurations == null)
            {
                throw new ArgumentNullException(nameof(collectionConfigurations));
            }

            this.collectionConfigurations = collectionConfigurations;
        }

        /// <summary>
        /// Visits a SqlAliasedCollectionExpression and transforms the documents accordingly.
        /// </summary>
        /// <param name="collectionExpression">The collection expression to visit.</param>
        /// <param name="documents">The documents to transform.</param>
        /// <returns>The transformed documents according to the collection expression.</returns>
        public override IEnumerable<JToken> Visit(SqlAliasedCollectionExpression collectionExpression, IEnumerable<JToken> documents)
        {
            //If the query was:

            //SELECT *
            //FROM c

            //and the document was:
            //{
            //    "name" : "John"
            //}

            //Then this function would return:
            //{
            //    "c" : { "name" : "John" }
            //}

            //which is just wrapping the original document in the identifier.

            // Get the sub collection
            CollectionEvaluationResult collectionEvaluationResult = collectionExpression.Collection.Accept(
                SqlInterpreterCollectionVisitor.Create(this.collectionConfigurations),
                documents);
            string identifer = collectionEvaluationResult.Identifer;

            // Figure out what the alias should be
            int aliasCounter = 0;
            string alias;
            if (collectionExpression.Alias != null)
            {
                alias = collectionExpression.Alias.Value;
            }
            else if (identifer != null)
            {
                alias = identifer;
            }
            else
            {
                alias = $"#{aliasCounter++}";
            }

            // Wrap the collection in the alias so it's easier to bind later
            foreach (Tuple<JToken, string> subDocumentAndRid in collectionEvaluationResult.SubDocumentsAndRids)
            {
                JToken wrappedDocument = new JObject();
                wrappedDocument[alias] = subDocumentAndRid.Item1;

                //// Add the _rid so that we can break ties for sort later
                wrappedDocument["_rid"] = subDocumentAndRid.Item2;
                yield return wrappedDocument;
            }
        }

        /// <summary>
        /// Visits a SqlArrayIteratorCollectionExpression and transforms the documents accordingly.
        /// </summary>
        /// <param name="collectionExpression">The collection expression to visit.</param>
        /// <param name="documents">The documents to transform.</param>
        /// <returns>The transformed documents according to the collection expression.</returns>
        public override IEnumerable<JToken> Visit(SqlArrayIteratorCollectionExpression collectionExpression, IEnumerable<JToken> documents)
        {
            //If the query was:

            //SELECT p
            //FROM p in c.parents

            //and the document was
            //{
            //    "parents" : [{"name" : "John"}, {"name" : "Sally"}]
            //}

            //then the results would be:

            //{ "p" : {"name" : "John"} }

            //and

            //{ "p" : {"name" : "Sally"} }

            //Notice that the result set is larger than the input 
            //This is because we emitted one document for each parent in the original document
            //and wrapped it in the provided alias.

            // throw away the identifer since we always have an alias
            CollectionEvaluationResult collectionEvaluationResult = collectionExpression.Collection.Accept(
                SqlInterpreterCollectionVisitor.Create(this.collectionConfigurations),
                documents);

            // Wrap the collection in the alias so it's easier to bind later
            foreach (Tuple<JToken, string> subDocumentAndRid in collectionEvaluationResult.SubDocumentsAndRids)
            {
                JToken document = subDocumentAndRid.Item1;
                string rid = subDocumentAndRid.Item2;
                if (document.Type == JTokenType.Array)
                {
                    JArray array = (JArray)document;
                    foreach (JToken item in array)
                    {
                        JToken wrappedDocument = new JObject
                        {
                            [collectionExpression.Alias.Value] = item,

                            //// Add the _rid so that we can break ties for sort later
                            ["_rid"] = rid
                        };
                        yield return wrappedDocument;
                    }
                }
            }
        }

        /// <summary>
        /// Visits a SqlJoinCollectionExpression and transforms the documents accordingly.
        /// </summary>
        /// <param name="collectionExpression">The collection expression to visit.</param>
        /// <param name="documents">The documents to transform.</param>
        /// <returns>The transformed documents according to the collection expression.</returns>
        public override IEnumerable<JToken> Visit(SqlJoinCollectionExpression collectionExpression, IEnumerable<JToken> documents)
        {
            //If the query was:

            //SELECT blah
            //FROM f
            //JOIN c IN f.children

            //and the document was
            //{
            //    "children" : [{"name" : "John"}, {"name" : "Sally"}]
            //}

            //then the results would be:

            //{ 
            //    "f" : { "children" : [{"name" : "John"}, {"name" : "Sally"}] } 
            //    "c" : {"name" : "John"}
            //}

            //and

            //{ 
            //    "f" : { "children" : [{"name" : "John"}, {"name" : "Sally"}] } 
            //    "c" : {"name" : "Sally"}
            //}

            //Notice that the result set is larger than the input 
            //This is because we emitted one document for each child in the original document
            //wrapped it with the provided alias 
            //and merged the results from the source expression.
            IEnumerable<JToken> collection1 = collectionExpression.Left.Accept(this, documents);

            // Perform the select many and merge the documents
            foreach (JToken document1 in collection1)
            {
                IEnumerable<JToken> collection2 = collectionExpression.Right.Accept(this, new JToken[] { document1 });
                foreach (JToken document2 in collection2)
                {
                    JObject mergedDocument = new JObject();
                    foreach (JProperty property in document1)
                    {
                        if (property.Name != "_rid")
                        {
                            mergedDocument.Add(property);
                        }
                    }

                    foreach (JProperty property in document2)
                    {
                        if (property.Name != "_rid")
                        {
                            mergedDocument.Add(property);
                        }
                    }

                    // Add the _rid at the end so we can break ties in the sort later.
                    mergedDocument["_rid"] = document1["_rid"];

                    yield return mergedDocument;
                }
            }
        }

        public static DataSourceEvaluator Create(CollectionConfigurations collectionConfigurations)
        {
            return new DataSourceEvaluator(collectionConfigurations);
        }

        private struct CollectionEvaluationResult
        {
            public CollectionEvaluationResult(
                IEnumerable<Tuple<JToken, string>> subDocumentsAndRids,
                string identifer)
            {
                this.SubDocumentsAndRids = subDocumentsAndRids;
                this.Identifer = identifer;
            }

            public IEnumerable<Tuple<JToken, string>> SubDocumentsAndRids { get; }

            public string Identifer { get; }
        }

        private sealed class SqlInterpreterCollectionVisitor : SqlCollectionVisitor<IEnumerable<JToken>, CollectionEvaluationResult>
        {
            private readonly CollectionConfigurations collectionConfigurations;

            public SqlInterpreterCollectionVisitor(CollectionConfigurations collectionConfigurations)
            {
                if (collectionConfigurations == null)
                {
                    throw new ArgumentNullException(nameof(collectionConfigurations));
                }

                this.collectionConfigurations = collectionConfigurations;
            }

            public override CollectionEvaluationResult Visit(SqlSubqueryCollection collection, IEnumerable<JToken> input)
            {
                List<Tuple<JToken, string>> subDocumentsAndRids = new List<Tuple<JToken, string>>();
                foreach (JToken document in input)
                {
                    string rid = document["_rid"].Value<string>();
                    IEnumerable<JToken> subqueryResults = SqlInterpreter.ExecuteQuery(
                        new JToken[] { document },
                        collection.Query,
                        this.collectionConfigurations);
                    foreach (JToken subqueryResult in subqueryResults)
                    {
                        subDocumentsAndRids.Add(new Tuple<JToken, string>(subqueryResult, rid));
                    }
                }

                return new CollectionEvaluationResult(subDocumentsAndRids, null);
            }

            public override CollectionEvaluationResult Visit(SqlInputPathCollection collection, IEnumerable<JToken> documents)
            {
                List<object> tokens = new List<object>();
                if (collection.RelativePath != null)
                {
                    SqlInterpreterPathExpressionVisitor pathExpressionVisitor = new SqlInterpreterPathExpressionVisitor(tokens);
                    collection.RelativePath.Accept(pathExpressionVisitor);
                }

                tokens.Add(collection.Input.Value);

                // Need to reverse the tokens, since the path is evaluated from the tail to the head.
                tokens.Reverse();

                IEnumerable<Tuple<JToken, string>> subDocumentsAndRids = documents
                    .Select((document) => ApplyPath(document, tokens))
                    .Where((subDocumentsAndRid) => subDocumentsAndRid.Item1 != null);
                return new CollectionEvaluationResult(subDocumentsAndRids, tokens.Last().ToString());
            }

            private static Tuple<JToken, string> ApplyPath(JToken document, IEnumerable<object> tokens)
            {
                JToken ridToken = document["_rid"];
                if (ridToken == null)
                {
                    throw new ArgumentException("Document did not have a '_rid' field.");
                }

                string rid = ridToken.Value<string>();

                // First token in optional
                object firstToken = tokens.First();
                if (document[firstToken] != null)
                {
                    document = document[firstToken];
                }

                foreach (object token in tokens.Skip(1))
                {
                    if (document == null)
                    {
                        break;
                    }

                    if (token is string propertyName)
                    {
                        JObject objectSubDocument = (JObject)document;
                        document = objectSubDocument[propertyName];
                    }
                    else if (token is long arrayIndex)
                    {
                        JArray arraySubDocument = (JArray)document;
                        if (arrayIndex >= arraySubDocument.Count())
                        {
                            document = null;
                        }
                        else
                        {
                            document = arraySubDocument[(int)arrayIndex];
                        }
                    }
                    else
                    {
                        throw new ArgumentException($"Unknown token type: {token.GetType()}");
                    }
                }

                return new Tuple<JToken, string>(document, rid);
            }

            private sealed class SqlInterpreterPathExpressionVisitor : SqlPathExpressionVisitor<IEnumerable<object>>
            {
                private readonly List<object> tokens;

                public SqlInterpreterPathExpressionVisitor(List<object> tokens)
                {
                    this.tokens = tokens;
                }

                public override IEnumerable<object> Visit(SqlIdentifierPathExpression sqlObject)
                {
                    this.tokens.Add(sqlObject.Value.Value);
                    sqlObject.ParentPath?.Accept(this);
                    return this.tokens;
                }

                public override IEnumerable<object> Visit(SqlNumberPathExpression sqlObject)
                {
                    this.tokens.Add(Number64.ToLong(sqlObject.Value.Value));
                    sqlObject.ParentPath?.Accept(this);
                    return this.tokens;
                }

                public override IEnumerable<object> Visit(SqlStringPathExpression sqlObject)
                {
                    this.tokens.Add(sqlObject.Value.Value);
                    sqlObject.ParentPath?.Accept(this);
                    return this.tokens;
                }
            }

            public static SqlInterpreterCollectionVisitor Create(CollectionConfigurations collectionConfigurations)
            {
                return new SqlInterpreterCollectionVisitor(collectionConfigurations);
            }
        }
    }
}
