//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    internal sealed class SqlNullLiteral : SqlLiteral
    {
        public static readonly SqlNullLiteral Singleton = new SqlNullLiteral();

        private SqlNullLiteral()
        {
        }

        public static SqlNullLiteral Create() => SqlNullLiteral.Singleton;

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);

        public override void Accept(SqlLiteralVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlLiteralVisitor<TResult> visitor) => visitor.Visit(this);
    }
}
