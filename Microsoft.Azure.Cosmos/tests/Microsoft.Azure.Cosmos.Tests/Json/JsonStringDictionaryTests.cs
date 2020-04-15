namespace Microsoft.Azure.Cosmos.Tests.Json
{
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class JsonStringDictionaryTests
    {
        private const byte BinaryFormat = 128;

        [TestInitialize]
        public void TestInitialize()
        {
            // Put test init code here
        }

        [ClassInitialize]
        public static void Initialize(TestContext textContext)
        {
            // put class init code here
        }

        [TestMethod]
        [Owner("brchon")]
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
        [Owner("brchon")]
        public void TestDictionarySizeLimit()
        {
            JsonStringDictionary jsonStringDictionary = new JsonStringDictionary(capacity: 0);
            Assert.IsFalse(jsonStringDictionary.TryAddString("hello", out int index));
        }

        [TestMethod]
        [Owner("brchon")]
        public void TestDifferentLengthStrings()
        {
            JsonStringDictionary jsonStringDictionary = new JsonStringDictionary(capacity: 256);
            for (int replicationCount = 0; replicationCount < 128; replicationCount++)
            {
                JsonStringDictionaryTests.AddAndValidate(
                    jsonStringDictionary,
                    expectedString: new string('a', replicationCount),
                    expectedIndex: replicationCount);
            }
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
            if (!jsonStringDictionary.TryGetStringAtIndex(expectedIndex, out UtfAllString actualString))
            {
                throw new AssertFailedException($"{nameof(JsonStringDictionary.TryGetStringAtIndex)}({expectedIndex}, out string {actualString}) failed.");
            }

            Assert.AreEqual(expectedString, actualString);
        }
    }
}
