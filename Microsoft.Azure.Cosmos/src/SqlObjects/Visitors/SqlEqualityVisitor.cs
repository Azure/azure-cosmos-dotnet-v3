// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SqlObjects.Visitors
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.ServiceProcess;
    using Microsoft.Azure.Cosmos.Sql;

    internal sealed class SqlEqualityVisitor : SqlObjectVisitor<SqlObject, bool>
    {
        public static readonly SqlEqualityVisitor Singleton = new SqlEqualityVisitor();

        private SqlEqualityVisitor()
        {
        }

        public override bool Visit(SqlAliasedCollectionExpression first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlAliasedCollectionExpression second))
            {
                return false;
            }

            if (!first.Alias.Accept(this, second.Alias))
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

            IEnumerable<(SqlScalarExpression, SqlScalarExpression)> itemPairs = first.Items.Zip(second.Items, (first, second) => (first, second));
            foreach ((SqlScalarExpression firstItem, SqlScalarExpression secondItem) in itemPairs)
            {
                if (firstItem.Accept(this, secondItem))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Visit(SqlArrayIteratorCollectionExpression first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlArrayIteratorCollectionExpression second))
            {
                return false;
            }

            if (!Equals(first.Alias, second.Alias))
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

            if (first.IsNot != second.IsNot)
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

        public override bool Visit(SqlConditionalScalarExpression first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlConditionalScalarExpression second))
            {
                return false;
            }

            if (!Equals(first.ConditionExpression, second.ConditionExpression))
            {
                return false;
            }

            if (!Equals(first.FirstExpression, second.FirstExpression))
            {
                return false;
            }

            if (!Equals(first.SecondExpression, second.SecondExpression))
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

            if (!Equals(first.SqlQuery, second.SqlQuery))
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

            IEnumerable<(SqlScalarExpression, SqlScalarExpression)> argumentPairs = first
                .Arguments
                .Zip(
                    second.Arguments,
                    (first, second) => (first, second));
            foreach ((SqlScalarExpression firstItem, SqlScalarExpression secondItem) in argumentPairs)
            {
                if (!Equals(firstItem, secondItem))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Visit(SqlGroupByClause first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlGroupByClause second))
            {
                return false;
            }

            IEnumerable<(SqlScalarExpression, SqlScalarExpression)> argumentPairs = first
                .Expressions
                .Zip(
                    second.Expressions,
                    (first, second) => (first, second));
            foreach ((SqlScalarExpression firstItem, SqlScalarExpression secondItem) in argumentPairs)
            {
                if (!Equals(firstItem, secondItem))
                {
                    return false;
                }
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

            if (!first.ParentPath.Accept(this, second.ParentPath))
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

            if (!first.RelativePath.Accept(this, second.RelativePath))
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

            if (!Equals(first.Expression, second.Expression))
            {
                return false;
            }

            if (first.Not != second.Not)
            {
                return false;
            }

            IEnumerable<(SqlScalarExpression, SqlScalarExpression)> itemPairs = first
                .Items
                .Zip(
                    second.Items,
                    (first, second) => (first, second));
            foreach ((SqlScalarExpression firstItem, SqlScalarExpression secondItem) in itemPairs)
            {
                if (!Equals(firstItem, secondItem))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Visit(SqlJoinCollectionExpression first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlJoinCollectionExpression second))
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

        public override bool Visit(SqlLimitSpec first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlLimitSpec second))
            {
                return false;
            }

            return Equals(first.LimitExpression, second.LimitExpression);
        }

        public override bool Visit(SqlLiteralArrayCollection first, SqlObject secondAsObject)
        {
            throw new System.NotImplementedException();
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

            if (!Equals(first.MemberExpression, second.MemberExpression))
            {
                return false;
            }

            if (!Equals(first.IndexExpression, second.IndexExpression))
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

            if (!Equals(first.Expression, second.Expression))
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

        public override bool Visit(SqlOrderbyClause first, SqlObject secondAsObject)
        {
            if (!(secondAsObject is SqlOrderbyClause second))
            {
                return false;
            }

            IEnumerable<(SqlOrderByItem, SqlOrderByItem)> itemPairs = first
                .OrderbyItems
                .Zip(
                    second.OrderbyItems,
                    (first, second) => (first, second));
            foreach ((SqlOrderByItem firstItem, SqlOrderByItem secondItem) in itemPairs)
            {
                if (!Equals(firstItem, secondItem))
                {
                    return false;
                }
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

            if (!Equals(first.PropertyIdentifier, second.PropertyIdentifier))
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

            if (!Equals(first.OrderbyClause, second.OrderbyClause))
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

            IEnumerable<(SqlSelectItem, SqlSelectItem)> itemPairs = first
                .Items
                .Zip(
                    second.Items,
                    (first, second) => (first, second));
            foreach ((SqlSelectItem firstItem, SqlSelectItem secondItem) in itemPairs)
            {
                if (!Equals(firstItem, secondItem))
                {
                    return false;
                }
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

        private static bool BothNull(SqlObject first, SqlObject second)
        {
            return (first == null) && (second == null);
        }

        private static bool DifferentNullality(SqlObject first, SqlObject second)
        {
            return ((first == null) && (second != null)) || ((first != null) && (second == null));
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
                return first.Accept(SqlEqualityVisitor.Singleton, second);
            }
        }
    }
}
