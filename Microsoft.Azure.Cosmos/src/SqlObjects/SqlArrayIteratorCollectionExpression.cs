//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Text;

    internal sealed class SqlArrayIteratorCollectionExpression : SqlCollectionExpression
    {
        public SqlIdentifier Alias
        {
            get;
            private set;
        }

        public SqlCollection Collection
        {
            get;
            private set;
        }

        public SqlArrayIteratorCollectionExpression(
           SqlIdentifier alias,
           SqlCollection collection)
            : base(SqlObjectKind.ArrayIteratorCollectionExpression)
        {
            if (alias == null)
            {
                throw new ArgumentNullException("alias");
            }

            if (collection == null)
            {
                throw new ArgumentNullException("collection");
            }

            this.Alias = alias;
            this.Collection = collection;
        }

        public override void AppendToBuilder(StringBuilder builder)
        {
            this.Alias.AppendToBuilder(builder);
            builder.Append(" IN ");
            this.Collection.AppendToBuilder(builder);
        }
    }
}
