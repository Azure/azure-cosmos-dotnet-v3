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
        private static readonly Func<LambdaExpression, Delegate> CompileLambdaDelegate = ExpressionCompileHelper.CreateCompileLambda();

        /// <summary>
        /// Compiles a LambdaExpression using interpretation mode when available
        /// to avoid native memory growth from DynamicMethod IL emission.
        /// </summary>
        public static Delegate CompileLambda(LambdaExpression lambda)
        {
            if (lambda == null)
            {
                throw new ArgumentNullException(nameof(lambda));
            }

            return ExpressionCompileHelper.CompileLambdaDelegate(lambda);
        }

        private static Func<LambdaExpression, Delegate> CreateCompileLambda()
        {
            bool useInterpretation = ConfigurationManager.GetEnvironmentVariable(
                ConfigurationManager.LinqExpressionCompileInterpretationEnabled,
                defaultValue: true);

            if (useInterpretation)
            {
                MethodInfo compileWithPreference = typeof(LambdaExpression)
                    .GetMethod(nameof(LambdaExpression.Compile), new Type[] { typeof(bool) });

                if (compileWithPreference != null)
                {
                    return lambda => (Delegate)compileWithPreference.Invoke(lambda, ExpressionCompileHelper.PreferInterpretationArgs);
                }
            }

            return lambda => lambda.Compile();
        }
    }
}
