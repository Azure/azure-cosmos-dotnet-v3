//------------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Text;

    // This class represents a collection expression that is comprised of a collection definition and an 
    // optional alias.
    // Examples:
    //  FROM Person p
    //  FROM [1, 3, 5, 7] a

    internal sealed class SqlAliasedCollectionExpression : SqlCollectionExpression
    {
        public SqlCollection Collection
        {
            get;
            private set;
        }

        public SqlIdentifier Alias
        {
            get;
            private set;
        }

        public SqlAliasedCollectionExpression(
            SqlCollection collection,
            SqlIdentifier alias)
            : base(SqlObjectKind.AliasedCollectionExpression)
        {
            if (collection == null)
            {
                throw new ArgumentNullException("collection");
            }
            
            this.Collection = collection;
            this.Alias = alias;
        }

        public override void AppendToBuilder(StringBuilder builder)
        {
            this.Collection.AppendToBuilder(builder);
            if (this.Alias != null)
            {
                builder.Append(" AS ");
                this.Alias.AppendToBuilder(builder);
            }
        }
    }
}
