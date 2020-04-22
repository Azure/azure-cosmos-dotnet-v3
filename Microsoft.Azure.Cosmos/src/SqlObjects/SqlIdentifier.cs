//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System.Collections.Generic;

    internal sealed class SqlIdentifier : SqlObject
    {
        private static readonly Dictionary<string, SqlIdentifier> FrequentIdentifiers = new Dictionary<string, SqlIdentifier>()
        {
            { "root", new SqlIdentifier("root") },
            { "payload", new SqlIdentifier("payload") },
            { "distance", new SqlIdentifier("distance") },
            { "$s", new SqlIdentifier("$s") },
            { "$t", new SqlIdentifier("$t") },
            { "$v", new SqlIdentifier("$v") },
            { "_attachments", new SqlIdentifier("_attachments") },
            { "_etag", new SqlIdentifier("_etag") },
            { "_rid", new SqlIdentifier("_rid") },
            { "_self", new SqlIdentifier("_self") },
            { "_ts", new SqlIdentifier("_ts") },
            { "attachments/", new SqlIdentifier("attachments/") },
            { "coordinates", new SqlIdentifier("coordinates") },
            { "geometry", new SqlIdentifier("geometry") },
            { "GeometryCollection", new SqlIdentifier("GeometryCollection") },
            { "id", new SqlIdentifier("id") },
            { "inE", new SqlIdentifier("inE") },
            { "inV", new SqlIdentifier("inV") },
            { "label", new SqlIdentifier("label") },
            { "LineString", new SqlIdentifier("LineString") },
            { "link", new SqlIdentifier("link") },
            { "MultiLineString", new SqlIdentifier("MultiLineString") },
            { "MultiPoint", new SqlIdentifier("MultiPoint") },
            { "MultiPolygon", new SqlIdentifier("MultiPolygon") },
            { "name", new SqlIdentifier("name") },
            { "outE", new SqlIdentifier("outE") },
            { "outV", new SqlIdentifier("outV") },
            { "Point", new SqlIdentifier("Point") },
            { "Polygon", new SqlIdentifier("Polygon") },
            { "properties", new SqlIdentifier("properties") },
            { "type", new SqlIdentifier("type") },
            { "value", new SqlIdentifier("value") },
            { "k", new SqlIdentifier("k") },
            { "elem0", new SqlIdentifier("elem0") },
            { "elem1", new SqlIdentifier("elem1") },
            { "elem2", new SqlIdentifier("elem2") },
            { "elem3", new SqlIdentifier("elem3") },
            { "elem4", new SqlIdentifier("elem4") },
            { "elem5", new SqlIdentifier("elem5") },
            { "elem6", new SqlIdentifier("elem6") },
            { "elem7", new SqlIdentifier("elem7") },
            { "elem8", new SqlIdentifier("elem8") },
            { "elem9", new SqlIdentifier("elem9") },
            { "elem10", new SqlIdentifier("elem10") },
            { "elem11", new SqlIdentifier("elem11") },
            { "elem12", new SqlIdentifier("elem12") },
            { "elem13", new SqlIdentifier("elem13") },
            { "elem14", new SqlIdentifier("elem14") },
            { "elem15", new SqlIdentifier("elem15") },
            { "elem16", new SqlIdentifier("elem16") },
            { "elem17", new SqlIdentifier("elem17") },
            { "elem18", new SqlIdentifier("elem18") },
            { "elem19", new SqlIdentifier("elem19") },
            { "elem20", new SqlIdentifier("elem20") },
            { "elem21", new SqlIdentifier("elem21") },
            { "elem22", new SqlIdentifier("elem22") },
            { "elem23", new SqlIdentifier("elem23") },
            { "elem24", new SqlIdentifier("elem24") },
            { "elem25", new SqlIdentifier("elem25") },
            { "elem26", new SqlIdentifier("elem26") },
            { "elem27", new SqlIdentifier("elem27") },
            { "elem28", new SqlIdentifier("elem28") },
            { "elem29", new SqlIdentifier("elem29") },
            { "elem30", new SqlIdentifier("elem30") },
            { "elem31", new SqlIdentifier("elem31") },
            { "elem32", new SqlIdentifier("elem32") },
        };

        private SqlIdentifier(string value)
            : base(SqlObjectKind.Identifier)
        {
            this.Value = value;
        }

        public string Value
        {
            get;
        }

        public static SqlIdentifier Create(string value)
        {
            if (!SqlIdentifier.FrequentIdentifiers.TryGetValue(value, out SqlIdentifier sqlIdentifier))
            {
                sqlIdentifier = new SqlIdentifier(value);
            }

            return sqlIdentifier;
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
