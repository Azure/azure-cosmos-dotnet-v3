//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
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

        public SqlGeoNearCallScalarExpression(
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

        public override void AppendToBuilder(System.Text.StringBuilder builder)
        {
            builder.Append("(")
                   .Append("_ST_DISTANCE")
                   .Append("(")
                   .Append(this.PropertyRef)
                   .Append(",")
                   .Append(this.Geometry)
                   .Append(")")
                   .Append(" BETWEEN ");

            if (this.NumberOfPoints == null)
            {
                builder.Append(this.MinimumDistance)
                       .Append(" AND ")
                       .Append(this.MaximumDistance);
            }
            else
            {
                builder.Append(SqlGeoNearCallScalarExpression.NearMinimumDistanceName)
                       .Append(" AND ")
                       .Append(SqlGeoNearCallScalarExpression.NearMaximumDistanceName);
            }
            builder.Append(")");
        }
    }
}
