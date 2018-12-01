//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Globalization;
    using System.Text;

    internal sealed class SqlUnaryScalarExpression : SqlScalarExpression
    {
        public SqlUnaryScalarOperatorKind OperatorKind
        {
            get;
            private set;
        }

        public SqlScalarExpression Expression
        {
            get;
            private set;
        }

        public SqlUnaryScalarExpression(
            SqlUnaryScalarOperatorKind operatorKind,
            SqlScalarExpression expression)
            : base(SqlObjectKind.UnaryScalarExpression)
        {
            if (expression == null)
            {
                throw new ArgumentNullException("expression");
            }
            
            this.OperatorKind = operatorKind;
            this.Expression = expression;
        }

        public static void Append(SqlUnaryScalarOperatorKind kind, StringBuilder builder)
        {
            switch (kind)
            {
                case SqlUnaryScalarOperatorKind.BitwiseNot:
                    builder.Append("~");
                    break;
                case SqlUnaryScalarOperatorKind.Not:
                    builder.Append("NOT");
                    break;
                case SqlUnaryScalarOperatorKind.Minus:
                    builder.Append("-");
                    break;
                case SqlUnaryScalarOperatorKind.Plus:
                    builder.Append("+");
                    break;
                default:
                    throw new ArgumentException(string.Format(CultureInfo.CurrentUICulture, "Unsupported operator {0}", kind));
            }
        }

        public override void AppendToBuilder(StringBuilder builder)
        {
            builder.Append("(");
            Append(this.OperatorKind, builder);
            builder.Append(" ");
            this.Expression.AppendToBuilder(builder);
            builder.Append(")");
        }
    }
}