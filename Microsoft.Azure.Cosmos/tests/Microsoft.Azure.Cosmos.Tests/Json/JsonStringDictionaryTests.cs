namespace Microsoft.Azure.Cosmos.Tests.Json
{
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class JsonStringDictionaryTests
    {
        private const byte BinaryFormat = 128;

        [TestMethod]
        [Owner("mayapainter")]
        public void TestBasicCase()
        {
            JsonStringDictionary jsonStringDictionary = new JsonStringDictionary(capacity: 100);

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
            JsonStringDictionary jsonStringDictionary = new JsonStringDictionary(capacity: 0);
            Assert.IsFalse(jsonStringDictionary.TryAddString("hello", out int _));
        }


        private static void AddAndValidate(JsonStringDictionary jsonStringDictionary, string expectedString, int expectedIndex)
        {
            // Try to add the string.
            if (!jsonStringDictionary.TryAddString(expectedString, out int actualIndex))
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
    }
}
