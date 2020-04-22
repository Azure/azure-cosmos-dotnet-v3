//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosCustomHeaderTests
    {
        private string property;
        private Action<string> setter;
        private Func<string> getter;
        public CosmosCustomHeaderTests()
        {
            this.setter = (string value) => { this.property = value; };
            this.getter = () => this.property;
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ValidateNullGetter()
        {
            new CosmosCustomHeader(null, this.setter);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ValidateNullSetter()
        {
            new CosmosCustomHeader(this.getter, null);
        }

        [TestMethod]
        public void GetterAndSetterGetCalled()
        {
            string value = Guid.NewGuid().ToString();
            CosmosCustomHeader header = new CosmosCustomHeader(this.getter, this.setter);
            Assert.IsNull(this.getter());
            this.setter(value);
            Assert.AreEqual(value, this.getter());
            Assert.AreEqual(value, this.property);
        }
    }
}
