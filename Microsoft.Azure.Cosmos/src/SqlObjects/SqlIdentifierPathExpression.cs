//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;

    internal sealed class SqlIdentifierPathExpression : SqlPathExpression
    {
        private SqlIdentifierPathExpression(SqlPathExpression parentPath, SqlIdentifier value)
            : base(SqlObjectKind.IdentifierPathExpression, parentPath)
        {
            this.Value = value ?? throw new ArgumentNullException("value");
        }

        public SqlIdentifier Value
        {
            get;
        }

        public static SqlIdentifierPathExpression Create(SqlPathExpression parentPath, SqlIdentifier value)
        {
            return new SqlIdentifierPathExpression(parentPath, value);
        }

        public override void Accept(SqlObjectVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input)
        {
            return visitor.Visit(this, input);
        }

        public override void Accept(SqlPathExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override TResult Accept<TResult>(SqlPathExpressionVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }
    }
}
