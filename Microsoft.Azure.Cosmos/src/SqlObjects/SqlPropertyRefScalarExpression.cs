//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Sql
{
    using System;

    internal sealed class SqlPropertyRefScalarExpression : SqlScalarExpression
    {
        public SqlScalarExpression MemberExpression
        {
            get;
            private set;
        }

        public SqlIdentifier PropertyIdentifier
        {
            get;
            private set;
        }

        public SqlPropertyRefScalarExpression(
            SqlScalarExpression memberExpression,
            SqlIdentifier propertyIdentifier)
            : base(SqlObjectKind.PropertyRefScalarExpression)
        {
            if (propertyIdentifier == null)
            {
                throw new ArgumentNullException("propertyIdentifier");
            }
            
            this.MemberExpression = memberExpression;
            this.PropertyIdentifier = propertyIdentifier;
        }

        public override void AppendToBuilder(System.Text.StringBuilder builder)
        {
            if (this.MemberExpression != null)
            {
                this.MemberExpression.AppendToBuilder(builder);
                builder.Append(".");
            }

            this.PropertyIdentifier.AppendToBuilder(builder);
        }
    }
}
