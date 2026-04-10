//-----------------------------------------------------------------------
// <copyright file="AggregateProjectionTransformer.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests.Query.OfflineEngine
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Microsoft.Azure.Cosmos.SqlObjects.Visitors;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Transforms a projection with an aggregate to be rewritten with the result.
    /// For example if the projection is SELECT SQUARE(AVG(c.age))
    /// it might be rewritten as SELECT SQUARE(43)
    /// And this is because aggregates need to enter a separate pipeline compared to all other queries.
    /// </summary>
    internal sealed class AggregateProjectionTransformer
    {
        private static readonly CosmosElement Undefined = CosmosUndefined.Create();

        private readonly AggregateProjectionTransformerVisitor visitor;

        public AggregateProjectionTransformer(
            IEnumerable<CosmosElement> dataSource)
        {
            this.visitor = new AggregateProjectionTransformerVisitor(dataSource);
        }

        public SqlSelectSpec TransformAggregatesInProjection(SqlSelectSpec selectSpec)
        {
            return selectSpec.Accept(this.visitor);
        }

        private sealed class AggregateProjectionTransformerVisitor : SqlSelectSpecVisitor<SqlSelectSpec>
        {
            private readonly AggregateScalarExpressionTransformer scalarExpressionTransformer;

            public AggregateProjectionTransformerVisitor(IEnumerable<CosmosElement> dataSource)
            {
                this.scalarExpressionTransformer = new AggregateScalarExpressionTransformer(dataSource);
            }

            public override SqlSelectSpec Visit(SqlSelectListSpec selectSpec)
            {
                List<SqlSelectItem> selectItems = new List<SqlSelectItem>();
                foreach (SqlSelectItem selectItem in selectSpec.Items)
                {
                    selectItems.Add(SqlSelectItem.Create(
                        selectItem.Expression.Accept(this.scalarExpressionTransformer),
                        selectItem.Alias));
                }

                return SqlSelectListSpec.Create(selectItems.ToImmutableArray());
            }

            public override SqlSelectSpec Visit(SqlSelectStarSpec selectSpec)
            {
                return selectSpec;
            }

            public override SqlSelectSpec Visit(SqlSelectValueSpec selectSpec)
            {
                return SqlSelectValueSpec.Create(selectSpec.Expression.Accept(this.scalarExpressionTransformer));
            }

            private sealed class AggregateScalarExpressionTransformer : SqlScalarExpressionVisitor<SqlScalarExpression>
            {
                private readonly IEnumerable<CosmosElement> dataSource;

                public AggregateScalarExpressionTransformer(IEnumerable<CosmosElement> dataSource)
                {
                    this.dataSource = dataSource;
                }

                public override SqlScalarExpression Visit(SqlAllScalarExpression sqlAllScalarExpression)
                {
                    // No need to worry about aggregates within the subquery (they will recursively get rewritten).
                    return sqlAllScalarExpression;
                }

                public override SqlScalarExpression Visit(SqlArrayCreateScalarExpression sqlArrayCreateScalarExpression)
                {
                    List<SqlScalarExpression> items = new List<SqlScalarExpression>();
                    foreach (SqlScalarExpression item in sqlArrayCreateScalarExpression.Items)
                    {
                        items.Add(item.Accept(this));
                    }

                    return SqlArrayCreateScalarExpression.Create(items.ToImmutableArray());
                }

                public override SqlScalarExpression Visit(SqlArrayScalarExpression sqlArrayScalarExpression)
                {
                    // No need to worry about aggregates in the subquery (they will recursively get rewritten).
                    return sqlArrayScalarExpression;
                }

                public override SqlScalarExpression Visit(SqlBetweenScalarExpression sqlBetweenScalarExpression)
                {
                    return SqlBetweenScalarExpression.Create(
                        sqlBetweenScalarExpression.Expression.Accept(this),
                        sqlBetweenScalarExpression.StartInclusive.Accept(this),
                        sqlBetweenScalarExpression.EndInclusive.Accept(this),
                        sqlBetweenScalarExpression.Not);
                }

                public override SqlScalarExpression Visit(SqlBinaryScalarExpression sqlBinaryScalarExpression)
                {
                    return SqlBinaryScalarExpression.Create(
                        sqlBinaryScalarExpression.OperatorKind,
                        sqlBinaryScalarExpression.LeftExpression.Accept(this),
                        sqlBinaryScalarExpression.RightExpression.Accept(this));
                }

                public override SqlScalarExpression Visit(SqlCoalesceScalarExpression sqlCoalesceScalarExpression)
                {
                    return SqlCoalesceScalarExpression.Create(
                        sqlCoalesceScalarExpression.Left.Accept(this),
                        sqlCoalesceScalarExpression.Right.Accept(this));
                }

                public override SqlScalarExpression Visit(SqlConditionalScalarExpression sqlConditionalScalarExpression)
                {
                    return SqlConditionalScalarExpression.Create(
                        sqlConditionalScalarExpression.Condition.Accept(this),
                        sqlConditionalScalarExpression.Consequent.Accept(this),
                        sqlConditionalScalarExpression.Alternative.Accept(this));
                }

                public override SqlScalarExpression Visit(SqlExistsScalarExpression sqlExistsScalarExpression)
                {
                    // No need to worry about aggregates within the subquery (they will recursively get rewritten).
                    return sqlExistsScalarExpression;
                }

                public override SqlScalarExpression Visit(SqlFirstScalarExpression sqlFirstScalarExpression)
                {
                    // No need to worry about aggregates within the subquery (they will recursively get rewritten).
                    return sqlFirstScalarExpression;
                }

                public override SqlScalarExpression Visit(SqlFunctionCallScalarExpression sqlFunctionCallScalarExpression)
                {
                    SqlScalarExpression rewrittenExpression;

                    // If the function call is an aggregate just evaluate the aggregate first and return that
                    if (
                        !sqlFunctionCallScalarExpression.IsUdf &&
                        Enum.TryParse(value: sqlFunctionCallScalarExpression.Name.Value, ignoreCase: true, result: out Aggregate aggregate))
                    {
                        IReadOnlyList<SqlScalarExpression> arguments = sqlFunctionCallScalarExpression.Arguments;
                        if (arguments.Count != 1)
                        {
                            throw new ArgumentException("Aggregates only accept one argument.");
                        }

                        IEnumerable<CosmosElement> results = this.dataSource
                            .Select((element) => arguments[0].Accept(
                                ScalarExpressionEvaluator.Singleton,
                                element));

                        // If aggregates are pushed to the index, then we only get back defined results
                        results = results.Where((result) => result != Undefined);

                        CosmosElement aggregationResult;
                        switch (aggregate)
                        {
                            case Aggregate.Min:
                            case Aggregate.Max:
                                if (results.Count() == 0)
                                {
                                    aggregationResult = Undefined;
                                }
                                else if (results.Any(result => !Utils.IsPrimitive(result)))
                                {
                                    aggregationResult = Undefined;
                                }
                                else
                                {
                                    aggregationResult = results.First();
                                    foreach (CosmosElement result in results)
                                    {
                                        // First compare the types
                                        int comparison = result.CompareTo(aggregationResult);

                                        if (aggregate == Aggregate.Min)
                                        {
                                            if (comparison < 0)
                                            {
                                                aggregationResult = result;
                                            }
                                        }
                                        else if (aggregate == Aggregate.Max)
                                        {
                                            if (comparison > 0)
                                            {
                                                aggregationResult = result;
                                            }
                                        }
                                        else
                                        {
                                            throw new InvalidOperationException("Should not get here");
                                        }
                                    }
                                }

                                break;

                            case Aggregate.Avg:
                            case Aggregate.Sum:
                                CosmosElement sum = CosmosNumber64.Create(0);
                                double count = 0;
                                foreach (CosmosElement result in results)
                                {
                                    if ((result is CosmosNumber resultAsNumber) && (sum is CosmosNumber num))
                                    {
                                        sum = CosmosNumber64.Create(Number64.ToDouble(num.Value) + Number64.ToDouble(resultAsNumber.Value));
                                        count++;
                                    }
                                    else
                                    {
                                        sum = Undefined;
                                    }
                                }

                                if (sum is CosmosNumber number)
                                {
                                    if (aggregate == Aggregate.Avg)
                                    {
                                        aggregationResult = count == 0 ? Undefined : CosmosNumber64.Create(Number64.ToDouble(number.Value) / count);
                                    }
                                    else
                                    {
                                        aggregationResult = CosmosNumber64.Create(Number64.ToDouble(number.Value));
                                    }
                                }
                                else
                                {
                                    aggregationResult = Undefined;
                                }

                                break;

                            case Aggregate.Count:
                                aggregationResult = CosmosNumber64.Create(results.Count());
                                break;

                            default:
                                throw new ArgumentException($"Unknown {nameof(Aggregate)} {aggregate}");
                        }

                        rewrittenExpression = aggregationResult != Undefined
                            ? aggregationResult.Accept(CosmosElementToSqlScalarExpression.Singleton)
                            : SqlLiteralScalarExpression.Create(SqlUndefinedLiteral.Create());

                    }
                    else
                    {
                        // Just a regular function call
                        rewrittenExpression = SqlFunctionCallScalarExpression.Create(
                            sqlFunctionCallScalarExpression.Name,
                            sqlFunctionCallScalarExpression.IsUdf,
                            sqlFunctionCallScalarExpression.Arguments);
                    }

                    return rewrittenExpression;
                }

                public override SqlScalarExpression Visit(SqlInScalarExpression sqlInScalarExpression)
                {
                    SqlScalarExpression[] items = new SqlScalarExpression[sqlInScalarExpression.Haystack.Length];
                    for (int i = 0; i < sqlInScalarExpression.Haystack.Length; i++)
                    {
                        items[i] = sqlInScalarExpression.Haystack[i].Accept(this);
                    }

                    return SqlInScalarExpression.Create(
                        sqlInScalarExpression.Needle.Accept(this),
                        sqlInScalarExpression.Not,
                        items);
                }

                public override SqlScalarExpression Visit(SqlLastScalarExpression sqlLastScalarExpression)
                {
                    // No need to worry about aggregates within the subquery (they will recursively get rewritten).
                    return sqlLastScalarExpression;
                }

                public override SqlScalarExpression Visit(SqlLikeScalarExpression sqlLikeScalarExpression)
                {
                    return sqlLikeScalarExpression;
                }

                public override SqlScalarExpression Visit(SqlLiteralScalarExpression sqlLiteralScalarExpression)
                {
                    return sqlLiteralScalarExpression;
                }

                public override SqlScalarExpression Visit(SqlMemberIndexerScalarExpression sqlMemberIndexerScalarExpression)
                {
                    return SqlMemberIndexerScalarExpression.Create(
                        sqlMemberIndexerScalarExpression.Member.Accept(this),
                        sqlMemberIndexerScalarExpression.Indexer.Accept(this));
                }

                public override SqlScalarExpression Visit(SqlObjectCreateScalarExpression sqlObjectCreateScalarExpression)
                {
                    List<SqlObjectProperty> properties = new List<SqlObjectProperty>();
                    foreach (SqlObjectProperty property in sqlObjectCreateScalarExpression.Properties)
                    {
                        properties.Add(SqlObjectProperty.Create(property.Name, property.Value.Accept(this)));
                    }

                    return SqlObjectCreateScalarExpression.Create(properties.ToImmutableArray());
                }

                public override SqlScalarExpression Visit(SqlParameterRefScalarExpression scalarExpression)
                {
                    return scalarExpression;
                }

                public override SqlScalarExpression Visit(SqlPropertyRefScalarExpression sqlPropertyRefScalarExpression)
                {
                    return SqlPropertyRefScalarExpression.Create(
                        sqlPropertyRefScalarExpression.Member?.Accept(this),
                        sqlPropertyRefScalarExpression.Identifier);
                }

                public override SqlScalarExpression Visit(SqlSubqueryScalarExpression sqlSubqueryScalarExpression)
                {
                    // No need to worry about the aggregates within the subquery since they get recursively evaluated.
                    return sqlSubqueryScalarExpression;
                }

                public override SqlScalarExpression Visit(SqlUnaryScalarExpression sqlUnaryScalarExpression)
                {
                    return SqlUnaryScalarExpression.Create(
                        sqlUnaryScalarExpression.OperatorKind,
                        sqlUnaryScalarExpression.Expression.Accept(this));
                }
            }
        }

        private sealed class CosmosElementToSqlScalarExpression : ICosmosElementVisitor<SqlScalarExpression>
        {
            public static readonly CosmosElementToSqlScalarExpression Singleton = new CosmosElementToSqlScalarExpression();

            private CosmosElementToSqlScalarExpression()
            {
            }

            public SqlScalarExpression Visit(CosmosArray cosmosArray)
            {
                List<SqlScalarExpression> items = new List<SqlScalarExpression>();
                foreach (CosmosElement arrayItem in cosmosArray)
                {
                    items.Add(arrayItem.Accept(this));
                }

                return SqlArrayCreateScalarExpression.Create(items.ToImmutableArray());
            }

            public SqlScalarExpression Visit(CosmosBinary cosmosBinary)
            {
                throw new NotImplementedException();
            }

            public SqlScalarExpression Visit(CosmosBoolean cosmosBoolean)
            {
                SqlBooleanLiteral literal = SqlBooleanLiteral.Create(cosmosBoolean.Value);
                return SqlLiteralScalarExpression.Create(literal);
            }

            public SqlScalarExpression Visit(CosmosGuid cosmosGuid)
            {
                throw new NotImplementedException();
            }

            public SqlScalarExpression Visit(CosmosNull cosmosNull)
            {
                SqlNullLiteral literal = SqlNullLiteral.Singleton;
                return SqlLiteralScalarExpression.Create(literal);
            }

            public SqlScalarExpression Visit(CosmosNumber cosmosNumber)
            {
                SqlNumberLiteral literal = SqlNumberLiteral.Create(cosmosNumber.Value);
                return SqlLiteralScalarExpression.Create(literal);
            }

            public SqlScalarExpression Visit(CosmosObject cosmosObject)
            {
                List<SqlObjectProperty> properties = new List<SqlObjectProperty>();

                foreach (KeyValuePair<string, CosmosElement> kvp in cosmosObject)
                {
                    SqlPropertyName name = SqlPropertyName.Create(kvp.Key);
                    CosmosElement value = kvp.Value;
                    SqlScalarExpression expression = value.Accept(this);
                    SqlObjectProperty property = SqlObjectProperty.Create(name, expression);
                    properties.Add(property);
                }

                return SqlObjectCreateScalarExpression.Create(properties.ToImmutableArray());
            }

            public SqlScalarExpression Visit(CosmosString cosmosString)
            {
                SqlStringLiteral literal = SqlStringLiteral.Create(cosmosString.Value);
                return SqlLiteralScalarExpression.Create(literal);
            }

            public SqlScalarExpression Visit(CosmosUndefined cosmosUndefined)
            {
                return SqlLiteralScalarExpression.Create(SqlUndefinedLiteral.Create());
            }
        }
    }
}