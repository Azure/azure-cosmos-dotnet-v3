//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Linq
{
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Serializer;
    using Microsoft.Azure.Cosmos.SqlObjects;

    /// <summary>
    /// Wrapper class for translating LINQ to DocDB SQL.
    /// </summary>
    internal static class SqlTranslator
    {
        /// <summary>
        /// This function exists for testing only.
        /// </summary>
        /// <param name="inputExpression">Expression to translate.</param>
        /// <param name="linqSerializerOptions">Optional serializer options.</param>
        /// <returns>A string describing the expression translation.</returns>
        internal static string TranslateExpression(
            Expression inputExpression,
            CosmosLinqSerializerOptions linqSerializerOptions = null)
        {
            TranslationContext context = new TranslationContext(linqSerializerOptions);

            inputExpression = ConstantEvaluator.PartialEval(inputExpression);
            SqlScalarExpression scalarExpression = ExpressionToSql.VisitNonSubqueryScalarExpression(inputExpression, context);
            return scalarExpression.ToString();
        }

        internal static string TranslateExpressionOld(
            Expression inputExpression,
            CosmosLinqSerializerOptions linqSerializerOptions = null)
        {
            TranslationContext context = new TranslationContext(linqSerializerOptions);

            inputExpression = ConstantFolding.Fold(inputExpression);
            SqlScalarExpression scalarExpression = ExpressionToSql.VisitNonSubqueryScalarExpression(inputExpression, context);
            return scalarExpression.ToString();
        }

        internal static SqlQuerySpec TranslateQuery(
            Expression inputExpression,
            CosmosLinqSerializerOptions linqSerializerOptions,
            IDictionary<object, string> parameters)
        {
            inputExpression = ConstantEvaluator.PartialEval(inputExpression);
            SqlQuery query = ExpressionToSql.TranslateQuery(inputExpression, parameters, linqSerializerOptions);
            string queryText = null;
            SqlParameterCollection sqlParameters = new SqlParameterCollection();
            if (parameters != null && parameters.Count > 0)
            {
                foreach (KeyValuePair<object, string> keyValuePair in parameters)
                {
                    sqlParameters.Add(new Microsoft.Azure.Cosmos.Query.Core.SqlParameter(keyValuePair.Value, keyValuePair.Key));
                }
            }
            queryText = query.ToString();

            SqlQuerySpec sqlQuerySpec = new SqlQuerySpec(queryText, sqlParameters);
            return sqlQuerySpec;
        }
    }
}
