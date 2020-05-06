//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;

    internal sealed class SqlInputPathCollection : SqlCollection
    {
        private SqlInputPathCollection(
            SqlIdentifier input,
            SqlPathExpression relativePath)
            : base(SqlObjectKind.InputPathCollection)
        {
            this.Input = input ?? throw new ArgumentNullException(nameof(input));
            this.RelativePath = relativePath;
        }

        public SqlIdentifier Input { get; }

        public SqlPathExpression RelativePath { get; }

        public static SqlInputPathCollection Create(
            SqlIdentifier input,
            SqlPathExpression relativePath) => new SqlInputPathCollection(input, relativePath);

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);

        public override void Accept(SqlCollectionVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlCollectionVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlCollectionVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
