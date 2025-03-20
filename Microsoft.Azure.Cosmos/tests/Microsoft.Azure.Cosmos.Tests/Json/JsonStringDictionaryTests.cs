namespace Microsoft.Azure.Cosmos.Tests.Json
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static Microsoft.Azure.Cosmos.Json.JsonBinaryEncoding;

    [TestClass]
    public class JsonStringDictionaryTests
    {
        [TestMethod]
        [Owner("mayapainter")]
        public void TestBasicCase()
        {
            List<string> strings = new List<string> { "test0", "test1", "test2", "test3", "test4", "test5" };
            JsonStringDictionary stringDictionary = new JsonStringDictionary(strings);
            Assert.AreEqual(6, stringDictionary.GetCount());

            for (int i = 0; i < stringDictionary.GetCount(); i++)
            {
                Assert.IsTrue(stringDictionary.TryGetString(i, out UtfAllString value));
                Assert.AreEqual(strings[i], value.Utf16String);

                Assert.IsTrue(stringDictionary.TryGetIndex(value.Utf8String.Span, out int index));
                Assert.AreEqual(i, index);
            }        
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void TestDictionarySizeLimit()
        {
            int maxDictionarySize = TypeMarker.UserString1ByteLengthMax - TypeMarker.UserString1ByteLengthMin + ((TypeMarker.UserString2ByteLengthMax - TypeMarker.UserString2ByteLengthMin) * 0xFF);

            List<string> strings = new();
            for (int i = 0; i < maxDictionarySize; i++)
            {
                strings.Add("test" + i);
            }

            JsonStringDictionary jsonStringDictionary0 = new JsonStringDictionary(strings);
            Assert.AreEqual(maxDictionarySize, jsonStringDictionary0.GetCount());

            strings.Add("testString");

            try
            {
                JsonStringDictionary jsonStringDictionary1 = new JsonStringDictionary(strings);
                Assert.Fail("Should not be able to create JsonStringDictionary over max size");
            }
            catch(ArgumentException ex)
            {
                Assert.AreEqual("Failed to add testString to JsonStringDictionary.", ex.Message);
            }
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void TestDictionaryComparison()
        {
            IJsonReadOnlyStringDictionary stringDictionary0 = new JsonStringDictionary();

            // Null comparison and reference-equals comparison.
            Assert.IsFalse(stringDictionary0.Equals(null));
            Assert.IsTrue(stringDictionary0.Equals(stringDictionary0));

            // Subset comparison
            IJsonReadOnlyStringDictionary stringDictionary1 = new JsonStringDictionary(new List<string> { "test0", "test1", "test2" });
            IJsonReadOnlyStringDictionary stringDictionary2 = new JsonStringDictionary(new List<string> { "test0", "test1" });
            Assert.IsFalse(stringDictionary1.Equals(stringDictionary2));

            // Value-equals comparison
            IJsonReadOnlyStringDictionary stringDictionary3 = new JsonStringDictionary(new List<string> { "test0", "test1", "test2" });
            Assert.IsTrue(stringDictionary1.Equals(stringDictionary3));

            // Superset comparison
            IJsonReadOnlyStringDictionary stringDictionary4 = new JsonStringDictionary(new List<string> { "test0", "test1", "test2", "test3" });
            Assert.IsFalse(stringDictionary1.Equals(stringDictionary4));

            // Order-sensitive comparison
            IJsonReadOnlyStringDictionary stringDictionary5 = new JsonStringDictionary(new List<string> { "test1", "test0", "test2" });
            Assert.IsFalse(stringDictionary1.Equals(stringDictionary5));
        }
    }
}
