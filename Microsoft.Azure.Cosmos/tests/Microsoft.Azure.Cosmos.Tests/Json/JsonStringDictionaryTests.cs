namespace Microsoft.Azure.Cosmos.NetFramework.Tests.Json
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
            JsonStringDictionary jsonStringDictionary = new JsonStringDictionary(100);
            JsonStringDictionaryTests.AddAndValidate(jsonStringDictionary, value: "str1", expectedIndex: 0);
            JsonStringDictionaryTests.AddAndValidate(jsonStringDictionary, value: "str2", expectedIndex: 1);
            JsonStringDictionaryTests.AddAndValidate(jsonStringDictionary, value: "str3", expectedIndex: 2);

            // Verify the Dictionary

        }

        private static void AddAndValidate(JsonStringDictionary jsonStringDictionary, string value, int expectedIndex)
        {
            if (!jsonStringDictionary.TryAddString(value, out int actualIndex))
            {
                throw new AssertFailedException($"Failed to insert {value} into {nameof(JsonStringDictionary)}.");
            }

            Assert.AreEqual(expectedIndex, actualIndex);
        }
    }
}
