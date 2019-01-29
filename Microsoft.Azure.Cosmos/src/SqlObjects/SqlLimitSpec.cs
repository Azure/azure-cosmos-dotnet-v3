//-----------------------------------------------------------------------------------------------------------------------------------------
// <copyright file="SqlLimitSpec.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System.Linq;

    internal sealed class SqlLimitSpec : SqlObject
    {
        private const int PremadeLimitIndex = 256;
        private static readonly SqlLimitSpec[] PremadeLimitSpecs = Enumerable.Range(0, PremadeLimitIndex).Select(limit => new SqlLimitSpec(limit)).ToArray();

        private SqlLimitSpec(long limit)
            : base(SqlObjectKind.LimitSpec)
        {
            this.Limit = limit;
        }

        public long Limit
        {
            get;
        }

        public static SqlLimitSpec Create(long value)
        {
            if (value < PremadeLimitIndex && value >= 0)
            {
                return SqlLimitSpec.PremadeLimitSpecs[value];
            }

            return new SqlLimitSpec(value);
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
    }
}
