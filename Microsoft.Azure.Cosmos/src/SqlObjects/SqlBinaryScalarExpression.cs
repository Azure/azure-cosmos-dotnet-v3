//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Text;
    using System.Globalization;

    internal sealed class SqlBinaryScalarExpression : SqlScalarExpression
    {
        public SqlBinaryScalarOperatorKind OperatorKind
        {
            get;
            private set;
        }

        public SqlScalarExpression LeftExpression
        {
            get;
            private set;
        }

        public SqlScalarExpression RightExpression
        {
            get;
            private set;
        }

        public SqlBinaryScalarExpression(
            SqlBinaryScalarOperatorKind operatorKind,
            SqlScalarExpression leftExpression,
            SqlScalarExpression rightExpression)
            : base(SqlObjectKind.BinaryScalarExpression)
        {
            if (leftExpression == null || rightExpression == null)
            {
                throw new ArgumentNullException();
            }
            this.OperatorKind = operatorKind;
            this.LeftExpression = leftExpression;
            this.RightExpression = rightExpression;
        }

        public static void Append(SqlBinaryScalarOperatorKind kind, StringBuilder builder)
        {
            switch (kind)
            {
                case SqlBinaryScalarOperatorKind.Add:
                    builder.Append("+");
                    break;
                case SqlBinaryScalarOperatorKind.And:
                    builder.Append("AND");
                    break;
                case SqlBinaryScalarOperatorKind.BitwiseAnd:
                    builder.Append("&");
                    break;
                case SqlBinaryScalarOperatorKind.BitwiseOr:
                    builder.Append("|");
                    break;
                case SqlBinaryScalarOperatorKind.BitwiseXor:
                    builder.Append("^");
                    break;
                case SqlBinaryScalarOperatorKind.Coalesce:
                    builder.Append("??");
                    break;
                case SqlBinaryScalarOperatorKind.Divide:
                    builder.Append("/");
                    break;
                case SqlBinaryScalarOperatorKind.Equal:
                    builder.Append("=");
                    break;
                case SqlBinaryScalarOperatorKind.GreaterThan:
                    builder.Append(">");
                    break;
                case SqlBinaryScalarOperatorKind.GreaterThanOrEqual:
                    builder.Append(">=");
                    break;
                case SqlBinaryScalarOperatorKind.LessThan:
                    builder.Append("<");
                    break;
                case SqlBinaryScalarOperatorKind.LessThanOrEqual:
                    builder.Append("<=");
                    break;
                case SqlBinaryScalarOperatorKind.Modulo:
                    builder.Append("%");
                    break;
                case SqlBinaryScalarOperatorKind.Multiply:
                    builder.Append("*");
                    break;
                case SqlBinaryScalarOperatorKind.NotEqual:
                    builder.Append("!=");
                    break;
                case SqlBinaryScalarOperatorKind.Or:
                    builder.Append("OR");
                    break;
                case SqlBinaryScalarOperatorKind.StringConcat:
                    builder.Append("||");
                    break;
                case SqlBinaryScalarOperatorKind.Subtract:
                    builder.Append("-");
                    break;
                default:
                    throw new ArgumentException(string.Format(CultureInfo.CurrentUICulture, "Unsupported operator {0}", kind));
            }
        }

        public override void AppendToBuilder(StringBuilder builder)
        {
            builder.Append("(");
            this.LeftExpression.AppendToBuilder(builder);
            builder.Append(" ");
            SqlBinaryScalarExpression.Append(this.OperatorKind, builder);
            builder.Append(" ");
            this.RightExpression.AppendToBuilder(builder);
            builder.Append(")");
        }
    }
}
