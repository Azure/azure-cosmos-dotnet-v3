//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System.Linq;
    using Microsoft.Azure.Cosmos.Services.Management.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    public sealed class RetriableTestClassAttribute : TestClassAttribute
    {
        private readonly int maxRetry;

        /// <summary>
        /// Creates a TestClass that retries TestMethods once upon failure.
        /// </summary>
        public RetriableTestClassAttribute()
        {
            this.maxRetry = 1;
        }

        /// <summary>
        /// Creates a TestClass that retries TestMethods up to <paramref name="maxRetry"/> times.
        /// </summary>
        public RetriableTestClassAttribute(int maxRetry)
        {
            this.maxRetry = maxRetry;
        }

        public override TestMethodAttribute GetTestMethodAttribute(TestMethodAttribute testMethodAttribute)
        {
            TestMethodAttribute baseTestmethod = base.GetTestMethodAttribute(testMethodAttribute);
            return new RetriableTestMethod(this.maxRetry, baseTestmethod);
        }
    }

    public class RetriableTestMethod : TestMethodAttribute
    {
        private readonly TestMethodAttribute testMethod;
        private int maxRetry;
        public RetriableTestMethod(
            int maxRetry,
            TestMethodAttribute testMethod)
        {
            this.maxRetry = maxRetry;
            this.testMethod = testMethod;
        }

        public override TestResult[] Execute(ITestMethod testMethod) => this.ExecuteWithRetry(this.maxRetry, testMethod);

        private TestResult[] ExecuteWithRetry(
            int retryCount,
            ITestMethod testMethod)
        {
            TestResult[] testResults = base.Execute(testMethod);
            if (testResults.Any((tr) => tr.Outcome == UnitTestOutcome.Failed))
            {
                if (retryCount > 0)
                {
                    Logger.LogLine($"Test method {testMethod.TestClassName}.{testMethod.TestMethodName} failed. Retrying ({retryCount - 1} left).");
                    return this.ExecuteWithRetry(retryCount - 1, testMethod);
                }
            }

            return testResults;
        }
    }
}
