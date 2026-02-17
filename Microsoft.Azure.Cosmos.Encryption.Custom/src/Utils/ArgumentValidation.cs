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

        /// <summary>
        /// Throws an <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is negative.
        /// </summary>
        /// <param name="value">The argument to validate as non-negative.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="value"/> corresponds.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DebuggerStepThrough]
        public static void ThrowIfNegative(
            int value,
#if NET6_0_OR_GREATER
            [CallerArgumentExpression(nameof(value))] string paramName = null)
#else
            string paramName = null)
#endif
        {
#if NET8_0_OR_GREATER
            ArgumentOutOfRangeException.ThrowIfNegative(value, paramName);
#else
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(paramName, value, $"'{paramName}' must be a non-negative value.");
            }
#endif
        }

        /// <summary>
        /// Throws an <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is greater than <paramref name="other"/>.
        /// </summary>
        /// <param name="value">The argument to validate.</param>
        /// <param name="other">The value to compare with <paramref name="value"/>.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="value"/> corresponds.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is greater than <paramref name="other"/>.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DebuggerStepThrough]
        public static void ThrowIfGreaterThan(
            int value,
            int other,
#if NET6_0_OR_GREATER
            [CallerArgumentExpression(nameof(value))] string paramName = null)
#else
            string paramName = null)
#endif
        {
#if NET8_0_OR_GREATER
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, other, paramName);
#else
            if (value > other)
            {
                throw new ArgumentOutOfRangeException(paramName, value, $"'{paramName}' must be less than or equal to {other}.");
            }
#endif
        }

        /// <summary>
        /// Throws an <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is negative.
        /// </summary>
        /// <param name="value">The argument to validate as non-negative.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="value"/> corresponds.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DebuggerStepThrough]
        public static void ThrowIfNegative(
            TimeSpan value,
#if NET6_0_OR_GREATER
            [CallerArgumentExpression(nameof(value))] string paramName = null)
#else
            string paramName = null)
#endif
        {
            if (value < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(paramName, value, $"'{paramName}' must be a non-negative value.");
            }
        }

        /// <summary>
        /// Throws an <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is greater than or equal to <paramref name="other"/>.
        /// </summary>
        /// <param name="value">The argument to validate.</param>
        /// <param name="other">The value to compare with <paramref name="value"/>.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="value"/> corresponds.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is greater than or equal to <paramref name="other"/>.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DebuggerStepThrough]
        public static void ThrowIfGreaterThanOrEqual(
            TimeSpan value,
            TimeSpan other,
#if NET6_0_OR_GREATER
            [CallerArgumentExpression(nameof(value))] string paramName = null)
#else
            string paramName = null)
#endif
        {
            if (value >= other)
            {
                throw new ArgumentOutOfRangeException(paramName, value, $"'{paramName}' ({value}) must be less than {other}.");
            }
        }
    }
}
