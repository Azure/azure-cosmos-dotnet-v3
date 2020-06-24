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
    sealed class SqlSelectClause : SqlObject
    {
        public static readonly SqlSelectClause SelectStar = new SqlSelectClause(SqlSelectStarSpec.Singleton);

        private SqlSelectClause(
            SqlSelectSpec selectSpec,
            SqlTopSpec topSpec = null,
            bool hasDistinct = false)
        {
            this.SelectSpec = selectSpec ?? throw new ArgumentNullException(nameof(selectSpec));
            this.TopSpec = topSpec;
            this.HasDistinct = hasDistinct;
        }

        public SqlSelectSpec SelectSpec { get; }

        public SqlTopSpec TopSpec { get; }

        public bool HasDistinct { get; }

        public static SqlSelectClause Create(
            SqlSelectSpec selectSpec,
            SqlTopSpec topSpec = null,
            bool hasDistinct = false) => new SqlSelectClause(selectSpec, topSpec, hasDistinct);

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
