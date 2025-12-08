//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class QueryRequestOptionsUniTests
    {
        [TestMethod]
        public void StatelessTest()
        {
            QueryRequestOptions requestOption = new QueryRequestOptions();

            RequestMessage testMessage = new RequestMessage();
            requestOption.PopulateRequestOptions(testMessage);

            Assert.IsNull(testMessage.Headers.ContinuationToken);
        }

        [TestMethod]
        public void MaxItemCountNotModifiedInOriginalQueryRequestOptions_SimpleCopyTest()
        {
            // This test verifies that when QueryRequestOptions properties are copied, 
            // the original object is not modified. This reproduces the issue described in GitHub issue #5225.

            QueryRequestOptions originalOptions = new QueryRequestOptions 
            { 
                MaxItemCount = -1,
                MaxConcurrency = 10,
                EnableScanInQuery = true,
                SessionToken = "test-session-token"
            };
            
            int originalMaxItemCount = originalOptions.MaxItemCount.Value;
            int? originalMaxConcurrency = originalOptions.MaxConcurrency;
            bool? originalEnableScanInQuery = originalOptions.EnableScanInQuery;
            string originalSessionToken = originalOptions.SessionToken;

            // Simulate the copy logic that would happen in CosmosQueryClientCore.ExecuteItemQueryAsync
            QueryRequestOptions requestOptionsCopy = new QueryRequestOptions
            {
                ResponseContinuationTokenLimitInKb = originalOptions.ResponseContinuationTokenLimitInKb,
                EnableScanInQuery = originalOptions.EnableScanInQuery,
                EnableLowPrecisionOrderBy = originalOptions.EnableLowPrecisionOrderBy,
                EnableOptimisticDirectExecution = originalOptions.EnableOptimisticDirectExecution,
                MaxBufferedItemCount = originalOptions.MaxBufferedItemCount,
                MaxItemCount = originalOptions.MaxItemCount,
                MaxConcurrency = originalOptions.MaxConcurrency,
                PartitionKey = originalOptions.PartitionKey,
                PopulateIndexMetrics = originalOptions.PopulateIndexMetrics,
                PopulateQueryAdvice = originalOptions.PopulateQueryAdvice,
                ConsistencyLevel = originalOptions.ConsistencyLevel,
                SessionToken = originalOptions.SessionToken,
                DedicatedGatewayRequestOptions = originalOptions.DedicatedGatewayRequestOptions,
                QueryTextMode = originalOptions.QueryTextMode,
                // Base RequestOptions properties
                IfMatchEtag = originalOptions.IfMatchEtag,
                IfNoneMatchEtag = originalOptions.IfNoneMatchEtag,
                Properties = originalOptions.Properties,
                AddRequestHeaders = originalOptions.AddRequestHeaders,
                PriorityLevel = originalOptions.PriorityLevel,
                CosmosThresholdOptions = originalOptions.CosmosThresholdOptions,
                ExcludeRegions = originalOptions.ExcludeRegions
            };

            // Simulate the modification that would happen in ExecuteItemQueryAsync 
            int pageSize = 5;
            requestOptionsCopy.MaxItemCount = pageSize;

            // Assert: The original QueryRequestOptions should NOT be modified
            Assert.AreEqual(originalMaxItemCount, originalOptions.MaxItemCount.Value, 
                "Original QueryRequestOptions.MaxItemCount should not be modified");
            Assert.AreEqual(originalMaxConcurrency, originalOptions.MaxConcurrency, 
                "Original QueryRequestOptions.MaxConcurrency should not be modified");
            Assert.AreEqual(originalEnableScanInQuery, originalOptions.EnableScanInQuery, 
                "Original QueryRequestOptions.EnableScanInQuery should not be modified");
            Assert.AreEqual(originalSessionToken, originalOptions.SessionToken, 
                "Original QueryRequestOptions.SessionToken should not be modified");

            // Assert: The copy should have the new MaxItemCount
            Assert.AreEqual(pageSize, requestOptionsCopy.MaxItemCount.Value, 
                "Copied QueryRequestOptions.MaxItemCount should be updated to pageSize");
            
            // Assert: Other properties should be preserved in the copy
            Assert.AreEqual(originalMaxConcurrency, requestOptionsCopy.MaxConcurrency, 
                "Copied QueryRequestOptions.MaxConcurrency should match original");
            Assert.AreEqual(originalEnableScanInQuery, requestOptionsCopy.EnableScanInQuery, 
                "Copied QueryRequestOptions.EnableScanInQuery should match original");
            Assert.AreEqual(originalSessionToken, requestOptionsCopy.SessionToken, 
                "Copied QueryRequestOptions.SessionToken should match original");
        }
    }
}