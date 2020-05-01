//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Globalization;
    using System.Linq.Expressions;
    using Microsoft.Azure.Cosmos.Query.Core;

    internal static class DocumentQueryEvaluator
    {
        private const string SQLMethod = "AsSQL";

        public static SqlQuerySpec Evaluate(
            Expression expression,
            CosmosSerializationOptions serializationOptions = null,
            ImmutableDictionary<object, string> parameters = null)
        {
            switch (expression.NodeType)
            {
                case ExpressionType.Constant:
                    {
                        return DocumentQueryEvaluator.HandleEmptyQuery((ConstantExpression)expression);
                    }
                case ExpressionType.Call:
                    {
                        return DocumentQueryEvaluator.HandleMethodCallExpression((MethodCallExpression)expression, parameters, serializationOptions);
                    }

                default:
                    throw new DocumentQueryException(
                        string.Format(CultureInfo.CurrentUICulture,
                        ClientResources.BadQuery_InvalidExpression,
                        expression.ToString()));
            }
        }

        public static bool IsTransformExpression(Expression expression)
        {
            return expression is MethodCallExpression methodCallExpression &&
                methodCallExpression.Method.DeclaringType == typeof(DocumentQueryable) &&
                (methodCallExpression.Method.Name == DocumentQueryEvaluator.SQLMethod);
        }

        /// <summary>
        /// This is to handle the case, where user just executes code like this.
        /// foreach(Database db in client.CreateDatabaseQuery()) {}        
        /// </summary>
        /// <param name="expression"></param>
        private static SqlQuerySpec HandleEmptyQuery(ConstantExpression expression)
        {
            if (expression.Value == null)
            {
                throw new DocumentQueryException(
                    string.Format(CultureInfo.CurrentUICulture,
                    ClientResources.BadQuery_InvalidExpression,
                    expression.ToString()));
            }

            Type expressionValueType = expression.Value.GetType();
            if (!expressionValueType.IsGenericType || !(expressionValueType.GetGenericTypeDefinition() == typeof(DocumentQuery<bool>).GetGenericTypeDefinition() || expressionValueType.GetGenericTypeDefinition() == typeof(CosmosLinqQuery<bool>).GetGenericTypeDefinition()))
            {
                throw new DocumentQueryException(
                    string.Format(CultureInfo.CurrentUICulture,
                    ClientResources.BadQuery_InvalidExpression,
                    expression.ToString()));
            }

            //No query specified.
            return null;
        }

        private static SqlQuerySpec HandleMethodCallExpression(
            MethodCallExpression expression,
            ImmutableDictionary<object, string> parameters,
            CosmosSerializationOptions serializationOptions = null)
        {
            if (DocumentQueryEvaluator.IsTransformExpression(expression))
            {
                if (string.Compare(expression.Method.Name, DocumentQueryEvaluator.SQLMethod, StringComparison.Ordinal) != 0)
                {
                    throw new DocumentQueryException(
                        string.Format(CultureInfo.CurrentUICulture,
                        ClientResources.BadQuery_InvalidExpression,
                        expression.ToString()));
                }

                return DocumentQueryEvaluator.HandleAsSqlTransformExpression(expression);
            }

            return SqlTranslator.TranslateQuery(expression, serializationOptions, parameters);
        }

        /// <summary>
        /// foreach(string record in client.CreateDocumentQuery().Navigate("Raw JQuery"))
        /// </summary>
        /// <param name="expression"></param>
        private static SqlQuerySpec HandleAsSqlTransformExpression(MethodCallExpression expression)
        {
            Expression paramExpression = expression.Arguments[1];

            if (paramExpression.NodeType == ExpressionType.Lambda)
            {
                LambdaExpression lambdaExpression = (LambdaExpression)paramExpression;
                // Send the lambda expression through the partial evaluator.
                return GetSqlQuerySpec(lambdaExpression.Compile().DynamicInvoke(null));
            }
            else if (paramExpression.NodeType == ExpressionType.Constant)
            {
                ConstantExpression constantExpression = (ConstantExpression)paramExpression;
                return GetSqlQuerySpec(constantExpression.Value);
            }
            else
            {
                LambdaExpression lamdaExpression = Expression.Lambda(paramExpression);
                return GetSqlQuerySpec(lamdaExpression.Compile().DynamicInvoke(null));
            }
        }

        private static SqlQuerySpec GetSqlQuerySpec(object value)
        {
            switch (value)
            {
                case null:
                    throw new DocumentQueryException(
                    string.Format(CultureInfo.CurrentUICulture,
                    ClientResources.BadQuery_InvalidExpression,
                    value));

                case SqlQuerySpec sqlQuerySpec:
                    return sqlQuerySpec;

                case string stringValue:
                    return new SqlQuerySpec(stringValue);

                default:
                    throw new DocumentQueryException(
                        string.Format(CultureInfo.CurrentUICulture,
                        ClientResources.BadQuery_InvalidExpression,
                        value));
            }
        }
    }
}
