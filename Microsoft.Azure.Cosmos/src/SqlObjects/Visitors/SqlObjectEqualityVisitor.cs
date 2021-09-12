// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SqlObjects.Visitors
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.SqlObjects;

    internal sealed class SqlObjectEqualityVisitor : SqlObjectVisitor<SqlObject, bool>
    {
        public static readonly SqlObjectEqualityVisitor Singleton = new SqlObjectEqualityVisitor();

        private SqlObjectEqualityVisitor()
        {
        }

        public override bool Visit(SqlAliasedCollectionExpression first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlAliasedCollectionExpression second))
            {
                return false;
            }

            if (!SqlObjectEqualityVisitor.Equals(first.Alias, second.Alias))
            {
                return false;
            }

            if (!first.Collection.Accept(this, second.Collection))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlArrayCreateScalarExpression first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlArrayCreateScalarExpression second))
            {
                return false;
            }

            if (!SequenceEquals(first.Items, second.Items))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlArrayIteratorCollectionExpression first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlArrayIteratorCollectionExpression second))
            {
                return false;
            }

            if (!Equals(first.Identifier, second.Identifier))
            {
                return false;
            }

            if (!Equals(first.Collection, second.Collection))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlArrayScalarExpression first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlArrayScalarExpression second))
            {
                return false;
            }

            if (!Equals(first.SqlQuery, second.SqlQuery))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlBetweenScalarExpression first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlBetweenScalarExpression second))
            {
                return false;
            }

            if (!Equals(first.Expression, second.Expression))
            {
                return false;
            }

            if (first.Not != second.Not)
            {
                return false;
            }

            if (!Equals(first.StartInclusive, second.StartInclusive))
            {
                return false;
            }

            if (!Equals(first.EndInclusive, second.EndInclusive))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlBinaryScalarExpression first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlBinaryScalarExpression second))
            {
                return false;
            }

            if (first.OperatorKind != second.OperatorKind)
            {
                return false;
            }

            if (!Equals(first.LeftExpression, second.LeftExpression))
            {
                return false;
            }

            if (!Equals(first.RightExpression, second.RightExpression))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlBooleanLiteral first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlBooleanLiteral second))
            {
                return false;
            }

            if (first.Value != second.Value)
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlCoalesceScalarExpression first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlCoalesceScalarExpression second))
            {
                return false;
            }

            if (!Equals(first.Left, second.Left))
            {
                return false;
            }

            if (!Equals(first.Right, second.Right))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlConditionalScalarExpression first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlConditionalScalarExpression second))
            {
                return false;
            }

            if (!Equals(first.Condition, second.Condition))
            {
                return false;
            }

            if (!Equals(first.Consequent, second.Consequent))
            {
                return false;
            }

            if (!Equals(first.Alternative, second.Alternative))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlExistsScalarExpression first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlExistsScalarExpression second))
            {
                return false;
            }

            if (!Equals(first.Subquery, second.Subquery))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlFromClause first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlFromClause second))
            {
                return false;
            }

            if (!Equals(first.Expression, second.Expression))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlFunctionCallScalarExpression first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlFunctionCallScalarExpression second))
            {
                return false;
            }

            if (first.IsUdf != second.IsUdf)
            {
                return false;
            }

            if (!Equals(first.Name, second.Name))
            {
                return false;
            }

            if (!SequenceEquals(first.Arguments, second.Arguments))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlGroupByClause first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlGroupByClause second))
            {
                return false;
            }

            if (!SequenceEquals(first.Expressions, second.Expressions))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlIdentifier first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlIdentifier second))
            {
                return false;
            }

            if (first.Value != second.Value)
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlIdentifierPathExpression first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlIdentifierPathExpression second))
            {
                return false;
            }

            if (!first.Value.Accept(this, second.Value))
            {
                return false;
            }

            if (!SqlObjectEqualityVisitor.Equals(first.ParentPath, second.ParentPath))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlInputPathCollection first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlInputPathCollection second))
            {
                return false;
            }

            if (!first.Input.Accept(this, second.Input))
            {
                return false;
            }

            if (!SqlObjectEqualityVisitor.Equals(first.RelativePath, second.RelativePath))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlInScalarExpression first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlInScalarExpression second))
            {
                return false;
            }

            if (!Equals(first.Needle, second.Needle))
            {
                return false;
            }

            if (first.Not != second.Not)
            {
                return false;
            }

            if (!SequenceEquals(first.Haystack, second.Haystack))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlJoinCollectionExpression first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlJoinCollectionExpression second))
            {
                return false;
            }

            if (!Equals(first.Left, second.Left))
            {
                return false;
            }

            if (!Equals(first.Right, second.Right))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlLikeScalarExpression first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlLikeScalarExpression second))
            {
                return false;
            }

            if (!Equals(first.Expression, second.Expression))
            {
                return false;
            }

            if (!Equals(first.Pattern, second.Pattern))
            {
                return false;
            }

            if (!Equals(first.Not, second.Not))
            {
                return false;
            }

            if (!Equals(first.EscapeSequence, second.EscapeSequence))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlLimitSpec first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlLimitSpec second))
            {
                return false;
            }

            return Equals(first.LimitExpression, second.LimitExpression);
        }

        public override bool Visit(SqlLiteralScalarExpression first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlLiteralScalarExpression second))
            {
                return false;
            }

            return Equals(first.Literal, second.Literal);
        }

        public override bool Visit(SqlMemberIndexerScalarExpression first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlMemberIndexerScalarExpression second))
            {
                return false;
            }

            if (!Equals(first.Member, second.Member))
            {
                return false;
            }

            if (!Equals(first.Indexer, second.Indexer))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlNullLiteral first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlNullLiteral second))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlNumberLiteral first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlNumberLiteral second))
            {
                return false;
            }

            return first.Value.Equals(second.Value);
        }

        public override bool Visit(SqlNumberPathExpression first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlNumberPathExpression second))
            {
                return false;
            }

            if (!Equals(first.Value, second.Value))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlObjectCreateScalarExpression first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlObjectCreateScalarExpression second))
            {
                return false;
            }

            if (first.Properties.Count() != second.Properties.Count())
            {
                return false;
            }

            // order of properties does not matter
            foreach (SqlObjectProperty property1 in first.Properties)
            {
                bool found = false;
                foreach (SqlObjectProperty property2 in second.Properties)
                {
                    found |= Equals(property1, property2);
                }

                if (!found)
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Visit(SqlObjectProperty first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlObjectProperty second))
            {
                return false;
            }

            if (!Equals(first.Name, second.Name))
            {
                return false;
            }

            if (!Equals(first.Value, second.Value))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlOffsetLimitClause first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlOffsetLimitClause second))
            {
                return false;
            }

            if (!Equals(first.LimitSpec, second.LimitSpec))
            {
                return false;
            }

            if (!Equals(first.OffsetSpec, second.OffsetSpec))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlOffsetSpec first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlOffsetSpec second))
            {
                return false;
            }

            if (!Equals(first.OffsetExpression, second.OffsetExpression))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlOrderByClause first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlOrderByClause second))
            {
                return false;
            }

            if (!SequenceEquals(first.OrderByItems, second.OrderByItems))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlOrderByItem first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlOrderByItem second))
            {
                return false;
            }

            if (first.IsDescending != second.IsDescending)
            {
                return false;
            }

            if (!Equals(first.Expression, second.Expression))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlParameter first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlParameter second))
            {
                return false;
            }

            if (first.Name != second.Name)
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlParameterRefScalarExpression first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlParameterRefScalarExpression second))
            {
                return false;
            }

            if (!Equals(first.Parameter, second.Parameter))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlProgram first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlProgram second))
            {
                return false;
            }

            if (!Equals(first.Query, second.Query))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlPropertyName first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlPropertyName second))
            {
                return false;
            }

            if (first.Value != second.Value)
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlPropertyRefScalarExpression first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlPropertyRefScalarExpression second))
            {
                return false;
            }

            if (!Equals(first.Identifier, second.Identifier))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlQuery first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlQuery second))
            {
                return false;
            }

            if (!first.SelectClause.Accept(this, second.SelectClause))
            {
                return false;
            }

            if (!Equals(first.FromClause, second.FromClause))
            {
                return false;
            }

            if (!Equals(first.WhereClause, second.WhereClause))
            {
                return false;
            }

            if (!Equals(first.GroupByClause, second.GroupByClause))
            {
                return false;
            }

            if (!Equals(first.OrderByClause, second.OrderByClause))
            {
                return false;
            }

            if (!Equals(first.OffsetLimitClause, second.OffsetLimitClause))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlSelectClause first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlSelectClause second))
            {
                return false;
            }

            if (first.HasDistinct != second.HasDistinct)
            {
                return false;
            }

            if (!Equals(first.SelectSpec, second.SelectSpec))
            {
                return false;
            }

            if (!Equals(first.TopSpec, second.TopSpec))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlSelectItem first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlSelectItem second))
            {
                return false;
            }

            if (!Equals(first.Alias, second.Alias))
            {
                return false;
            }

            if (!Equals(first.Expression, second.Expression))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlSelectListSpec first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlSelectListSpec second))
            {
                return false;
            }

            if (!SequenceEquals(first.Items, second.Items))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlSelectStarSpec first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlSelectStarSpec second))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlSelectValueSpec first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlSelectValueSpec second))
            {
                return false;
            }

            if (!Equals(first.Expression, second.Expression))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlStringLiteral first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlStringLiteral second))
            {
                return false;
            }

            if (first.Value != second.Value)
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlStringPathExpression first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlStringPathExpression second))
            {
                return false;
            }

            if (!Equals(first.Value, second.Value))
            {
                return false;
            }

            if (!Equals(first.ParentPath, second.ParentPath))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlSubqueryCollection first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlSubqueryCollection second))
            {
                return false;
            }

            if (!Equals(first.Query, second.Query))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlSubqueryScalarExpression first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlSubqueryScalarExpression second))
            {
                return false;
            }

            if (!Equals(first.Query, second.Query))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlTopSpec first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlTopSpec second))
            {
                return false;
            }

            if (!Equals(first.TopExpresion, second.TopExpresion))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlUnaryScalarExpression first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlUnaryScalarExpression second))
            {
                return false;
            }

            if (!Equals(first.Expression, second.Expression))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlUndefinedLiteral first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlUndefinedLiteral second))
            {
                return false;
            }

            return true;
        }

        public override bool Visit(SqlWhereClause first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlWhereClause second))
            {
                return false;
            }

            if (!Equals(first.FilterExpression, second.FilterExpression))
            {
                return false;
            }

            return true;
        }

        private static bool SequenceEquals(
            IReadOnlyList<SqlObject> firstList,
            IReadOnlyList<SqlObject> secondList)
        {
            if (firstList.Count != secondList.Count)
            {
                return false;
            }

            IEnumerable<(SqlObject, SqlObject)> itemPairs = firstList
                .Zip(secondList, (first, second) => (first, second));

            foreach ((SqlObject firstItem, SqlObject secondItem) in itemPairs)
            {
                if (!firstItem.Accept(SqlObjectEqualityVisitor.Singleton, secondItem))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool BothNull(SqlObject first, SqlObject second)
        {
            return (first is null) && (second is null);
        }

        private static bool DifferentNullality(SqlObject first, SqlObject second)
        {
            return (first is null && !(second is null)) || (!(first is null) && (second is null));
        }

        private static bool Equals(SqlObject first, SqlObject second)
        {
            if (BothNull(first, second))
            {
                return true;
            }
            else if (DifferentNullality(first, second))
            {
                return false;
            }
            else
            {
                // Both not null
                return first.Accept(SqlObjectEqualityVisitor.Singleton, second);
            }
        }
    }
}
