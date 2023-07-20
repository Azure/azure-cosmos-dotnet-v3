namespace Microsoft.Azure.Cosmos.Tests.Resource.Settings
{
    using System;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DedicatedGatewayRequestOptionsTests
    {
        [TestMethod]
        public void BypassIntegratedCacheHeaderIsSetWhenTrue()
        {
            DedicatedGatewayRequestOptions dedicatedGatewayRequestOptions = new DedicatedGatewayRequestOptions
            {
                BypassIntegratedCache = true
            };

            ItemRequestOptions itemRequestOptions = new ItemRequestOptions
            {
                DedicatedGatewayRequestOptions = dedicatedGatewayRequestOptions
            };

            QueryRequestOptions queryRequestOptions = new QueryRequestOptions
            {
                DedicatedGatewayRequestOptions = dedicatedGatewayRequestOptions
            };

            RequestMessage itemRequestMessage = new RequestMessage();
            RequestMessage queryRequestMessage = new RequestMessage();

            itemRequestOptions.PopulateRequestOptions(itemRequestMessage);
            queryRequestOptions.PopulateRequestOptions(queryRequestMessage);


            Assert.IsNotNull(itemRequestMessage.Headers[HttpConstants.HttpHeaders.DedicatedGatewayPerRequestBypassIntegratedCache]);
            Assert.IsNotNull(queryRequestMessage.Headers[HttpConstants.HttpHeaders.DedicatedGatewayPerRequestBypassIntegratedCache]);

            Assert.AreEqual("True", itemRequestMessage.Headers[HttpConstants.HttpHeaders.DedicatedGatewayPerRequestBypassIntegratedCache]);
            Assert.AreEqual("True", queryRequestMessage.Headers[HttpConstants.HttpHeaders.DedicatedGatewayPerRequestBypassIntegratedCache]);
        }

        [TestMethod]
        public void BypassIntegratedCacheHeaderIsNotSetWhenFalse()
        {
            DedicatedGatewayRequestOptions dedicatedGatewayRequestOptions = new DedicatedGatewayRequestOptions
            {
                BypassIntegratedCache = false
            };

            ItemRequestOptions itemRequestOptions = new ItemRequestOptions
            {
                DedicatedGatewayRequestOptions = dedicatedGatewayRequestOptions
            };

            QueryRequestOptions queryRequestOptions = new QueryRequestOptions
            {
                DedicatedGatewayRequestOptions = dedicatedGatewayRequestOptions
            };

            RequestMessage itemRequestMessage = new RequestMessage();
            RequestMessage queryRequestMessage = new RequestMessage();

            itemRequestOptions.PopulateRequestOptions(itemRequestMessage);
            queryRequestOptions.PopulateRequestOptions(queryRequestMessage);


            Assert.IsNull(itemRequestMessage.Headers[HttpConstants.HttpHeaders.DedicatedGatewayPerRequestBypassIntegratedCache]);
            Assert.IsNull(queryRequestMessage.Headers[HttpConstants.HttpHeaders.DedicatedGatewayPerRequestBypassIntegratedCache]);
        }

        [TestMethod]
        public void BypassIntegratedCacheHeaderIsNotSetWhenNotSet()
        {
            ItemRequestOptions itemRequestOptions = new ItemRequestOptions
            {
                DedicatedGatewayRequestOptions = new DedicatedGatewayRequestOptions()
            };

            QueryRequestOptions queryRequestOptions = new QueryRequestOptions
            {
                DedicatedGatewayRequestOptions = new DedicatedGatewayRequestOptions()
            };

            RequestMessage itemRequestMessage = new RequestMessage();
            RequestMessage queryRequestMessage = new RequestMessage();

            itemRequestOptions.PopulateRequestOptions(itemRequestMessage);
            queryRequestOptions.PopulateRequestOptions(queryRequestMessage);


            Assert.IsNull(itemRequestMessage.Headers[HttpConstants.HttpHeaders.DedicatedGatewayPerRequestBypassIntegratedCache]);
            Assert.IsNull(queryRequestMessage.Headers[HttpConstants.HttpHeaders.DedicatedGatewayPerRequestBypassIntegratedCache]);
        }

        [TestMethod]
        public void ShardKeyHeaderIsSetWhenSet()
        {
            string shardKey = Guid.NewGuid().ToString();
            DedicatedGatewayRequestOptions dedicatedGatewayRequestOptions = new DedicatedGatewayRequestOptions
            {
                ShardKey = shardKey
            };

            ItemRequestOptions itemRequestOptions = new ItemRequestOptions
            {
                DedicatedGatewayRequestOptions = dedicatedGatewayRequestOptions
            };

            QueryRequestOptions queryRequestOptions = new QueryRequestOptions
            {
                DedicatedGatewayRequestOptions = dedicatedGatewayRequestOptions
            };

            RequestMessage itemRequestMessage = new RequestMessage();
            RequestMessage queryRequestMessage = new RequestMessage();

            itemRequestOptions.PopulateRequestOptions(itemRequestMessage);
            queryRequestOptions.PopulateRequestOptions(queryRequestMessage);


            Assert.IsNotNull(itemRequestMessage.Headers[HttpConstants.HttpHeaders.DedicatedGatewayShardKey]);
            Assert.IsNotNull(queryRequestMessage.Headers[HttpConstants.HttpHeaders.DedicatedGatewayShardKey]);

            Assert.AreEqual(shardKey, itemRequestMessage.Headers[HttpConstants.HttpHeaders.DedicatedGatewayShardKey]);
            Assert.AreEqual(shardKey, queryRequestMessage.Headers[HttpConstants.HttpHeaders.DedicatedGatewayShardKey]);
        }

        [TestMethod]
        public void ShardKeyHeaderIsNotSetWhenEmpty()
        {
            DedicatedGatewayRequestOptions dedicatedGatewayRequestOptions = new DedicatedGatewayRequestOptions
            {
                ShardKey = ""
            };

            ItemRequestOptions itemRequestOptions = new ItemRequestOptions
            {
                DedicatedGatewayRequestOptions = dedicatedGatewayRequestOptions
            };

            QueryRequestOptions queryRequestOptions = new QueryRequestOptions
            {
                DedicatedGatewayRequestOptions = dedicatedGatewayRequestOptions
            };

            RequestMessage itemRequestMessage = new RequestMessage();
            RequestMessage queryRequestMessage = new RequestMessage();

            itemRequestOptions.PopulateRequestOptions(itemRequestMessage);
            queryRequestOptions.PopulateRequestOptions(queryRequestMessage);


            Assert.IsNull(itemRequestMessage.Headers[HttpConstants.HttpHeaders.DedicatedGatewayShardKey]);
            Assert.IsNull(queryRequestMessage.Headers[HttpConstants.HttpHeaders.DedicatedGatewayShardKey]);
        }

        [TestMethod]
        public void ShardKeyHeaderIsNotSetWhenNotSet()
        {
            ItemRequestOptions itemRequestOptions = new ItemRequestOptions
            {
                DedicatedGatewayRequestOptions = new DedicatedGatewayRequestOptions()
            };

            QueryRequestOptions queryRequestOptions = new QueryRequestOptions
            {
                DedicatedGatewayRequestOptions = new DedicatedGatewayRequestOptions()
            };

            RequestMessage itemRequestMessage = new RequestMessage();
            RequestMessage queryRequestMessage = new RequestMessage();

            itemRequestOptions.PopulateRequestOptions(itemRequestMessage);
            queryRequestOptions.PopulateRequestOptions(queryRequestMessage);


            Assert.IsNull(itemRequestMessage.Headers[HttpConstants.HttpHeaders.DedicatedGatewayShardKey]);
            Assert.IsNull(queryRequestMessage.Headers[HttpConstants.HttpHeaders.DedicatedGatewayShardKey]);
        }
    }
}
