//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Globalization;

    internal abstract class SqlScalarExpressionVisitor
    {
        protected SqlScalarExpression Visit(SqlScalarExpression expression)
        {
            if(expression == null)
            {
                return null;
            }

            switch(expression.Kind)
            {
                case SqlObjectKind.ArrayCreateScalarExpression:
                    return Visit(expression as SqlArrayCreateScalarExpression);
                case SqlObjectKind.BinaryScalarExpression:
                    return Visit(expression as SqlBinaryScalarExpression);
                case SqlObjectKind.CoalesceScalarExpression:
                    return Visit(expression as SqlCoalesceScalarExpression);
                case SqlObjectKind.ConditionalScalarExpression:
                    return Visit(expression as SqlConditionalScalarExpression);
                case SqlObjectKind.ConversionScalarExpression:
                    return Visit(expression as SqlConversionScalarExpression);
                case SqlObjectKind.FunctionCallScalarExpression:
                    return Visit(expression as SqlFunctionCallScalarExpression);
                case SqlObjectKind.GeoNearCallScalarExpression:
                    return Visit(expression as SqlGeoNearCallScalarExpression);
                case SqlObjectKind.InScalarExpression:
                    return Visit(expression as SqlInScalarExpression);
                case SqlObjectKind.LiteralScalarExpression:
                    return Visit(expression as SqlLiteralScalarExpression);
                case SqlObjectKind.MemberIndexerScalarExpression:
                    return Visit(expression as SqlMemberIndexerScalarExpression);
                case SqlObjectKind.ObjectCreateScalarExpression:
                    return Visit(expression as SqlObjectCreateScalarExpression);
                case SqlObjectKind.PropertyRefScalarExpression:
                    return Visit(expression as SqlPropertyRefScalarExpression);
                case SqlObjectKind.SubqueryCollectionExpression:
                    return Visit(expression as SqlSubqueryScalarExpression);
                case SqlObjectKind.UnaryScalarExpression:
                    return Visit(expression as SqlUnaryScalarExpression);
                case SqlObjectKind.ExistsScalarExpression:
                    return Visit(expression as SqlExistsScalarExpression);
                default:
                    throw new InvalidProgramException(
                        string.Format(CultureInfo.InvariantCulture, "Unexpected SqlObjectKind {0}", expression.Kind));
            }
        }

        protected abstract SqlScalarExpression Visit(SqlArrayCreateScalarExpression expression);
        protected abstract SqlScalarExpression Visit(SqlBinaryScalarExpression expression);
        protected abstract SqlScalarExpression Visit(SqlCoalesceScalarExpression expression);
        protected abstract SqlScalarExpression Visit(SqlConditionalScalarExpression expression);
        protected abstract SqlScalarExpression Visit(SqlConversionScalarExpression expression);
        protected abstract SqlScalarExpression Visit(SqlFunctionCallScalarExpression expression);
        protected abstract SqlScalarExpression Visit(SqlGeoNearCallScalarExpression expression);
        protected abstract SqlScalarExpression Visit(SqlInScalarExpression expression);
        protected abstract SqlScalarExpression Visit(SqlLiteralScalarExpression expression);
        protected abstract SqlScalarExpression Visit(SqlMemberIndexerScalarExpression expression);
        protected abstract SqlScalarExpression Visit(SqlObjectCreateScalarExpression expression);
        protected abstract SqlScalarExpression Visit(SqlPropertyRefScalarExpression expression);
        protected abstract SqlScalarExpression Visit(SqlSubqueryScalarExpression expression);
        protected abstract SqlScalarExpression Visit(SqlExistsScalarExpression expression);
        protected abstract SqlScalarExpression Visit(SqlUnaryScalarExpression expression);
    }
}
