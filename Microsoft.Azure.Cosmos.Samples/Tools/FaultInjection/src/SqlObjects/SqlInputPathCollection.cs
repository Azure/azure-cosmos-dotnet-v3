//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SqlObjects
{
    using System;
    using Microsoft.Azure.Cosmos.SqlObjects.Visitors;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    sealed class SqlInputPathCollection : SqlCollection
    {
        private SqlInputPathCollection(
            SqlIdentifier input,
            SqlPathExpression relativePath)
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
