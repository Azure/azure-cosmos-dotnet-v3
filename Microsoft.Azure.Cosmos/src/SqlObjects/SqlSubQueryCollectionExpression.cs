//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Text;

    // WARNING: this class does not exist in C++
    // It was just created for testing purposes.

    internal sealed class SqlSubqueryCollectionExpression : SqlCollectionExpression
    {
        public SqlSubqueryCollection Query
        {
            get;
            private set;
        }

        public SqlIdentifier InputIdentifier
        { 
            get; 
            private set;
        }

        public SqlSubqueryCollectionExpression(SqlIdentifier inputIdentifier, SqlSubqueryCollection query)
            : base(SqlObjectKind.SubqueryCollectionExpression)
        {
            if (inputIdentifier == null)
            {
                throw new ArgumentException("inputIdentifier");
            }

            if (query == null)
            {
                throw new ArgumentNullException("query");
            }

            this.InputIdentifier = inputIdentifier;
            this.Query = query;
        }

        public override void AppendToBuilder(StringBuilder builder)
        {
            this.InputIdentifier.AppendToBuilder(builder);
            builder.Append(" IN (");
            this.Query.AppendToBuilder(builder);
            builder.Append(")");
        }
    }
}
