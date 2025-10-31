//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Tests.Diagnostics
{
    using System.Reflection;

    /// <summary>
    /// Test-only utilities for accessing internal CosmosDiagnosticsContext state.
    /// Uses reflection to avoid exposing test-only APIs in production code.
    /// </summary>
    internal static class CosmosDiagnosticsContextTestUtilities
    {
        private static readonly FieldInfo ScopeStackField = typeof(CosmosDiagnosticsContext)
            .GetField("scopeStack", BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>
        /// Checks whether the internal scope stack is empty, indicating all scopes have been properly disposed.
        /// </summary>
        /// <param name="context">The diagnostics context to check.</param>
        /// <returns>True if the scope stack is empty; otherwise, false.</returns>
        public static bool IsScopeStackEmpty(CosmosDiagnosticsContext context)
        {
            if (ScopeStackField == null)
            {
                throw new System.InvalidOperationException("Could not find scopeStack field via reflection");
            }

            object stackObj = ScopeStackField.GetValue(context);
            if (stackObj is System.Collections.Generic.Stack<string> stack)
            {
                lock (stack)
                {
                    return stack.Count == 0;
                }
            }

            throw new System.InvalidOperationException("scopeStack field is not of expected type");
        }
    }
}
