//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;

    internal sealed class SqlPropertyRefScalarExpression : SqlScalarExpression
    {
        private SqlPropertyRefScalarExpression(
            SqlScalarExpression memberExpression,
            SqlIdentifier propertyIdentifier)
            : base(SqlObjectKind.PropertyRefScalarExpression)
        {
            this.MemberExpression = memberExpression;
            this.PropertyIdentifier = propertyIdentifier ?? throw new ArgumentNullException("propertyIdentifier");
        }

        public SqlIdentifier PropertyIdentifier
        {
            get;
        }

        public SqlScalarExpression MemberExpression
        {
            get;
        }

        public static SqlPropertyRefScalarExpression Create(
            SqlScalarExpression memberExpression,
            SqlIdentifier propertyIdentifier)
        {
            return new SqlPropertyRefScalarExpression(memberExpression, propertyIdentifier);
        }

        public override void Accept(SqlObjectVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }

        public override void Accept(SqlScalarExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override TResult Accept<TResult>(SqlScalarExpressionVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input)
        {
            return visitor.Visit(this, input);
        }

        public override TResult Accept<T, TResult>(SqlScalarExpressionVisitor<T, TResult> visitor, T input)
        {
            return visitor.Visit(this, input);
        }
    }
}
