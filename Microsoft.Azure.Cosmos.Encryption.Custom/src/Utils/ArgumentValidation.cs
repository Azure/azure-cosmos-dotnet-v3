//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Provides backwards-compatible argument validation methods optimized for performance.
    /// </summary>
    internal static class ArgumentValidation
    {
        /// <summary>
        /// Throws an <see cref="ArgumentNullException"/> if <paramref name="argument"/> is null.
        /// </summary>
        /// <param name="argument">The reference type argument to validate as non-null.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="argument"/> corresponds.</param>
        /// <exception cref="ArgumentNullException"><paramref name="argument"/> is null.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DebuggerStepThrough]
        public static void ThrowIfNull(
#if NET6_0_OR_GREATER
            [System.Diagnostics.CodeAnalysis.NotNull]
#endif
            object argument,
#if NET6_0_OR_GREATER
            [CallerArgumentExpression(nameof(argument))] string paramName = null)
#else
            string paramName = null)
#endif
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(argument, paramName);
#else
            if (argument is null)
            {
                throw new ArgumentNullException(paramName);
            }
#endif
        }

        /// <summary>
        /// Throws an <see cref="ArgumentNullException"/> if <paramref name="argument"/> is null,
        /// or an <see cref="ArgumentException"/> if it is empty.
        /// </summary>
        /// <param name="argument">The string argument to validate as non-null and non-empty.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="argument"/> corresponds.</param>
        /// <exception cref="ArgumentNullException"><paramref name="argument"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="argument"/> is empty.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DebuggerStepThrough]
        public static void ThrowIfNullOrEmpty(
#if NET6_0_OR_GREATER
            [System.Diagnostics.CodeAnalysis.NotNull]
#endif
            string argument,
#if NET6_0_OR_GREATER
            [CallerArgumentExpression(nameof(argument))] string paramName = null)
#else
            string paramName = null)
#endif
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrEmpty(argument, paramName);
#else
            if (argument is null)
            {
                throw new ArgumentNullException(paramName);
            }

            if (argument.Length == 0)
            {
                throw new ArgumentException("The value cannot be an empty string.", paramName);
            }
#endif
        }

        /// <summary>
        /// Throws an <see cref="ArgumentNullException"/> if <paramref name="argument"/> is null,
        /// or an <see cref="ArgumentException"/> if it is empty or contains only white-space characters.
        /// </summary>
        /// <param name="argument">The string argument to validate.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="argument"/> corresponds.</param>
        /// <exception cref="ArgumentNullException"><paramref name="argument"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="argument"/> is empty or whitespace.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DebuggerStepThrough]
        public static void ThrowIfNullOrWhiteSpace(
#if NET6_0_OR_GREATER
            [System.Diagnostics.CodeAnalysis.NotNull]
#endif
            string argument,
#if NET6_0_OR_GREATER
            [CallerArgumentExpression(nameof(argument))] string paramName = null)
#else
            string paramName = null)
#endif
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrWhiteSpace(argument, paramName);
#else
            if (argument is null)
            {
                throw new ArgumentNullException(paramName);
            }

            if (argument.Length == 0)
            {
                throw new ArgumentException("The value cannot be an empty string.", paramName);
            }

            if (string.IsNullOrWhiteSpace(argument))
            {
                throw new ArgumentException("The value cannot be an empty string or composed entirely of whitespace.", paramName);
            }
#endif
        }
    }
}
