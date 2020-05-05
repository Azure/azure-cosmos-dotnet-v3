//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Runtime.CompilerServices;
    using Microsoft.Azure.Cosmos.SqlObjects.Visitors;

    internal abstract class SqlObject : IEquatable<SqlObject>
    {
        protected SqlObject(SqlObjectKind kind)
        {
            this.Kind = kind;
        }

        public SqlObjectKind Kind
        {
            get;
        }

        public abstract void Accept(SqlObjectVisitor visitor);

        public abstract TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor);

        public abstract TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input);

        public override string ToString()
        {
            return this.Serialize(prettyPrint: false);
        }

        public override int GetHashCode()
        {
            return this.Accept(SqlObjectHasher.Singleton);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is SqlObject sqlObject))
            {
                return false;
            }

            return this.Equals(sqlObject);
        }

        public bool Equals(SqlObject other)
        {
            return this.Accept(SqlEqualityVisitor.Singleton, other);
        }

        public string PrettyPrint()
        {
            return this.Serialize(prettyPrint: true);
        }

        public SqlObject GetObfuscatedObject()
        {
            SqlObjectObfuscator sqlObjectObfuscator = new SqlObjectObfuscator();
            return this.Accept(sqlObjectObfuscator);
        }

        private string Serialize(bool prettyPrint)
        {
            SqlObjectTextSerializer sqlObjectTextSerializer = new SqlObjectTextSerializer(prettyPrint);
            this.Accept(sqlObjectTextSerializer);
            return sqlObjectTextSerializer.ToString();
        }

        public static bool operator ==(SqlObject first, SqlObject second) => first.Equals(second);
        public static bool operator !=(SqlObject first, SqlObject second) => !(first == second);
    }
}
