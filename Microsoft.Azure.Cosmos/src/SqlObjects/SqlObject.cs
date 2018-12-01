//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------


namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Text;

    internal abstract class SqlObject
    {
        public SqlObjectKind Kind
        {
            get;
            private set;
        }

        public SqlObject(SqlObjectKind kind)
        {
            this.Kind = kind;
        }

        public abstract void AppendToBuilder(StringBuilder builder);

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            this.AppendToBuilder(builder);
            return builder.ToString();
        }
    }
}
