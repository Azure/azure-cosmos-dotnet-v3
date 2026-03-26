//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;

    /// <summary>
    /// Provides a shared compile strategy for LambdaExpression that avoids generating
    /// JIT-compiled DynamicMethods whose IL persists in native memory.
    /// On .NET 6+ runtimes, uses Compile(preferInterpretation: true) to interpret
    /// expressions without IL emission. On older runtimes, falls back to standard Compile().
    /// See: https://github.com/Azure/azure-cosmos-dotnet-v3/issues/5487
    /// </summary>
    internal static class ExpressionCompileHelper
    {
        private static readonly object[] PreferInterpretationArgs = new object[] { true };
        private static readonly Func<LambdaExpression, Delegate> InterpretedCompile = ExpressionCompileHelper.CreateInterpretedCompile();
        private static readonly bool IsInterpretedCompileSupported = ExpressionCompileHelper.InterpretedCompile != null;
        private static readonly bool IsInterpretationEnabled = ConfigurationManager.GetEnvironmentVariable(
            ConfigurationManager.LinqExpressionCompileInterpretationEnabled,
            defaultValue: true);

        /// <summary>
        /// Compiles a strongly-typed Expression using interpretation mode when available
        /// to avoid native memory growth from DynamicMethod IL emission.
        /// Prefer this overload when the expression type is known at compile time.
        /// </summary>
        public static TDelegate CompileLambda<TDelegate>(Expression<TDelegate> expression)
            where TDelegate : Delegate
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }

            if (ExpressionCompileHelper.IsInterpretedCompileSupported
                && ExpressionCompileHelper.IsInterpretationEnabled)
            {
                return expression.Compile(preferInterpretation: true);
            }

            return expression.Compile();
        }

        /// <summary>
        /// Compiles a LambdaExpression using interpretation mode when available
        /// to avoid native memory growth from DynamicMethod IL emission.
        /// Use this overload when only an untyped LambdaExpression is available.
        /// </summary>
        public static Delegate CompileLambda(LambdaExpression lambda)
        {
            if (lambda == null)
            {
                throw new ArgumentNullException(nameof(lambda));
            }

            if (ExpressionCompileHelper.InterpretedCompile != null
                && ExpressionCompileHelper.IsInterpretationEnabled)
            {
                return ExpressionCompileHelper.InterpretedCompile(lambda);
            }

            return lambda.Compile();
        }

        private static Func<LambdaExpression, Delegate> CreateInterpretedCompile()
        {
            MethodInfo compileWithPreference = typeof(LambdaExpression)
                .GetMethod(nameof(LambdaExpression.Compile), new Type[] { typeof(bool) });

            if (compileWithPreference != null)
            {
                return lambda => (Delegate)compileWithPreference.Invoke(lambda, ExpressionCompileHelper.PreferInterpretationArgs);
            }

            return null;
        }
    }
}
