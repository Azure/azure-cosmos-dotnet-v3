//-----------------------------------------------------------------------
// <copyright file="Projector.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests.Query.OfflineEngine
{
    using System;
    using System.Linq;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Microsoft.Azure.Cosmos.SqlObjects.Visitors;
    using Newtonsoft.Json.Linq;

    internal sealed class Projector : SqlSelectSpecVisitor<JToken, JToken>
    {
        private readonly ScalarExpressionEvaluator scalarExpressionEvaluator;

        private Projector(ScalarExpressionEvaluator scalarExpressionEvaluator)
        {
            if (scalarExpressionEvaluator == null)
            {
                throw new ArgumentNullException(nameof(scalarExpressionEvaluator));
            }

            this.scalarExpressionEvaluator = scalarExpressionEvaluator;
        }

        public override JToken Visit(SqlSelectListSpec selectSpec, JToken document)
        {
            JObject jObject = new JObject();

            int aliasCounter = 1;
            foreach (SqlSelectItem sqlSelectItem in selectSpec.Items)
            {
                JToken value = sqlSelectItem.Expression.Accept(this.scalarExpressionEvaluator, document);
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

                    jObject[key] = value;
                }
            }

            return jObject;
        }

        public override JToken Visit(SqlSelectStarSpec selectSpec, JToken input)
        {
            JToken result;
            JObject document = (JObject)input;

            // remove the _rid since it's not needed at this point.
            document.Remove("_rid");

            // if the document only has one property then it means
            // that the object was wrapped for binding and needs to be unwrapped.
            if (document.Properties().Count() == 1)
            {
                result = document.Properties().First().Value;
            }
            else
            {
                throw new ArgumentException("Tried to run SELECT * on a JOIN query");
            }

            return result;
        }

        public override JToken Visit(SqlSelectValueSpec selectSpec, JToken document)
        {
            return selectSpec.Expression.Accept(this.scalarExpressionEvaluator, document);
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

        public static Projector Create(CollectionConfigurations collectionConfigurations)
        {
            return new Projector(ScalarExpressionEvaluator.Create(collectionConfigurations));
        }
    }
}
