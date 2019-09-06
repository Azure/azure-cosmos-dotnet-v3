//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;

    internal sealed class SqlObjectLiteral : SqlLiteral
    {
        public readonly bool isValueSerialized;

        public object Value
        {
            get;
            private set;
        }

        private SqlObjectLiteral(object value, bool isValueSerialized)
            : base(SqlObjectKind.ObjectLiteral)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            this.Value = value;
            this.isValueSerialized = isValueSerialized;
        }

        public static SqlObjectLiteral Create(object value, bool isValueSerialized)
        {
            return new SqlObjectLiteral(value, isValueSerialized);
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

        public override void Accept(SqlLiteralVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override TResult Accept<TResult>(SqlLiteralVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }
    }
}
