//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System.Linq;
    using Microsoft.Azure.Cosmos.Services.Management.Tests;


    public sealed class TestClassAttribute : VisualStudio.TestTools.UnitTesting.TestClassAttribute
    {
        private readonly int maxRetry;

        /// <summary>
        /// Creates a TestClass that retries TestMethods once upon failure.
        /// </summary>
        public TestClassAttribute()
        {
            this.maxRetry = 1;
        }

        /// <summary>
        /// Creates a TestClass that retries TestMethods up to <paramref name="maxRetry"/> times.
        /// </summary>
        public TestClassAttribute(int maxRetry)
        {
            this.maxRetry = maxRetry;
        }

        public override VisualStudio.TestTools.UnitTesting.TestMethodAttribute GetTestMethodAttribute(VisualStudio.TestTools.UnitTesting.TestMethodAttribute testMethodAttribute)
        {
            VisualStudio.TestTools.UnitTesting.TestMethodAttribute baseTestmethod = base.GetTestMethodAttribute(testMethodAttribute);
            return new RetriableTestMethod(this.maxRetry, baseTestmethod);
        }
    }

    public class RetriableTestMethod : VisualStudio.TestTools.UnitTesting.TestMethodAttribute
    {
        private readonly VisualStudio.TestTools.UnitTesting.TestMethodAttribute testMethod;
        private int maxRetry;
        public RetriableTestMethod(
            int maxRetry,
            VisualStudio.TestTools.UnitTesting.TestMethodAttribute testMethod)
        {
            this.maxRetry = maxRetry;
            this.testMethod = testMethod;
        }

        public override VisualStudio.TestTools.UnitTesting.TestResult[] Execute(VisualStudio.TestTools.UnitTesting.ITestMethod testMethod) => this.ExecuteWithRetry(this.maxRetry, testMethod);

        private VisualStudio.TestTools.UnitTesting.TestResult[] ExecuteWithRetry(
            int retryCount,
            VisualStudio.TestTools.UnitTesting.ITestMethod testMethod)
        {
            VisualStudio.TestTools.UnitTesting.TestResult[] testResults = base.Execute(testMethod);
            if (testResults.Any((tr) => tr.Outcome == VisualStudio.TestTools.UnitTesting.UnitTestOutcome.Failed))
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
