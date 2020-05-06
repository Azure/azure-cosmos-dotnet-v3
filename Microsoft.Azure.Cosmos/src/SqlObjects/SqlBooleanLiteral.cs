//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    internal sealed class SqlBooleanLiteral : SqlLiteral
    {
        public static readonly SqlBooleanLiteral True = new SqlBooleanLiteral(true);
        public static readonly SqlBooleanLiteral False = new SqlBooleanLiteral(false);

        private SqlBooleanLiteral(bool value)
            : base(SqlObjectKind.BooleanLiteral)
        {
            this.Value = value;
        }

        public bool Value { get; }

        public static SqlBooleanLiteral Create(bool value) => value? True : False;

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);

        public override void Accept(SqlLiteralVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlLiteralVisitor<TResult> visitor) => visitor.Visit(this);
    }
}
