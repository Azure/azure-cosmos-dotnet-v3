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

        /// <summary>
        /// Compiles a LambdaExpression using interpretation mode when available
        /// to avoid native memory growth from DynamicMethod IL emission.
        /// The behavior can be toggled at runtime via the
        /// AZURE_COSMOS_LINQ_EXPRESSION_INTERPRETATION_ENABLED environment variable.
        /// </summary>
        public static Delegate CompileLambda(LambdaExpression lambda)
        {
            if (lambda == null)
            {
                throw new ArgumentNullException(nameof(lambda));
            }

            if (ExpressionCompileHelper.InterpretedCompile != null
                && ConfigurationManager.GetEnvironmentVariable(
                    ConfigurationManager.LinqExpressionCompileInterpretationEnabled,
                    defaultValue: true))
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
