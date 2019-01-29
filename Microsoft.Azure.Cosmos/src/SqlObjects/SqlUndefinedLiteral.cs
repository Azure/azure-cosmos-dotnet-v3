//-----------------------------------------------------------------------------------------------------------------------------------------
// <copyright file="SqlUndefinedLiteral.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System.Text;

    internal sealed class SqlUndefinedLiteral : SqlLiteral
    {
        public static readonly SqlUndefinedLiteral Singleton = new SqlUndefinedLiteral();

        private SqlUndefinedLiteral()
            : base(SqlObjectKind.UndefinedLiteral)
        {
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
