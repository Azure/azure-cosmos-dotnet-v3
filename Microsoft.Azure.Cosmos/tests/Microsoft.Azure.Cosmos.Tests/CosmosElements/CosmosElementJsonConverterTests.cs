//-----------------------------------------------------------------------
// <copyright file="LazyCosmosElementTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.NetFramework.Tests.CosmosElements
{
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Tests for LazyCosmosElementTests.
    /// </summary>
    [TestClass]
    public class CosmosElementJsonConverterTests
    {
        [TestMethod]
        [Owner("brchon")]
        public void TrueTest()
        {
            string input = "true";
            CosmosElementJsonConverterTests.VerifyConverter(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void FalseTest()
        {
            string input = "false";
            CosmosElementJsonConverterTests.VerifyConverter(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void NullTest()
        {
            string input = "null";
            CosmosElementJsonConverterTests.VerifyConverter(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void IntegerTest()
        {
            string input = "1337";
            CosmosElementJsonConverterTests.VerifyConverter(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void DoubleTest()
        {
            string input = "1337.1337";
            CosmosElementJsonConverterTests.VerifyConverter(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void StringTest()
        {
            string input = "\"Hello World\"";
            CosmosElementJsonConverterTests.VerifyConverter(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void ArrayTest()
        {
            string input = "[-2,-1,0,1,2]";
            CosmosElementJsonConverterTests.VerifyConverter(input);
        }

        [TestMethod]
        [Owner("brchon")]
        public void ObjectTest()
        {
            string input = "{\"GlossDiv\":10,\"title\":\"example glossary\"}";
            CosmosElementJsonConverterTests.VerifyConverter(input);
        }


        private static string NewtonsoftFormat(string json)
        {
            return JsonConvert.SerializeObject(JToken.Parse(json), Formatting.None);
        }

        private static void VerifyConverter(string input)
        {
            input = CosmosElementJsonConverterTests.NewtonsoftFormat(input);
            CosmosElement cosmosElement = JsonConvert.DeserializeObject<CosmosElement>(input, new CosmosElementJsonConverter());
            string toString = JsonConvert.SerializeObject(cosmosElement, new CosmosElementJsonConverter());
            toString = CosmosElementJsonConverterTests.NewtonsoftFormat(toString);
            Assert.IsTrue(JToken.EqualityComparer.Equals(JToken.Parse(input), JToken.Parse(toString)), $"Expected:{input}, Actual:{toString}");
        }
    }
}