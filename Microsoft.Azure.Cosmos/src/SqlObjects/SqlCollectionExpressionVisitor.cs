//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Globalization;

    internal abstract class SqlCollectionExpressionVisitor
    {
        protected void Visit(SqlCollectionExpression expression)
        {
            switch(expression.Kind)
            {
                case SqlObjectKind.AliasedCollectionExpression:
                    this.Visit(expression as SqlAliasedCollectionExpression);
                    return;
                case SqlObjectKind.ArrayIteratorCollectionExpression:
                    this.Visit(expression as SqlArrayIteratorCollectionExpression);
                    return;
                case SqlObjectKind.JoinCollectionExpression:
                    this.Visit(expression as SqlJoinCollectionExpression);
                    return;
                case SqlObjectKind.SubqueryCollectionExpression:
                    this.Visit(expression as SqlSubqueryCollectionExpression);
                    return;
                default:
                    throw new InvalidProgramException(
                        string.Format(CultureInfo.InvariantCulture, "Unexpected SqlObjectKind {0}", expression.Kind));
            }
        }

        protected abstract void Visit(SqlAliasedCollectionExpression expression);
        protected abstract void Visit(SqlArrayIteratorCollectionExpression expression);
        protected abstract void Visit(SqlJoinCollectionExpression expression);
        protected abstract void Visit(SqlSubqueryCollectionExpression expression);
    }

}
