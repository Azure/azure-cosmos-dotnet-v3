//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Client.Test
{
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class SessionTokenTests
    {
        [TestMethod]
        public void ValidateSuccessfulSessionTokenParsing()
        {
            // valid session token
            string sessionToken = "1#100#1=20#2=5#3=30";
            Assert.IsTrue(VectorSessionToken.TryCreate(sessionToken, out _));

            sessionToken = "500";
            Assert.IsTrue(SimpleSessionToken.TryCreate(sessionToken, out _));
        }

        [TestMethod]
        public void ValidateSessionTokenParsingWithInvalidVersion()
        {
            string sessionToken = "foo#100#1=20#2=5#3=30";
            Assert.IsFalse(VectorSessionToken.TryCreate(sessionToken, out _));
        }

        [TestMethod]
        public void ValidateSessionTokenParsingWithInvalidGlobalLsn()
        {
            string sessionToken = "1#foo#1=20#2=5#3=30";
            Assert.IsFalse(VectorSessionToken.TryCreate(sessionToken, out _));
        }

        [TestMethod]
        public void ValidateSessionTokenParsingWithInvalidRegionProgress()
        {
            string sessionToken = "1#100#1=20#2=x#3=30";
            Assert.IsFalse(VectorSessionToken.TryCreate(sessionToken, out _));
        }

        [TestMethod]
        public void ValidateSessionTokenParsingWithInvalidFormat()
        {
            string sessionToken = "1;100#1=20#2=40";
            Assert.IsFalse(VectorSessionToken.TryCreate(sessionToken, out _));

            sessionToken = "foo";
            Assert.IsFalse(SimpleSessionToken.TryCreate(sessionToken, out _));
        }

        [TestMethod]
        public void ValidateSessionTokenParsingFromEmptyString()
        {
            string sessionToken = "";
            Assert.IsFalse(VectorSessionToken.TryCreate(sessionToken, out _));
        }

        [TestMethod]
        public void ValidateSessionTokenComparison()
        {
            // valid session token

            Assert.IsTrue(VectorSessionToken.TryCreate("1#100#1=20#2=5#3=30", out ISessionToken sessionToken1));
            Assert.IsTrue(VectorSessionToken.TryCreate("2#105#4=10#2=5#3=30", out ISessionToken sessionToken2));
            Assert.IsFalse(sessionToken1.Equals(sessionToken2));
            Assert.IsFalse(sessionToken2.Equals(sessionToken1));
            Assert.IsTrue(sessionToken1.IsValid(sessionToken2));
            Assert.IsFalse(sessionToken2.IsValid(sessionToken1));

            Assert.IsTrue(VectorSessionToken.TryCreate("2#105#2=5#3=30#4=10", out ISessionToken sessionTokenMerged));
            Assert.IsTrue(sessionTokenMerged.Equals(sessionToken1.Merge(sessionToken2)));

            Assert.IsTrue(VectorSessionToken.TryCreate("1#100#1=20#2=5#3=30", out sessionToken1));
            Assert.IsTrue(VectorSessionToken.TryCreate("1#100#1=10#2=8#3=30", out sessionToken2));
            Assert.IsFalse(sessionToken1.Equals(sessionToken2));
            Assert.IsFalse(sessionToken2.Equals(sessionToken1));
            Assert.IsFalse(sessionToken1.IsValid(sessionToken2));
            Assert.IsFalse(sessionToken2.IsValid(sessionToken1));

            Assert.IsTrue(VectorSessionToken.TryCreate("1#100#1=20#2=8#3=30", out sessionTokenMerged));
            Assert.IsTrue(sessionTokenMerged.Equals(sessionToken1.Merge(sessionToken2)));

            Assert.IsTrue(VectorSessionToken.TryCreate("1#100#1=20#2=5#3=30", out sessionToken1));
            Assert.IsTrue(VectorSessionToken.TryCreate("1#102#1=100#2=8#3=30", out sessionToken2));
            Assert.IsFalse(sessionToken1.Equals(sessionToken2));
            Assert.IsFalse(sessionToken2.Equals(sessionToken1));
            Assert.IsTrue(sessionToken1.IsValid(sessionToken2));
            Assert.IsFalse(sessionToken2.IsValid(sessionToken1));

            Assert.IsTrue(VectorSessionToken.TryCreate("1#102#2=8#3=30#1=100", out sessionTokenMerged));
            Assert.IsTrue(sessionTokenMerged.Equals(sessionToken1.Merge(sessionToken2)));

            Assert.IsTrue(VectorSessionToken.TryCreate("1#101#1=20#2=5#3=30", out sessionToken1));
            Assert.IsTrue(VectorSessionToken.TryCreate("1#100#1=20#2=5#3=30#4=40", out sessionToken2));

            try
            {
                sessionToken1.Merge(sessionToken2);
                Assert.Fail("Region progress can not be different when version is same");
            }
            catch (InternalServerErrorException)
            {
            }

            try
            {
                sessionToken2.IsValid(sessionToken1);
                Assert.Fail("Region progress can not be different when version is same");
            }
            catch (InternalServerErrorException)
            {
            }

            Assert.IsTrue(SimpleSessionToken.TryCreate("100", out sessionToken1));
            Assert.IsTrue(SimpleSessionToken.TryCreate("200", out sessionToken2));
            Assert.IsTrue(sessionToken1.Merge(sessionToken2).Equals(sessionToken2));
            Assert.IsFalse(sessionToken2.Equals(sessionToken1));
            Assert.IsTrue(sessionToken1.IsValid(sessionToken2));
            Assert.IsFalse(sessionToken2.IsValid(sessionToken1));

            Assert.IsTrue(SimpleSessionToken.TryCreate("100", out sessionToken1));
            Assert.IsTrue(SimpleSessionToken.TryCreate("100", out sessionToken2));
            Assert.IsTrue(sessionToken1.Merge(sessionToken2).Equals(sessionToken1));
            Assert.IsTrue(sessionToken2.Equals(sessionToken1));
            Assert.IsTrue(sessionToken1.IsValid(sessionToken2));
            Assert.IsTrue(sessionToken2.IsValid(sessionToken1));
        }
    }
}