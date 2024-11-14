//-----------------------------------------------------------------------
// <copyright file="AggregateProjectionDector.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests.Query.OfflineEngine
{
    using System;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Microsoft.Azure.Cosmos.SqlObjects.Visitors;

    /// <summary>
    /// Visits a SQL DOM to determine if it has an aggregate.
    /// </summary>
    internal sealed class AggregateProjectionDector
    {
        /// <summary>
        /// Determines whether or not the SqlSelectSpec has an aggregate in the outer most query.
        /// </summary>
        /// <param name="selectSpec">The select spec to traverse.</param>
        /// <returns>Whether or not the SqlSelectSpec has an aggregate in the outer most query.</returns>
        public static bool HasAggregate(SqlSelectSpec selectSpec)
        {
            return selectSpec.Accept(AggregateProjectionDectorVisitor.Singleton);
        }

        private sealed class AggregateProjectionDectorVisitor : SqlSelectSpecVisitor<bool>
        {
            public static readonly AggregateProjectionDectorVisitor Singleton = new AggregateProjectionDectorVisitor();

            public override bool Visit(SqlSelectListSpec selectSpec)
            {
                bool hasAggregates = false;
                foreach (SqlSelectItem selectItem in selectSpec.Items)
                {
                    hasAggregates |= selectItem.Expression.Accept(AggregateScalarExpressionDetector.Singleton);
                }

                return hasAggregates;
            }

            public override bool Visit(SqlSelectValueSpec selectSpec)
            {
                return selectSpec.Expression.Accept(AggregateScalarExpressionDetector.Singleton);
            }

            public override bool Visit(SqlSelectStarSpec selectSpec)
            {
                return false;
            }

            /// <summary>
            /// Determines if there is an aggregate in a scalar expression.
            /// </summary>
            private sealed class AggregateScalarExpressionDetector : SqlScalarExpressionVisitor<bool>
            {
                public static readonly AggregateScalarExpressionDetector Singleton = new AggregateScalarExpressionDetector();

                public override bool Visit(SqlAllScalarExpression sqlAllScalarExpression)
                {
                    // No need to worry about aggregates within the subquery (they will recursively get rewritten).
                    return false;
                }

                public override bool Visit(SqlArrayCreateScalarExpression sqlArrayCreateScalarExpression)
                {
                    bool hasAggregates = false;
                    foreach (SqlScalarExpression item in sqlArrayCreateScalarExpression.Items)
                    {
                        hasAggregates |= item.Accept(this);
                    }

                    return hasAggregates;
                }

                public override bool Visit(SqlArrayScalarExpression sqlArrayScalarExpression)
                {
                    // No need to worry about aggregates in the subquery (they will recursively get rewritten).
                    return false;
                }

                public override bool Visit(SqlBetweenScalarExpression sqlBetweenScalarExpression)
                {
                    return sqlBetweenScalarExpression.Expression.Accept(this) ||
                        sqlBetweenScalarExpression.StartInclusive.Accept(this) ||
                        sqlBetweenScalarExpression.EndInclusive.Accept(this);
                }

                public override bool Visit(SqlBinaryScalarExpression sqlBinaryScalarExpression)
                {
                    return sqlBinaryScalarExpression.LeftExpression.Accept(this) ||
                        sqlBinaryScalarExpression.RightExpression.Accept(this);
                }

                public override bool Visit(SqlCoalesceScalarExpression sqlCoalesceScalarExpression)
                {
                    return sqlCoalesceScalarExpression.Left.Accept(this) ||
                        sqlCoalesceScalarExpression.Right.Accept(this);
                }

                public override bool Visit(SqlConditionalScalarExpression sqlConditionalScalarExpression)
                {
                    return sqlConditionalScalarExpression.Condition.Accept(this) ||
                        sqlConditionalScalarExpression.Consequent.Accept(this) ||
                        sqlConditionalScalarExpression.Alternative.Accept(this);
                }

                public override bool Visit(SqlExistsScalarExpression sqlExistsScalarExpression)
                {
                    // No need to worry about aggregates within the subquery (they will recursively get rewritten).
                    return false;
                }

                public override bool Visit(SqlFirstScalarExpression sqlFirstScalarExpression)
                {
                    // No need to worry about aggregates within the subquery (they will recursively get rewritten).
                    return false;
                }

                public override bool Visit(SqlFunctionCallScalarExpression sqlFunctionCallScalarExpression)
                {
                    return !sqlFunctionCallScalarExpression.IsUdf &&
                        Enum.TryParse(value: sqlFunctionCallScalarExpression.Name.Value, ignoreCase: true, result: out Aggregate _);
                }

                public override bool Visit(SqlInScalarExpression sqlInScalarExpression)
                {
                    bool hasAggregates = false;
                    for (int i = 0; i < sqlInScalarExpression.Haystack.Length; i++)
                    {
                        hasAggregates |= sqlInScalarExpression.Haystack[i].Accept(this);
                    }

                    return hasAggregates;
                }

                public override bool Visit(SqlLastScalarExpression sqlLastScalarExpression)
                {
                    // No need to worry about aggregates within the subquery (they will recursively get rewritten).
                    return false;
                }

                public override bool Visit(SqlLikeScalarExpression sqlLikeScalarExpression)
                {
                    return false;
                }

                public override bool Visit(SqlLiteralScalarExpression sqlLiteralScalarExpression)
                {
                    return false;
                }

                public override bool Visit(SqlMemberIndexerScalarExpression sqlMemberIndexerScalarExpression)
                {
                    return sqlMemberIndexerScalarExpression.Member.Accept(this) ||
                        sqlMemberIndexerScalarExpression.Indexer.Accept(this);
                }

                public override bool Visit(SqlObjectCreateScalarExpression sqlObjectCreateScalarExpression)
                {
                    bool hasAggregates = false;
                    foreach (SqlObjectProperty property in sqlObjectCreateScalarExpression.Properties)
                    {
                        hasAggregates |= property.Value.Accept(this);
                    }

                    return hasAggregates;
                }

                public override bool Visit(SqlParameterRefScalarExpression scalarExpression)
                {
                    return false;
                }

                public override bool Visit(SqlPropertyRefScalarExpression sqlPropertyRefScalarExpression)
                {
                    bool hasAggregates = false;
                    if (sqlPropertyRefScalarExpression.Member != null)
                    {
                        hasAggregates = sqlPropertyRefScalarExpression.Member.Accept(this);
                    }

                    return hasAggregates;
                }

                public override bool Visit(SqlSubqueryScalarExpression sqlSubqueryScalarExpression)
                {
                    // No need to worry about the aggregates within the subquery since they get recursively evaluated.
                    return false;
                }

                public override bool Visit(SqlUnaryScalarExpression sqlUnaryScalarExpression)
                {
                    return sqlUnaryScalarExpression.Expression.Accept(this);
                }
            }
        }
    }
}