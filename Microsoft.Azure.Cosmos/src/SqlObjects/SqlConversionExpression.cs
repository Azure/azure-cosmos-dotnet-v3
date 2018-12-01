//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    internal sealed class SqlConversionScalarExpression : SqlScalarExpression
    {
        private readonly SqlScalarExpression expression;
        private readonly Type sourceType;
        private readonly Type targetType;

        public SqlConversionScalarExpression(SqlScalarExpression expression, Type sourceType, Type targetType)
            : base(SqlObjectKind.ConversionScalarExpression)
        {
            this.expression = expression;
            this.sourceType = sourceType;
            this.targetType = targetType;
        }

        public override void AppendToBuilder(StringBuilder builder)
        {
            this.expression.AppendToBuilder(builder);
        }
    }
}
