namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    class ComputedPropertyComparer : IEqualityComparer<ComputedProperty>
    {
        public static void AssertAreEqual(Collection<ComputedProperty> expected, Collection<ComputedProperty> actual)
        {
            int expectedCount = expected?.Count ?? 0;
            int actualCount = actual?.Count ?? 0;
            Assert.AreEqual(expectedCount, actualCount);

            for (int i = 0; i < expectedCount; i++)
            {
                AssertAreEqual(expected[i], actual[i]);
            }
        }

        public static void AssertAreEqual(ComputedProperty expected, ComputedProperty actual)
        {
            ComputedPropertyComparer comparer = new ComputedPropertyComparer();
            Assert.IsTrue(comparer.Equals(expected, actual), $"Expected: {ToString(expected)}{Environment.NewLine}Actual:{ToString(actual)}");
        }

        private static string ToString(ComputedProperty computedProperty) => $@"""Name"":""{computedProperty.Name}"", ""Query"":""{computedProperty.Query}""";

        public bool Equals(ComputedProperty x, ComputedProperty y)
        {
            if (x == null) return y == null;
            if (y == null) return false;

            return (x.Name?.Equals(y.Name) == true) &&
                (x.Query?.Equals(y.Query) == true);
        }

        public int GetHashCode([DisallowNull] ComputedProperty obj)
        {
            return obj.GetHashCode();
        }
    }
}
