namespace Microsoft.Azure.Cosmos.Tests.Json
{
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class JsonStringDictionaryTests
    {
        private const byte BinaryFormat = 128;

        /*
        [TestMethod]
        [Owner("mayapainter")]
        public void TestBasicCase()
        {
            IJsonStringDictionary jsonStringDictionary = new JsonStringDictionary();

            // First new string -> index 0
            JsonStringDictionaryTests.AddAndValidate(jsonStringDictionary, expectedString: "str1", expectedIndex: 0);

            // Second new string -> index 1
            JsonStringDictionaryTests.AddAndValidate(jsonStringDictionary, expectedString: "str2", expectedIndex: 1);

            // Re adding second string -> also index 1
            JsonStringDictionaryTests.AddAndValidate(jsonStringDictionary, expectedString: "str2", expectedIndex: 1);

            // Adding third new string -> index 2
            JsonStringDictionaryTests.AddAndValidate(jsonStringDictionary, expectedString: "str3", expectedIndex: 2);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void TestDictionarySizeLimit()
        {
            JsonStringDictionary jsonStringDictionary = new();
            Assert.IsFalse(jsonStringDictionary.TryAddString("hello", maxCount: 0, out int _));
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void TestCreateAndAddStrings()
        {
            IJsonStringDictionary stringDictionary = new JsonStringDictionary();

            // Strings are case sensitive.
            AddAndValidate(stringDictionary, "test0", 0);
            AddAndValidate(stringDictionary, "Test0", 1);

            // Inserting same string should return same index.
            AddAndValidate(stringDictionary, "test0", 0);
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void TestDictionaryComparison()
        {
            IJsonStringDictionary stringDictionary = new JsonStringDictionary();

            // Null comparison and reference-equals comparison.
            Assert.IsFalse(stringDictionary.Equals(null));
            Assert.IsTrue(stringDictionary.Equals(stringDictionary));

            // Subset comparison
            AddAndValidate(stringDictionary, "test0", 0);
            AddAndValidate(stringDictionary, "test1", 1);
            AddAndValidate(stringDictionary, "test2", 2);

            IJsonStringDictionary stringDictionary1 = new JsonStringDictionary();
            AddAndValidate(stringDictionary1, "test0", 0);
            AddAndValidate(stringDictionary1, "test1", 1);
            Assert.IsFalse(stringDictionary.Equals(stringDictionary1));

            // Reference-equals comparison
            AddAndValidate(stringDictionary1, "test2", 2);
            Assert.IsTrue(stringDictionary.Equals(stringDictionary1));

            // Superset comparison
            AddAndValidate(stringDictionary1, "test3", 3);
            Assert.IsFalse(stringDictionary.Equals(stringDictionary1));
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void TestAsMutableJsonStringDictionary()
        {
            IReadOnlyJsonStringDictionary stringDictionary1 = new JsonStringDictionary();
            Assert.IsNotNull(stringDictionary1.AsMutableJsonStringDictionary());

            IReadOnlyList<string> userStrings = new List<string> { "test" };
            IReadOnlyJsonStringDictionary stringDictionary2 = new JsonStringDictionary(userStrings, readOnly: false);
            Assert.IsNotNull(stringDictionary2.AsMutableJsonStringDictionary());

            IReadOnlyJsonStringDictionary stringDictionaryReadOnly = new JsonStringDictionary(userStrings, readOnly: true);
            Assert.IsNull(stringDictionaryReadOnly.AsMutableJsonStringDictionary());
        }

        private static void AddAndValidate(IJsonStringDictionary jsonStringDictionary, string expectedString, int expectedIndex, int capacity = 2080)
        {
            // Try to add the string.
            if (!jsonStringDictionary.TryAddString(expectedString, capacity, out int actualIndex))
            {
                throw new AssertFailedException($"{nameof(JsonStringDictionary.TryAddString)}({expectedString}, out int {actualIndex}) failed.");
            }

            Assert.AreEqual(expectedIndex, actualIndex);

            // Try to read the string back by index
            if (!jsonStringDictionary.TryGetString(expectedIndex, out UtfAllString actualString))
            {
                throw new AssertFailedException($"{nameof(JsonStringDictionary.TryGetString)}({expectedIndex}, out string {actualString}) failed.");
            }

            Assert.AreEqual(expectedString, actualString.Utf16String);
        }
        */
    }
}
