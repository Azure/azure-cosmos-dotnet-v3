//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;

    internal static class Utilities
    {
        /// <summary>
        /// Add quotation signs to a string.
        /// </summary>
        /// <param name="toQuote">String to quote.</param>
        /// <returns>A string properly quoted for embedding into SQL.</returns>
        public static string SqlQuoteString(string toQuote)
        {
            toQuote = toQuote.Replace("'", "\\'").Replace("\"", "\\\"");
            toQuote = "\"" + toQuote + "\"";
            return toQuote;
        }

        /// <summary>
        /// Get a lambda expression; may unpeel quotes.
        /// </summary> 
        /// <param name="expr">Expression to convert to a lambda.</param>
        /// <returns>The contained lambda expression, or an exception.</returns>
        public static LambdaExpression GetLambda(Expression expr)
        {
            while (expr.NodeType == ExpressionType.Quote)
            {
                expr = ((UnaryExpression)expr).Operand;
            }

            if (expr.NodeType != ExpressionType.Lambda)
            {
                throw new ArgumentException("Expected a lambda expression");
            }

            return expr as LambdaExpression;
        }

        /// <summary>
        /// Generate a new parameter and add it to the current scope.
        /// </summary>
        /// <param name="prefix">Prefix for the parameter name.</param>
        /// <param name="type">Parameter type.</param>
        /// <param name="inScope">Names to avoid.</param>
        /// <returns>The new parameter.</returns>
        public static ParameterExpression NewParameter(string prefix, Type type, HashSet<ParameterExpression> inScope)
        {
            int suffix = 0;
            while (true)
            {
                string name = prefix + suffix.ToString(CultureInfo.InvariantCulture);
                ParameterExpression param = Expression.Parameter(type, name);
                if (!inScope.Any(p => p.Name.Equals(name)))
                {
                    inScope.Add(param);
                    return param;
                }
                suffix++;
            }
        }
    }

    internal abstract class ExpressionSimplifier
    {
        private static readonly ConcurrentDictionary<Type, ExpressionSimplifier> cached = new ConcurrentDictionary<Type, ExpressionSimplifier>();
        public abstract object EvalBoxed(Expression expr);

        public static object Evaluate(Expression expr)
        {
            ExpressionSimplifier evaluator;
            if (cached.ContainsKey(expr.Type))
            {
                evaluator = cached[expr.Type];
            }
            else
            {
                Type qType = typeof(ExpressionSimplifier<>).MakeGenericType(expr.Type);
                evaluator = (ExpressionSimplifier)Activator.CreateInstance(qType);
                cached.TryAdd(expr.Type, evaluator);
            }
            return evaluator.EvalBoxed(expr);
        }

        public static Expression EvaluateToExpression(Expression expr)
        {
            object value = Evaluate(expr);
            return Expression.Constant(value, expr.Type);
        }
    }

    internal sealed class ExpressionSimplifier<T> : ExpressionSimplifier
    {
        public override object EvalBoxed(Expression expr)
        {
            return this.Eval(expr);
        }

        public T Eval(Expression expr)
        {
            Expression<Func<T>> lambda = Expression.Lambda<Func<T>>(expr);
            Func<T> func = lambda.Compile();
            return func();
        }
    }
}
