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
    abstract class SqlObject : IEquatable<SqlObject>
    {
        protected SqlObject()
        {
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
            return SqlObject.Equals(this, other);
        }

        public string PrettyPrint()
        {
            return this.Serialize(prettyPrint: true);
        }

        public SqlObject GetObfuscatedObject()
        {
            return this.Accept(new SqlObjectObfuscator());
        }

        private string Serialize(bool prettyPrint)
        {
            SqlObjectTextSerializer sqlObjectTextSerializer = new SqlObjectTextSerializer(prettyPrint);
            this.Accept(sqlObjectTextSerializer);
            return sqlObjectTextSerializer.ToString();
        }

        public static bool Equals(SqlObject first, SqlObject second)
        {
#if !DEBUG
            if (object.ReferenceEquals(first, second))
            {
                return true;
            }
#else
            if ((first is null) && (second is null))
            {
                return true;
            }
#endif

            if ((first is null) || (second is null))
            {
                return false;
            }

            return first.Accept(SqlObjectEqualityVisitor.Singleton, second);
        }

        public static bool operator ==(SqlObject first, SqlObject second)
        {
            return SqlObject.Equals(first, second);
        }

        public static bool operator !=(SqlObject first, SqlObject second)
        {
            return !(first == second);
        }
    }
}
