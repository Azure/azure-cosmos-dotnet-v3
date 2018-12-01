//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Text;
    using System.Globalization;

    internal sealed class SqlConditionalScalarExpression : SqlScalarExpression
    {
        public SqlScalarExpression ConditionExpression
        {
            get;
            private set;
        }

        public SqlScalarExpression FirstExpression
        {
            get;
            private set;
        }

        public SqlScalarExpression SecondExpression
        {
            get;
            private set;
        }

        public SqlConditionalScalarExpression(
            SqlScalarExpression condition,
            SqlScalarExpression first,
            SqlScalarExpression second
            )
            : base(SqlObjectKind.ConditionalScalarExpression)
        {
            if(condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if(first == null)
            {
                throw new ArgumentNullException("first");
            }

            if (second == null)
            {
                throw new ArgumentNullException("second");
            }

            this.ConditionExpression = condition;
            this.FirstExpression = first;
            this.SecondExpression = second;
        }

        public override void AppendToBuilder(StringBuilder builder)
        {
            builder.Append('(');
            this.ConditionExpression.AppendToBuilder(builder);
            builder.Append(" ? ");
            this.FirstExpression.AppendToBuilder(builder);
            builder.Append(" : ");
            this.SecondExpression.AppendToBuilder(builder);
            builder.Append(')');
        }
    }
}
