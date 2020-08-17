namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    internal static class ArgumentValidatorExtensions
    {
        internal static bool IsNull<T>(this T parameter)
        {
            return null == parameter;
        }

        internal static void ValidateNotNull<T>(this T parameter, string name)
        {
            if (parameter.IsNull())
            {
                throw new ArgumentNullException(string.Concat(name, " [", typeof(T), "]"));
            }
        }

        internal static void ValidateNotNullOrWhitespace(this string parameter, string name)
        {
            if (string.IsNullOrWhiteSpace(parameter))
            {
                throw new ArgumentException($"{name} cannot be null or empty or consist of only whitespace.");
            }
        }

        internal static void ValidateNotEmpty<T>(this IEnumerable<T> parameter, string name)
        {
            if (!parameter.Any())
            {
                throw new ArgumentException($"{name} cannot be empty.");
            }
        }

        internal static void ValidateNotNullForEach<T>(this IEnumerable<T> parameters, string name)
        {

            if (parameters.Any(t => t.IsNull()))
            {
                throw new ArgumentException($"None of the elements in {name} can be null or empty.");
            }
        }

        internal static void ValidateNotNullOrWhitespaceForEach(this IEnumerable<string> parameters, string name)
        {
            if (parameters.Any(s => string.IsNullOrWhiteSpace(s)))
            {
                throw new ArgumentException($"One of more of the elements in {name} is null or empty or consist of only whitespace.");
            }
        }

        internal static void ValidateGreaterThanSize<T>(this IEnumerable<T> parameter, int size, string name)
        {
            if (parameter.Count() < size)
            {
                throw new ArgumentOutOfRangeException($"{name} must contain at least {size} elements.");
            }
        }

        internal static void ValidateType(this object value, Type type, string name)
        {
            if (!value.GetType().Equals(type))
            {
                throw new InvalidCastException($"Expected {name} to be of type {type}");
            }

        }
    }
}

