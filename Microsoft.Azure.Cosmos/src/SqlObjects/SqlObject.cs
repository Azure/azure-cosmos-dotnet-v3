//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    internal abstract class SqlObject
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

        public override string ToString() => this.Serialize(prettyPrint: false);

        public override int GetHashCode() => this.Accept(SqlObjectHasher.Singleton);

        public string PrettyPrint() => this.Serialize(prettyPrint: true);

        public SqlObject GetObfuscatedObject() => this.Accept(new SqlObjectObfuscator());

        private string Serialize(bool prettyPrint)
        {
            SqlObjectTextSerializer sqlObjectTextSerializer = new SqlObjectTextSerializer(prettyPrint);
            this.Accept(sqlObjectTextSerializer);
            return sqlObjectTextSerializer.ToString();
        }
    }
}
