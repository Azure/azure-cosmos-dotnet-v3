//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Collections.Generic;

    internal sealed class SqlPropertyName : SqlObject
    {
        private static readonly Dictionary<string, SqlPropertyName> SystemProperties = new Dictionary<string, SqlPropertyName>()
        {
            { "$s", new SqlPropertyName("$s") },
            { "$t", new SqlPropertyName("$t") },
            { "$v", new SqlPropertyName("$v") },
            { "_attachments", new SqlPropertyName("_attachments") },
            { "_etag", new SqlPropertyName("_etag") },
            { "_rid", new SqlPropertyName("_rid") },
            { "_self", new SqlPropertyName("_self") },
            { "_ts", new SqlPropertyName("_ts") },
            { "attachments/", new SqlPropertyName("attachments/") },
            { "coordinates", new SqlPropertyName("coordinates") },
            { "geometry", new SqlPropertyName("geometry") },
            { "GeometryCollection", new SqlPropertyName("GeometryCollection") },
            { "id", new SqlPropertyName("id") },
            { "inE", new SqlPropertyName("inE") },
            { "inV", new SqlPropertyName("inV") },
            { "label", new SqlPropertyName("label") },
            { "LineString", new SqlPropertyName("LineString") },
            { "link", new SqlPropertyName("link") },
            { "MultiLineString", new SqlPropertyName("MultiLineString") },
            { "MultiPoint", new SqlPropertyName("MultiPoint") },
            { "MultiPolygon", new SqlPropertyName("MultiPolygon") },
            { "name", new SqlPropertyName("name") },
            { "outE", new SqlPropertyName("outE") },
            { "outV", new SqlPropertyName("outV") },
            { "Point", new SqlPropertyName("Point") },
            { "Polygon", new SqlPropertyName("Polygon") },
            { "properties", new SqlPropertyName("properties") },
            { "type", new SqlPropertyName("type") },
            { "value", new SqlPropertyName("value") },

            // ExtendedJsonHelper
            { "$symbol", new SqlPropertyName("$symbol") },
            { "$binary", new SqlPropertyName("$binary") },
            { "$type", new SqlPropertyName("$type") },
            { "$ref", new SqlPropertyName("$ref") },
            { "$$id", new SqlPropertyName("$$id") },
            { "$db", new SqlPropertyName("$db") },
            { "$oid", new SqlPropertyName("$oid") },
            { "$date", new SqlPropertyName("$date") },
            { "$code", new SqlPropertyName("$code") },
            { "$options", new SqlPropertyName("$options") },
            { "$regex", new SqlPropertyName("$regex") },
            { "$scope", new SqlPropertyName("$scope") },

            // Random Geospatial
            { "crs", new SqlPropertyName("crs") },
        };

        private SqlPropertyName(string value)
            : base(SqlObjectKind.PropertyName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"{nameof(value)} must not be null, empty, or whitespace.");
            }

            this.Value = value;
        }

        public string Value
        {
            get;
        }

        public static SqlPropertyName Create(string value)
        {
            if (!SqlPropertyName.SystemProperties.TryGetValue(value, out SqlPropertyName sqlPropertyName))
            {
                sqlPropertyName = new SqlPropertyName(value);
            }

            return sqlPropertyName;
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
