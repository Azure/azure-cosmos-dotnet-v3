//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class RequestOptionTests
    {
        private static readonly string CustomHeaderName = "custom-header1";
        private static readonly string CustomHeaderValue = "value1";

        [TestMethod]
        public void CustomHeadersTests()
        {

            RequestOptions reqeustOptions = new RequestOptions();
            reqeustOptions.CustomRequestHeaders = new Dictionary<string, string>()
                {
                    { RequestOptionTests.CustomHeaderName, RequestOptionTests.CustomHeaderValue},
                };

            RequestMessage message = new RequestMessage();
            reqeustOptions.PopulateRequestOptions(message);

            Assert.IsTrue(message.Headers.TryGetValue(CustomHeaderName, out string headerValue));
            Assert.AreEqual(CustomHeaderValue, headerValue);
        }

        [TestMethod]
        public void TransactionalbatchCustomHeaderTest()
        {
            ItemRequestOptions itemRequestOptions = new ItemRequestOptions();
            itemRequestOptions.CustomRequestHeaders = new Dictionary<string, string>()
                {
                    { RequestOptionTests.CustomHeaderName, RequestOptionTests.CustomHeaderValue},
                };

            TransactionalBatchItemRequestOptions batchItemRequestOptions = TransactionalBatchItemRequestOptions.FromItemRequestOptions(itemRequestOptions);
            Assert.AreSame(itemRequestOptions.CustomRequestHeaders, batchItemRequestOptions.CustomRequestHeaders);
        }
    }
}
