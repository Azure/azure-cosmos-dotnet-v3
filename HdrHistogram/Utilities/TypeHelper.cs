using System;
using System.Linq;
using System.Reflection;

namespace HdrHistogram.Utilities
{
    internal static class TypeHelper
    {
        /// <summary>
        /// Gets the constructor that matches the parameter array.
        /// Searches for a public instance constructor whose parameters match the types in the specified array.
        /// </summary>
        /// <param name="type">The type to search.</param>
        /// <param name="ctorArgTypes">An array of <see cref="Type"/> objects representing the number, order and type of the parameters for the desired constructor.</param>
        /// <returns>The <see cref="ConstructorInfo"/> if a match is found, else <c>null</c>.</returns>
        /// <remarks>
        /// In most versions of .NET this method is provided directly on <see cref="Type"/>, however for full support, we provide this ourselves.
        /// </remarks>
        public static ConstructorInfo GetConstructor( Type type, Type[] ctorArgTypes)
        {
            var info = type.GetTypeInfo();
            return info.DeclaredConstructors
                .FirstOrDefault(ctor => IsParameterMatch(ctor, ctorArgTypes));
        }

        private static bool IsParameterMatch(ConstructorInfo ctor, Type[] expectedParamters)
        {
            var ctorParams = ctor.GetParameters();
            return ctorParams.Select(p => p.ParameterType)
                .ToArray()
                .IsSequenceEqual(expectedParamters);
        }
    }
}
