//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    public static class State
    {
        public static int Iteration = 0;
        public static int IterationForSuccess = 0;
        public static int IterationForMax = 0;
    }

    [RetriableTestClassAttribute]
    public class RetriableTestClassAttributeTests
    {
        [TestMethod]
        public void RetriableTestClassAttribute_RetryOnce()
        {
            if (State.Iteration++ == 0)
            {
                throw new Exception();
            }
        }

        [TestMethod]
        public void RetriableTestClassAttribute_DontRetryOnSuccess()
        {
            if (State.IterationForSuccess++ > 0)
            {
                throw new Exception();
            }
        }
    }

    [RetriableTestClassAttribute(3)]
    public class RetriableTestClassAttributeTestsWithMax
    {
        [TestMethod]
        public void RetriableTestClassAttribute_RetryUpToMax()
        {
            if (State.IterationForMax++ < 3)
            {
                throw new Exception();
            }
        }
    }
}
