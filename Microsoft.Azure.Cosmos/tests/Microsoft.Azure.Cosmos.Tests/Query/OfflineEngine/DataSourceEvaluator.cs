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
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Microsoft.Azure.Cosmos.SqlObjects.Visitors;

    /// <summary>
    /// This class takes a stream of documents and wraps them with the appropriate identifiers 
    /// so that the query execution downstream does not need a binder.
    /// </summary>
    internal sealed class DataSourceEvaluator : SqlCollectionExpressionVisitor<IEnumerable<CosmosElement>, IEnumerable<CosmosElement>>
    {
        public static readonly DataSourceEvaluator Singleton = new DataSourceEvaluator();

        private DataSourceEvaluator()
        {
        }

        /// <summary>
        /// Visits a SqlAliasedCollectionExpression and transforms the documents accordingly.
        /// </summary>
        /// <param name="collectionExpression">The collection expression to visit.</param>
        /// <param name="documents">The documents to transform.</param>
        /// <returns>The transformed documents according to the collection expression.</returns>
        public override IEnumerable<CosmosElement> Visit(SqlAliasedCollectionExpression collectionExpression, IEnumerable<CosmosElement> documents)
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
                SqlInterpreterCollectionVisitor.Singleton,
                documents);
            string identifer = collectionEvaluationResult.Identifer;

            // Figure out what the alias should be
            int aliasCounter = 0;
            string alias;
            if (collectionExpression.Alias != null)
            {
                alias = collectionExpression.Alias.Value;
            }
            else
            {
                alias = identifer != null ? identifer : $"#{aliasCounter++}";
            }

            // Wrap the collection in the alias so it's easier to bind later
            foreach (Tuple<CosmosElement, string> subDocumentAndRid in collectionEvaluationResult.SubDocumentsAndRids)
            {
                Dictionary<string, CosmosElement> wrappedDocument = new Dictionary<string, CosmosElement>
                {
                    [alias] = subDocumentAndRid.Item1,

                    //// Add the _rid so that we can break ties for sort later
                    ["_rid"] = CosmosString.Create(subDocumentAndRid.Item2)
                };

                yield return CosmosObject.Create(wrappedDocument);
            }
        }

        /// <summary>
        /// Visits a SqlArrayIteratorCollectionExpression and transforms the documents accordingly.
        /// </summary>
        /// <param name="collectionExpression">The collection expression to visit.</param>
        /// <param name="documents">The documents to transform.</param>
        /// <returns>The transformed documents according to the collection expression.</returns>
        public override IEnumerable<CosmosElement> Visit(SqlArrayIteratorCollectionExpression collectionExpression, IEnumerable<CosmosElement> documents)
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
                SqlInterpreterCollectionVisitor.Singleton,
                documents);

            // Wrap the collection in the alias so it's easier to bind later
            foreach (Tuple<CosmosElement, string> subDocumentAndRid in collectionEvaluationResult.SubDocumentsAndRids)
            {
                CosmosElement document = subDocumentAndRid.Item1;
                string rid = subDocumentAndRid.Item2;
                if (document is CosmosArray array)
                {
                    foreach (CosmosElement item in array)
                    {
                        Dictionary<string, CosmosElement> wrappedDocument = new Dictionary<string, CosmosElement>
                        {
                            [collectionExpression.Identifier.Value] = item,

                            //// Add the _rid so that we can break ties for sort later
                            ["_rid"] = CosmosString.Create(rid),
                        };
                        yield return CosmosObject.Create(wrappedDocument);
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
        public override IEnumerable<CosmosElement> Visit(SqlJoinCollectionExpression collectionExpression, IEnumerable<CosmosElement> documents)
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
            IEnumerable<CosmosElement> collection1 = collectionExpression.Left.Accept(this, documents);

            // Perform the select many and merge the documents
            foreach (CosmosObject document1 in collection1)
            {
                IEnumerable<CosmosElement> collection2 = collectionExpression.Right.Accept(this, new CosmosElement[] { document1 });
                foreach (CosmosObject document2 in collection2)
                {
                    Dictionary<string, CosmosElement> mergedDocument = new Dictionary<string, CosmosElement>();
                    foreach (KeyValuePair<string, CosmosElement> property in document1)
                    {
                        if (property.Key != "_rid")
                        {
                            mergedDocument[property.Key] = property.Value;
                        }
                    }

                    foreach (KeyValuePair<string, CosmosElement> property in document2)
                    {
                        if (property.Key != "_rid")
                        {
                            mergedDocument[property.Key] = property.Value;
                        }
                    }

                    // Add the _rid at the end so we can break ties in the sort later.
                    mergedDocument["_rid"] = document1["_rid"];

                    yield return CosmosObject.Create(mergedDocument);
                }
            }
        }

        private struct CollectionEvaluationResult
        {
            public CollectionEvaluationResult(
                IEnumerable<Tuple<CosmosElement, string>> subDocumentsAndRids,
                string identifer)
            {
                this.SubDocumentsAndRids = subDocumentsAndRids;
                this.Identifer = identifer;
            }

            public IEnumerable<Tuple<CosmosElement, string>> SubDocumentsAndRids { get; }

            public string Identifer { get; }
        }

        private sealed class SqlInterpreterCollectionVisitor : SqlCollectionVisitor<IEnumerable<CosmosElement>, CollectionEvaluationResult>
        {
            public static readonly SqlInterpreterCollectionVisitor Singleton = new SqlInterpreterCollectionVisitor();

            public SqlInterpreterCollectionVisitor()
            {
            }

            public override CollectionEvaluationResult Visit(SqlSubqueryCollection collection, IEnumerable<CosmosElement> input)
            {
                List<Tuple<CosmosElement, string>> subDocumentsAndRids = new List<Tuple<CosmosElement, string>>();
                foreach (CosmosObject document in input)
                {
                    string rid = ((CosmosString)document["_rid"]).Value;
                    IEnumerable<CosmosElement> subqueryResults = SqlInterpreter.ExecuteQuery(
                        new CosmosElement[] { document },
                        collection.Query);
                    foreach (CosmosElement subqueryResult in subqueryResults)
                    {
                        subDocumentsAndRids.Add(new Tuple<CosmosElement, string>(subqueryResult, rid));
                    }
                }

                return new CollectionEvaluationResult(subDocumentsAndRids, null);
            }

            public override CollectionEvaluationResult Visit(SqlInputPathCollection collection, IEnumerable<CosmosElement> documents)
            {
                List<Either<long, string>> tokens = new List<Either<long, string>>();
                if (collection.RelativePath != null)
                {
                    SqlInterpreterPathExpressionVisitor pathExpressionVisitor = new SqlInterpreterPathExpressionVisitor(tokens);
                    collection.RelativePath.Accept(pathExpressionVisitor);
                }

                tokens.Add(collection.Input.Value);

                // Need to reverse the tokens, since the path is evaluated from the tail to the head.
                tokens.Reverse();

                IEnumerable<Tuple<CosmosElement, string>> subDocumentsAndRids = documents
                    .Select((document) => ApplyPath(document, tokens))
                    .Where((subDocumentsAndRid) => subDocumentsAndRid.Item1 != null);
                return new CollectionEvaluationResult(subDocumentsAndRids, tokens.Last().FromRight(null));
            }

            private static Tuple<CosmosElement, string> ApplyPath(CosmosElement document, IEnumerable<Either<long, string>> tokens)
            {
                CosmosObject documentAsObject = (CosmosObject)document;
                if (!documentAsObject.TryGetValue<CosmosString>("_rid", out CosmosString ridToken))
                {
                    throw new ArgumentException("Document did not have a '_rid' field.");
                }

                string rid = ridToken.Value;

                // First token in optional
                Either<long, string> firstToken = tokens.First();
                firstToken.Match(
                    (long longToken) => throw new InvalidOperationException(),
                    (string stringToken) =>
                    {
                        if (documentAsObject.TryGetValue(stringToken, out CosmosElement value))
                        {
                            document = value;
                        }
                    });

                foreach (Either<long, string> token in tokens.Skip(1))
                {
                    if (document == null)
                    {
                        break;
                    }

                    token.Match(
                        (long arrayIndex) =>
                        {
                            CosmosArray arraySubDocument = (CosmosArray)document;
                            document = arrayIndex >= arraySubDocument.Count ? null : arraySubDocument[(int)arrayIndex];
                        },
                        (string propertyName) =>
                        {
                            CosmosObject objectSubDocument = (CosmosObject)document;
                            if (!objectSubDocument.TryGetValue(propertyName, out document))
                            {
                                document = null;
                            }
                        });
                }

                return new Tuple<CosmosElement, string>(document, rid);
            }

            private sealed class SqlInterpreterPathExpressionVisitor : SqlPathExpressionVisitor<IEnumerable<Either<long, string>>>
            {
                private readonly List<Either<long, string>> tokens;

                public SqlInterpreterPathExpressionVisitor(List<Either<long, string>> tokens)
                {
                    this.tokens = tokens;
                }

                public override IEnumerable<Either<long, string>> Visit(SqlIdentifierPathExpression sqlObject)
                {
                    this.tokens.Add(sqlObject.Value.Value);
                    sqlObject.ParentPath?.Accept(this);
                    return this.tokens;
                }

                public override IEnumerable<Either<long, string>> Visit(SqlNumberPathExpression sqlObject)
                {
                    this.tokens.Add(Number64.ToLong(sqlObject.Value.Value));
                    sqlObject.ParentPath?.Accept(this);
                    return this.tokens;
                }

                public override IEnumerable<Either<long, string>> Visit(SqlStringPathExpression sqlObject)
                {
                    this.tokens.Add(sqlObject.Value.Value);
                    sqlObject.ParentPath?.Accept(this);
                    return this.tokens;
                }
            }
        }
    }
}