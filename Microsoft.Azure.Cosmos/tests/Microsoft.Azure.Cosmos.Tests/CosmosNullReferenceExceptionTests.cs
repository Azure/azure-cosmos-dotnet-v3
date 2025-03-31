//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="CancellationToken"/>  scenarios.
    /// </summary>
    [TestClass]
    public class CosmosNullReferenceExceptionTests
    {
        [TestMethod]
        public void CosmosNullRefWrapingTest()
        {
            string message = "Test1234 NullReferenceException";
            NullReferenceException nullReferenceException;
            try
            {
                throw new NullReferenceException(message);
            }
            catch (NullReferenceException nre)
            {
                nullReferenceException = nre;
            }

            string rootTraceName = "TestRoot";
            ITrace trace = Trace.GetRootTrace(rootTraceName);
            using (trace.StartChild("startChild")) { }

            CosmosNullReferenceException cosmosNullReferenceException = new CosmosNullReferenceException(
                nullReferenceException,
                trace);

            Assert.AreEqual(nullReferenceException.StackTrace, cosmosNullReferenceException.StackTrace);
            Assert.AreEqual(nullReferenceException, cosmosNullReferenceException.InnerException);
            Assert.AreEqual(nullReferenceException.Data, cosmosNullReferenceException.Data);


            Assert.IsTrue(cosmosNullReferenceException.Message.Contains(message));
            Assert.IsTrue(cosmosNullReferenceException.Message.Contains(rootTraceName));
            Assert.AreNotEqual(nullReferenceException.Message, cosmosNullReferenceException.Message);
            string cosmosToString = cosmosNullReferenceException.ToString();
            Assert.IsFalse(cosmosToString.Contains("Microsoft.Azure.Cosmos.CosmosNullReferenceException"), $"The internal wrapper exception should not be exposed to users. {cosmosToString}");
            Assert.IsTrue(cosmosToString.Contains(message));
            Assert.IsTrue(cosmosToString.Contains(rootTraceName));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ExpectArgumentNullExceptionTest()
        {
            _ = new CosmosNullReferenceException(null, NoOpTrace.Singleton);
        }
    }
}