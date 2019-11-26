using System.Net;
using Microsoft.Azure.Documents;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Azure.Cosmos.Resource.Throughput
{
    [TestClass]
    public class ThroughputResponseTests
    {
        [TestMethod]
        public void OfferReplacePendingHeaderNotSetReturnsNull()
        {
            ThroughputResponse throughputResponse =
                new ThroughputResponse(HttpStatusCode.OK, new Headers(), null, null);

            Assert.IsNull(throughputResponse.IsReplacePending);
        }

        [TestMethod]
        public void OfferReplacePendingHeaderSetTrueReturnsTrue()
        {
            ThroughputResponse throughputResponse = new ThroughputResponse(HttpStatusCode.OK, new Headers
            {
                {WFConstants.BackendHeaders.OfferReplacePending, "true"}
            }, null, null);

            Assert.IsNotNull(throughputResponse.IsReplacePending);
            Assert.IsTrue(throughputResponse.IsReplacePending.Value);
        }

        [TestMethod]
        public void OfferReplacePendingHeaderSetFalseReturnsFalse()
        {
            ThroughputResponse throughputResponse = new ThroughputResponse(HttpStatusCode.OK, new Headers
            {
                {WFConstants.BackendHeaders.OfferReplacePending, "false"}
            }, null, null);

            Assert.IsNotNull(throughputResponse.IsReplacePending);
            Assert.IsFalse(throughputResponse.IsReplacePending.Value);
        }
    }
}
