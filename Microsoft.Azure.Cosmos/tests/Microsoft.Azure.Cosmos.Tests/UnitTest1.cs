using Microsoft.Azure.Documents.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Azure.Cosmos;
using System;

namespace BinaryVsTextTest
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            Microsoft.Azure.Cosmos.FeedOptions feedOptions = new FeedOptions()
            {
                ContentSerializationOptions = CosmosBinary
            };
            

        }
    }
}
