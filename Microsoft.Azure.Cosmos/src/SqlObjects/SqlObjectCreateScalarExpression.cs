//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Text;

    internal sealed class SqlObjectCreateScalarExpression : SqlScalarExpression
    {
        public SqlObjectProperty[] Properties
        {
            get;
            private set;
        }

        public SqlObjectCreateScalarExpression(SqlObjectProperty[] properties)
            : base(SqlObjectKind.ObjectCreateScalarExpression)
        {
            if (properties == null)
            {
                throw new ArgumentNullException("vecProperties");
            }
            
            this.Properties = properties;
        }

        public override void AppendToBuilder(StringBuilder builder)
        {
            builder.Append("{");
            for (int i = 0; i < this.Properties.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }
                
                this.Properties[i].AppendToBuilder(builder);
            }
            builder.Append("}");
        }
    }
}
