//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Linq
{
    using Microsoft.Azure.Cosmos.Sql;
    using System.Linq.Expressions;
    using System.Text;

    /// <summary>
    /// Wrapper class for translating LINQ to DocDB SQL.
    /// </summary>
    internal static class SqlTranslator
    {
        /// <summary>
        /// This function exists for testing only.
        /// </summary>
        /// <param name="inputExpression">Expression to translate.</param>
        /// <returns>A string describing the expression translation.</returns>
        internal static string TranslateExpression(Expression inputExpression)
        {
            TranslationContext context = new TranslationContext();

            inputExpression = ConstantEvaluator.PartialEval(inputExpression);
            SqlScalarExpression scalarExpression = ExpressionToSql.VisitNonSubqueryScalarExpression(inputExpression, context);
            return scalarExpression.ToString();
        }

        internal static string TranslateExpressionOld(Expression inputExpression)
        {
            TranslationContext context = new TranslationContext();

            inputExpression = ConstantFolding.Fold(inputExpression);
            SqlScalarExpression scalarExpression = ExpressionToSql.VisitNonSubqueryScalarExpression(inputExpression, context);
            return scalarExpression.ToString();
        }

        internal static SqlQuerySpec TranslateQuery(Expression inputExpression)
        {
            inputExpression = ConstantEvaluator.PartialEval(inputExpression);
            SqlQuery query = ExpressionToSql.TranslateQuery(inputExpression);
            return new SqlQuerySpec(query.ToString());
        }
    }
}
