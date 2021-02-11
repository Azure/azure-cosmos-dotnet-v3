//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;

    internal static class ConstantEvaluator
    {
        /// <summary> 
        /// Performs evaluation and replacement of independent sub-trees 
        /// </summary> 
        /// <param name="expression">The root of the expression tree.</param>
        /// <param name="fnCanBeEvaluated">A function that decides whether a given expression node can be part of the local function.</param>
        /// <returns>A new tree with sub-trees evaluated and replaced.</returns> 
        public static Expression PartialEval(Expression expression, Func<Expression, bool> fnCanBeEvaluated)
        {
            HashSet<Expression> candidates = Nominator.Nominate(expression, fnCanBeEvaluated);
            SubtreeEvaluator subTreeEvaluator = new SubtreeEvaluator(candidates);

            return subTreeEvaluator.Evaluate(expression);
        }

        /// <summary> 
        /// Performs evaluation and replacement of independent sub-trees 
        /// </summary> 
        /// <param name="expression">The root of the expression tree.</param>
        /// <returns>A new tree with sub-trees evaluated and replaced.</returns> 
        public static Expression PartialEval(Expression expression)
        {
            return PartialEval(expression, ConstantEvaluator.CanBeEvaluated);
        }

        private static bool CanBeEvaluated(Expression expression)
        {
            ConstantExpression constantExpression = expression as ConstantExpression;
            if (constantExpression != null)
            {
                if (constantExpression.Value is IQueryable)
                {
                    return false;
                }
            }

            MethodCallExpression methodCallExpression = expression as MethodCallExpression;
            if (methodCallExpression != null)
            {
                Type type = methodCallExpression.Method.DeclaringType;
                if (type == typeof(Enumerable) || type == typeof(Queryable) || type == typeof(CosmosLinq))
                {
                    return false;
                }
            }

            if (expression.NodeType == ExpressionType.Constant && expression.Type == typeof(object))
            {
                return true;
            }

            if (expression.NodeType == ExpressionType.Parameter || expression.NodeType == ExpressionType.Lambda)
            {
                return false;
            }

            return true;
        }
    }
}
