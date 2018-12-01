//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Text;
    using Newtonsoft.Json;

    internal sealed class SqlObjectLiteral : SqlLiteral
    {
        private readonly bool isValueSerialized;

        public object Value
        {
            get;
            private set;
        }

        public SqlObjectLiteral(object value, bool isValueSerialized)
            : base(SqlObjectKind.ObjectLiteral)
        {
            if(value == null)
            {
                throw new ArgumentNullException("value");
            }

            this.Value = value;
            this.isValueSerialized = isValueSerialized;
        }

        public override void AppendToBuilder(StringBuilder builder)
        {
            if (this.isValueSerialized)
            {
                builder.Append(this.Value);
            }
            else
            {
                builder.Append(JsonConvert.SerializeObject(this.Value));
            }
        }
    }
}