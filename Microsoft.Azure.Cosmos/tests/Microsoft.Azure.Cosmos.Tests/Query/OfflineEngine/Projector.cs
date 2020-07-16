//-----------------------------------------------------------------------
// <copyright file="Projector.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests.Query.OfflineEngine
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Microsoft.Azure.Cosmos.SqlObjects.Visitors;
    using Newtonsoft.Json.Linq;

    internal sealed class Projector : SqlSelectSpecVisitor<CosmosElement, CosmosElement>
    {
        public static readonly Projector Singleton = new Projector();

        private Projector()
        {
        }

        public override CosmosElement Visit(SqlSelectListSpec selectSpec, CosmosElement document)
        {
            if (selectSpec == null)
            {
                throw new ArgumentNullException(nameof(selectSpec));
            }

            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            Dictionary<string, CosmosElement> dictionary = new Dictionary<string, CosmosElement>();

            int aliasCounter = 1;
            foreach (SqlSelectItem sqlSelectItem in selectSpec.Items)
            {
                CosmosElement value = sqlSelectItem.Expression.Accept(ScalarExpressionEvaluator.Singleton, document);
                if (value != null)
                {
                    string key = default;
                    if (sqlSelectItem.Alias != null)
                    {
                        key = sqlSelectItem.Alias.Value;
                    }
                    else
                    {
                        if (sqlSelectItem.Expression is SqlMemberIndexerScalarExpression memberIndexer)
                        {
                            SqlScalarExpression lastToken = this.GetLastMemberIndexerToken(memberIndexer);

                            if (lastToken is SqlLiteralScalarExpression literalScalarExpression)
                            {
                                if (literalScalarExpression.Literal is SqlStringLiteral stringLiteral)
                                {
                                    key = stringLiteral.Value;
                                }
                            }
                        }
                    }

                    if (key == default)
                    {
                        key = $"${aliasCounter++}";
                    }

                    dictionary[key] = value;
                }
            }

            return CosmosObject.Create(dictionary);
        }

        public override CosmosElement Visit(SqlSelectStarSpec selectSpec, CosmosElement document)
        {
            if (selectSpec == null)
            {
                throw new ArgumentNullException(nameof(selectSpec));
            }

            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            CosmosObject documentAsObject = (CosmosObject)document;

            Dictionary<string, CosmosElement> properties = new Dictionary<string, CosmosElement>();
            foreach (KeyValuePair<string, CosmosElement> kvp in documentAsObject)
            {
                // remove the _rid since it's not needed at this point.
                if (kvp.Key != "_rid")
                {
                    properties[kvp.Key] = kvp.Value;
                }
            }

            // if the document only has one property then it means
            // that the object was wrapped for binding and needs to be unwrapped.
            if (properties.Count != 1)
            {
                throw new ArgumentException("Tried to run SELECT * on a JOIN query");
            }

            return properties.First().Value;
        }

        public override CosmosElement Visit(SqlSelectValueSpec selectSpec, CosmosElement document)
        {
            if (selectSpec == null)
            {
                throw new ArgumentNullException(nameof(selectSpec));
            }

            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            return selectSpec.Expression.Accept(ScalarExpressionEvaluator.Singleton, document);
        }

        private SqlScalarExpression GetLastMemberIndexerToken(SqlMemberIndexerScalarExpression memberIndexer)
        {
            SqlScalarExpression last;
            if (!(memberIndexer.Indexer is SqlMemberIndexerScalarExpression memberIndexerExpression))
            {
                last = memberIndexer.Indexer;
            }
            else
            {
                last = this.GetLastMemberIndexerToken(memberIndexerExpression);
            }

            return last;
        }
    }
}
