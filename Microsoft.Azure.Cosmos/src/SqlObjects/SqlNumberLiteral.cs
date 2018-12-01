//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Globalization;
    using System.Text;
    using Newtonsoft.Json;

    internal sealed class SqlNumberLiteral : SqlLiteral
    {
        public object Value
        {
            get;
            private set;
        }

        private SqlNumberLiteral(object value)
            : base(SqlObjectKind.NumberLiteral)
        {
            this.Value = value;
        }

        public SqlNumberLiteral(Byte value)
            : this((object)value)
        {
        }

        public SqlNumberLiteral(Decimal value)
            : this((object)value)
        {
        }

        public SqlNumberLiteral(Double value)
            : this((object)value)
        {
        }

        public SqlNumberLiteral(Int16 value)
            : this((object)value)
        {
        }

        public SqlNumberLiteral(Int32 value)
            : this((object)value)
        {
        }

        public SqlNumberLiteral(Int64 value)
            : this((object)value)
        {
        }

        public SqlNumberLiteral(SByte value)
            : this((object)value)
        {
        }

        public SqlNumberLiteral(Single value)
            : this((object)value)
        {
        }

        public SqlNumberLiteral(UInt16 value)
            : this((object)value)
        {
        }

        public SqlNumberLiteral(UInt32 value)
            : this((object)value)
        {
        }

        public SqlNumberLiteral(UInt64 value)
            : this((object)value)
        {
        }

        public override void AppendToBuilder(StringBuilder builder)
        {
            builder.AppendFormat(CultureInfo.InvariantCulture, "{0}", JsonConvert.SerializeObject(this.Value));
        }
    }
}