//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class RequestOptionTests
    {
        [TestMethod]
        public void CustomHeadersTests()
        {
            string customHearName = "custom-header1";
            string customHeaderValue = "value1";
            ImmutableDictionary<string, string>.Builder builder = ImmutableDictionary.CreateBuilder<string, string>();
            builder.Add(customHearName, customHeaderValue);

            RequestOptions ro = new RequestOptions();
            ro.CustomRequestHeaders = builder.ToImmutable();

            RequestMessage message = new RequestMessage();
            ro.PopulateRequestOptions(message);

            Assert.IsTrue(message.Headers.TryGetValue(customHearName, out string headerValue));
            Assert.AreEqual(customHeaderValue, headerValue);
        }
    }
}
