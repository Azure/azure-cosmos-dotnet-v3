namespace Microsoft.Azure.Cosmos.Tests
{
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Cosmos;
    using System;
    using System.Threading.Tasks;

    [TestClass]
    public class BinaryPerfTest
    {
        [TestMethod]
        public void BinaryFormat()
        {
            FeedOptions feedOptions = new FeedOptions()
            {
                ContentSerializationFormat = ContentSerializationFormat.CosmosBinary
            };
        }
    }
}
