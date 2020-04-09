//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json.Linq;
    using System.IO;

    [TestClass]
    public class CosmosThroughputTests
    {
        [TestMethod]
        public async Task AutopilotThroughputSerializationTest()
        {
            AutopilotThroughputProperties autopilotThroughputProperties = new AutopilotThroughputProperties(1000);
            Assert.AreEqual(1000, autopilotThroughputProperties.MaxThroughput);
            Assert.IsNull(autopilotThroughputProperties.Throughput);

            using (Stream stream = MockCosmosUtil.Serializer.ToStream<AutopilotThroughputProperties>(autopilotThroughputProperties))
            {
                using(StreamReader reader = new StreamReader(stream))
                {
                    string output = await reader.ReadToEndAsync();
                    Assert.IsNotNull(output);
                    Assert.AreEqual("{\"offerVersion\":\"V2\",\"content\":{\"offerAutopilotSettings\":{\"maxThroughput\":1000}}}", output);
                }
            }

            OfferAutopilotProperties autopilotProperties = new OfferAutopilotProperties(1000);
            using (Stream stream = MockCosmosUtil.Serializer.ToStream<OfferAutopilotProperties>(autopilotProperties))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    string output = await reader.ReadToEndAsync();
                    Assert.IsNotNull(output);
                    Assert.AreEqual("{\"maxThroughput\":1000}", output);
                }
            }

            using (Stream stream = MockCosmosUtil.Serializer.ToStream<OfferAutopilotProperties>(autopilotProperties))
            {
                OfferAutopilotProperties fromStream = MockCosmosUtil.Serializer.FromStream<OfferAutopilotProperties>(stream);
                Assert.IsNotNull(fromStream);
                Assert.AreEqual(1000, fromStream.MaxThroughput);
            }

            OfferContentProperties content = OfferContentProperties.CreateAutoPilotOfferConent(1000);
            using (Stream stream = MockCosmosUtil.Serializer.ToStream<OfferContentProperties>(content))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    string output = await reader.ReadToEndAsync();
                    Assert.IsNotNull(output);
                    Assert.AreEqual("{\"offerAutopilotSettings\":{\"maxThroughput\":1000}}", output);
                }
            }

            using (Stream stream = MockCosmosUtil.Serializer.ToStream<OfferContentProperties>(content))
            {
                OfferContentProperties fromStream = MockCosmosUtil.Serializer.FromStream<OfferContentProperties>(stream);
                Assert.IsNotNull(fromStream.OfferAutopilotSettings);
                Assert.AreEqual(1000, fromStream.OfferAutopilotSettings.MaxThroughput);
                Assert.IsNull(fromStream.OfferThroughput);
            }

            using (Stream stream = MockCosmosUtil.Serializer.ToStream<AutopilotThroughputProperties>(autopilotThroughputProperties))
            {
                AutopilotThroughputProperties fromStream = MockCosmosUtil.Serializer.FromStream<AutopilotThroughputProperties>(stream);
                Assert.AreEqual(1000, fromStream.MaxThroughput);
                Assert.IsNull(fromStream.Throughput); ;
            }
        }
    }
}
