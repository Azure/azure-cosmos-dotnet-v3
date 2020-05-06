//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    internal sealed class SqlUndefinedLiteral : SqlLiteral
    {
        private static readonly SqlUndefinedLiteral Singleton = new SqlUndefinedLiteral();

        private SqlUndefinedLiteral()
            : base(SqlObjectKind.UndefinedLiteral)
        {
        }

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);

        public override void Accept(SqlLiteralVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlLiteralVisitor<TResult> visitor) => visitor.Visit(this);

        public static SqlUndefinedLiteral Create() => SqlUndefinedLiteral.Singleton;
    }
}
