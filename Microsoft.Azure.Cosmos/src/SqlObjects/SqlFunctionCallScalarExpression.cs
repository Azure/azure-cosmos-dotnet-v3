//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;

    internal sealed class SqlFunctionCallScalarExpression : SqlScalarExpression
    {
        private const string UdfNamespaceQualifier = "udf.";

        public SqlIdentifier Name
        { 
            get; 
            private set;
        }

        public SqlScalarExpression[] Arguments
        {
            get;
            private set;
        }

        public bool IsUdf
        {
            get;
            private set;
        }

        public SqlFunctionCallScalarExpression(
            SqlIdentifier name,
            SqlScalarExpression[] arguments)
            : this(name, arguments, false)
        {
        }

        public SqlFunctionCallScalarExpression(
            SqlIdentifier name,
            SqlScalarExpression[] arguments,
            bool isUdf)
            : base(SqlObjectKind.FunctionCallScalarExpression)
        {
            if (name == null)
            {
                throw new ArgumentNullException("name");
            }

            this.Arguments = arguments;
            this.Name = name;
            this.IsUdf = isUdf;
        }

        public override void AppendToBuilder(System.Text.StringBuilder builder)
        {
            if (this.IsUdf)
            {
                builder.Append(UdfNamespaceQualifier);
            }

            builder.Append(this.Name);
            builder.Append("(");
            for (int i = 0; i < this.Arguments.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }
                
                this.Arguments[i].AppendToBuilder(builder);
            }
            builder.Append(")");
        }
    }
}
