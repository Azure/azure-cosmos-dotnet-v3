//-----------------------------------------------------------------------------------------------------------------------------------------
// <copyright file="SqlGeoNearCallScalarExpression.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;

    internal class SqlGeoNearCallScalarExpression : SqlScalarExpression
    {
        public const string NearMinimumDistanceName = "@nearMinimumDistance";
        public const string NearMaximumDistanceName = "@nearMaximumDistance";

        public SqlScalarExpression PropertyRef
        {
            get;
            private set;
        }

        public SqlScalarExpression Geometry
        {
            get;
            set;
        }

        public uint? NumberOfPoints
        {
            get;
            set;
        }

        public double MinimumDistance
        {
            get;
            set;
        }

        public double MaximumDistance
        {
            get;
            set;
        }

        private SqlGeoNearCallScalarExpression(
            SqlScalarExpression propertyRef,
            SqlScalarExpression geometry,
            uint? num = null,
            double minDistance = 0,
            double maxDistance = 1e4)
            : base(SqlObjectKind.GeoNearCallScalarExpression)
        {
            this.PropertyRef = propertyRef;
            this.Geometry = geometry;
            this.NumberOfPoints = num;
            this.MinimumDistance = Math.Max(0, minDistance);
            this.MaximumDistance = Math.Max(0, maxDistance);
        }

        public static SqlGeoNearCallScalarExpression Create(
            SqlScalarExpression propertyRef,
            SqlScalarExpression geometry,
            uint? num = null,
            double minDistance = 0,
            double maxDistance = 1e4)
        {
            return new SqlGeoNearCallScalarExpression(propertyRef, geometry, num, minDistance, maxDistance);
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

        public override void Accept(SqlScalarExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override TResult Accept<TResult>(SqlScalarExpressionVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }

        public override TResult Accept<T, TResult>(SqlScalarExpressionVisitor<T, TResult> visitor, T input)
        {
            return visitor.Visit(this, input);
        }
    }
}
