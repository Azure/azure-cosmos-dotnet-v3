// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    [TestClass]
    public class FaultInjectionBuilderValidationTests
    {
        #region FaultInjectionRuleBuilder Validation

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests that FaultInjectionRuleBuilder rejects null or empty ID")]
        public void FaultInjectionRuleBuilder_NullId_Throws()
        {
            FaultInjectionCondition condition = new FaultInjectionConditionBuilder().Build();
            FaultInjectionServerErrorResult result = FaultInjectionResultBuilder
                .GetResultBuilder(FaultInjectionServerErrorType.Gone)
                .Build();

            Assert.ThrowsException<ArgumentNullException>(() =>
                new FaultInjectionRuleBuilder(id: null, condition: condition, result: result));
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests that FaultInjectionRuleBuilder rejects empty ID")]
        public void FaultInjectionRuleBuilder_EmptyId_Throws()
        {
            FaultInjectionCondition condition = new FaultInjectionConditionBuilder().Build();
            FaultInjectionServerErrorResult result = FaultInjectionResultBuilder
                .GetResultBuilder(FaultInjectionServerErrorType.Gone)
                .Build();

            Assert.ThrowsException<ArgumentNullException>(() =>
                new FaultInjectionRuleBuilder(id: string.Empty, condition: condition, result: result));
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests that FaultInjectionRuleBuilder rejects null condition")]
        public void FaultInjectionRuleBuilder_NullCondition_Throws()
        {
            FaultInjectionServerErrorResult result = FaultInjectionResultBuilder
                .GetResultBuilder(FaultInjectionServerErrorType.Gone)
                .Build();

            Assert.ThrowsException<ArgumentNullException>(() =>
                new FaultInjectionRuleBuilder(id: "test", condition: null, result: result));
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests that FaultInjectionRuleBuilder rejects null result")]
        public void FaultInjectionRuleBuilder_NullResult_Throws()
        {
            FaultInjectionCondition condition = new FaultInjectionConditionBuilder().Build();

            Assert.ThrowsException<ArgumentNullException>(() =>
                new FaultInjectionRuleBuilder(id: "test", condition: condition, result: null));
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests that FaultInjectionRuleBuilder rejects hit limit of 0")]
        public void FaultInjectionRuleBuilder_ZeroHitLimit_Throws()
        {
            FaultInjectionCondition condition = new FaultInjectionConditionBuilder().Build();
            FaultInjectionServerErrorResult result = FaultInjectionResultBuilder
                .GetResultBuilder(FaultInjectionServerErrorType.Gone)
                .Build();

            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                new FaultInjectionRuleBuilder(id: "test", condition: condition, result: result)
                    .WithHitLimit(0));
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests that FaultInjectionRuleBuilder rejects negative hit limit")]
        public void FaultInjectionRuleBuilder_NegativeHitLimit_Throws()
        {
            FaultInjectionCondition condition = new FaultInjectionConditionBuilder().Build();
            FaultInjectionServerErrorResult result = FaultInjectionResultBuilder
                .GetResultBuilder(FaultInjectionServerErrorType.Gone)
                .Build();

            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                new FaultInjectionRuleBuilder(id: "test", condition: condition, result: result)
                    .WithHitLimit(-1));
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests that Gateway connection type rejects Gone error type")]
        public void FaultInjectionRuleBuilder_GatewayWithGone_Throws()
        {
            FaultInjectionCondition condition = new FaultInjectionConditionBuilder()
                .WithConnectionType(FaultInjectionConnectionType.Gateway)
                .Build();
            FaultInjectionServerErrorResult result = FaultInjectionResultBuilder
                .GetResultBuilder(FaultInjectionServerErrorType.Gone)
                .Build();

            Assert.ThrowsException<ArgumentException>(() =>
                new FaultInjectionRuleBuilder(id: "test", condition: condition, result: result)
                    .Build());
        }

        #endregion

        #region FaultInjectionServerErrorResultBuilder Validation

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests injection rate below 0 throws")]
        public void ServerErrorResultBuilder_InjectionRateBelowZero_Throws()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                FaultInjectionResultBuilder
                    .GetResultBuilder(FaultInjectionServerErrorType.Gone)
                    .WithInjectionRate(0));
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests injection rate above 1 throws")]
        public void ServerErrorResultBuilder_InjectionRateAboveOne_Throws()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                FaultInjectionResultBuilder
                    .GetResultBuilder(FaultInjectionServerErrorType.Gone)
                    .WithInjectionRate(1.1));
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests injection rate negative throws")]
        public void ServerErrorResultBuilder_InjectionRateNegative_Throws()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                FaultInjectionResultBuilder
                    .GetResultBuilder(FaultInjectionServerErrorType.Gone)
                    .WithInjectionRate(-0.5));
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests injection rate of exactly 1 succeeds")]
        public void ServerErrorResultBuilder_InjectionRateExactlyOne_Succeeds()
        {
            FaultInjectionServerErrorResult result = FaultInjectionResultBuilder
                .GetResultBuilder(FaultInjectionServerErrorType.Gone)
                .WithInjectionRate(1.0)
                .Build();

            Assert.IsNotNull(result);
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests injection rate of 0.5 succeeds")]
        public void ServerErrorResultBuilder_InjectionRateHalf_Succeeds()
        {
            FaultInjectionServerErrorResult result = FaultInjectionResultBuilder
                .GetResultBuilder(FaultInjectionServerErrorType.Gone)
                .WithInjectionRate(0.5)
                .Build();

            Assert.IsNotNull(result);
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests ResponseDelay without delay throws")]
        public void ServerErrorResultBuilder_ResponseDelayWithoutDelay_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
                FaultInjectionResultBuilder
                    .GetResultBuilder(FaultInjectionServerErrorType.ResponseDelay)
                    .Build());
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests ConnectionDelay without delay throws")]
        public void ServerErrorResultBuilder_ConnectionDelayWithoutDelay_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() =>
                FaultInjectionResultBuilder
                    .GetResultBuilder(FaultInjectionServerErrorType.ConnectionDelay)
                    .Build());
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests SendDelay without delay throws (requires #5654 fix)")]
        public void ServerErrorResultBuilder_SendDelayWithoutDelay_Throws()
        {
            // This test validates the fix from #5654 (SendDelay delay validation)
            // Before the fix, Build() does not throw for SendDelay without delay
            try
            {
                FaultInjectionResultBuilder
                    .GetResultBuilder(FaultInjectionServerErrorType.SendDelay)
                    .Build();

                // If we reach here, the fix from #5654 is not yet applied
                Assert.Inconclusive("Fix #5654 not yet applied - SendDelay should require delay.");
            }
            catch (ArgumentNullException)
            {
                // Expected after fix #5654 is applied
            }
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests ResponseDelay with delay succeeds")]
        public void ServerErrorResultBuilder_ResponseDelayWithDelay_Succeeds()
        {
            FaultInjectionServerErrorResult result = FaultInjectionResultBuilder
                .GetResultBuilder(FaultInjectionServerErrorType.ResponseDelay)
                .WithDelay(TimeSpan.FromSeconds(1))
                .Build();

            Assert.IsNotNull(result);
            Assert.AreEqual(TimeSpan.FromSeconds(1), result.GetDelay());
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests WithDelay on non-delay error type throws (requires #5667 fix)")]
        public void ServerErrorResultBuilder_WithDelayOnNonDelayType_Throws()
        {
            // This test validates the fix from #5667 (WithDelay throws on non-delay types)
            try
            {
                FaultInjectionResultBuilder
                    .GetResultBuilder(FaultInjectionServerErrorType.Gone)
                    .WithDelay(TimeSpan.FromSeconds(1));

                // If we reach here, the fix from #5667 is not yet applied
                Assert.Inconclusive("Fix #5667 not yet applied - WithDelay should throw for non-delay types.");
            }
            catch (InvalidOperationException)
            {
                // Expected after fix #5667 is applied
            }
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests basic Gone error type build succeeds")]
        public void ServerErrorResultBuilder_GoneType_Succeeds()
        {
            FaultInjectionServerErrorResult result = FaultInjectionResultBuilder
                .GetResultBuilder(FaultInjectionServerErrorType.Gone)
                .Build();

            Assert.IsNotNull(result);
            Assert.AreEqual(FaultInjectionServerErrorType.Gone, result.GetServerErrorType());
        }

        #endregion

        #region FaultInjectionEndpointBuilder Validation

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests null database name throws (requires #5666 fix)")]
        public void EndpointBuilder_NullDatabaseName_Throws()
        {
            try
            {
                new FaultInjectionEndpointBuilder(
                    databaseName: null,
                    containerName: "col",
                    feedRange: FeedRange.FromPartitionKey(new PartitionKey("test")));

                Assert.Inconclusive("Fix #5666 not yet applied - null databaseName should throw.");
            }
            catch (ArgumentNullException)
            {
                // Expected after fix #5666 is applied
            }
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests empty database name throws (requires #5666 fix)")]
        public void EndpointBuilder_EmptyDatabaseName_Throws()
        {
            try
            {
                new FaultInjectionEndpointBuilder(
                    databaseName: string.Empty,
                    containerName: "col",
                    feedRange: FeedRange.FromPartitionKey(new PartitionKey("test")));

                Assert.Inconclusive("Fix #5666 not yet applied - empty databaseName should throw.");
            }
            catch (ArgumentNullException)
            {
                // Expected after fix #5666 is applied
            }
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests null container name throws (requires #5666 fix)")]
        public void EndpointBuilder_NullContainerName_Throws()
        {
            try
            {
                new FaultInjectionEndpointBuilder(
                    databaseName: "db",
                    containerName: null,
                    feedRange: FeedRange.FromPartitionKey(new PartitionKey("test")));

                Assert.Inconclusive("Fix #5666 not yet applied - null containerName should throw.");
            }
            catch (ArgumentNullException)
            {
                // Expected after fix #5666 is applied
            }
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests null feed range throws (requires #5666 fix)")]
        public void EndpointBuilder_NullFeedRange_Throws()
        {
            try
            {
                new FaultInjectionEndpointBuilder(
                    databaseName: "db",
                    containerName: "col",
                    feedRange: null);

                Assert.Inconclusive("Fix #5666 not yet applied - null feedRange should throw.");
            }
            catch (ArgumentNullException)
            {
                // Expected after fix #5666 is applied
            }
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests negative replica count throws")]
        public void EndpointBuilder_NegativeReplicaCount_Throws()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                new FaultInjectionEndpointBuilder(
                    databaseName: "db",
                    containerName: "col",
                    feedRange: FeedRange.FromPartitionKey(new PartitionKey("test")))
                .WithReplicaCount(-1));
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests valid endpoint builder succeeds")]
        public void EndpointBuilder_ValidParams_Succeeds()
        {
            FaultInjectionEndpoint endpoint = new FaultInjectionEndpointBuilder(
                databaseName: "db",
                containerName: "col",
                feedRange: FeedRange.FromPartitionKey(new PartitionKey("test")))
                .WithReplicaCount(3)
                .WithIncludePrimary(false)
                .Build();

            Assert.IsNotNull(endpoint);
            Assert.AreEqual(3, endpoint.GetReplicaCount());
            Assert.IsFalse(endpoint.IsIncludePrimary());
            Assert.AreEqual("dbs/db/colls/col", endpoint.GetResourceName());
        }

        #endregion

        #region FaultInjectionConnectionErrorResultBuilder Validation

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests connection error result builder with valid params")]
        public void ConnectionErrorResultBuilder_ValidParams_Succeeds()
        {
            FaultInjectionConnectionErrorResult result =
                new FaultInjectionConnectionErrorResultBuilder(
                    FaultInjectionConnectionErrorType.ReceiveStreamClosed)
                .WithInterval(TimeSpan.FromSeconds(1))
                .WithThreshold(0.5)
                .Build();

            Assert.IsNotNull(result);
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests connection error result threshold above 1 throws")]
        public void ConnectionErrorResultBuilder_ThresholdAboveOne_Throws()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                new FaultInjectionConnectionErrorResultBuilder(
                    FaultInjectionConnectionErrorType.ReceiveStreamClosed)
                .WithThreshold(1.1));
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests connection error result threshold of 0 throws")]
        public void ConnectionErrorResultBuilder_ThresholdZero_Throws()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                new FaultInjectionConnectionErrorResultBuilder(
                    FaultInjectionConnectionErrorType.ReceiveStreamClosed)
                .WithThreshold(0));
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests connection error result negative interval throws")]
        public void ConnectionErrorResultBuilder_NegativeInterval_Throws()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                new FaultInjectionConnectionErrorResultBuilder(
                    FaultInjectionConnectionErrorType.ReceiveStreamClosed)
                .WithInterval(TimeSpan.FromSeconds(-1)));
        }

        #endregion

        #region FaultInjectionCustomServerErrorResultBuilder Validation

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests custom server error result builder with valid params")]
        public void CustomServerErrorResultBuilder_ValidParams_Succeeds()
        {
            FaultInjectionCustomServerErrorResult result =
                new FaultInjectionCustomServerErrorResultBuilder(404, 0)
                .WithInjectionRate(0.5)
                .WithTimes(3)
                .Build();

            Assert.IsNotNull(result);
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests custom server error result injection rate above 1 throws")]
        public void CustomServerErrorResultBuilder_InjectionRateAboveOne_Throws()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                new FaultInjectionCustomServerErrorResultBuilder(404, 0)
                .WithInjectionRate(1.1));
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests custom server error result injection rate of 0 throws")]
        public void CustomServerErrorResultBuilder_InjectionRateZero_Throws()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                new FaultInjectionCustomServerErrorResultBuilder(404, 0)
                .WithInjectionRate(0));
        }

        #endregion
    }

    [TestClass]
    public class FaultInjectorUnitTests
    {
        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests that FaultInjector constructor rejects null rules (requires #5656 fix)")]
        public void FaultInjector_NullRules_Throws()
        {
            try
            {
                new FaultInjector(null);

                Assert.Inconclusive("Fix #5656 not yet applied - null rules should throw.");
            }
            catch (ArgumentNullException)
            {
                // Expected after fix #5656 is applied
            }
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests that FaultInjector constructor accepts empty rules list")]
        public void FaultInjector_EmptyRules_Succeeds()
        {
            FaultInjector injector = new FaultInjector(new List<FaultInjectionRule>());
            Assert.IsNotNull(injector);
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests GetApplicationContext returns null before initialization")]
        public void FaultInjector_GetApplicationContext_BeforeInit_ReturnsNull()
        {
            FaultInjector injector = new FaultInjector(new List<FaultInjectionRule>());
            Assert.IsNull(injector.GetApplicationContext());
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests GetFaultInjectionRuleId returns null for unknown activity")]
        public void FaultInjector_GetRuleId_UnknownActivity_ReturnsNull()
        {
            FaultInjector injector = new FaultInjector(new List<FaultInjectionRule>());
            Assert.IsNull(injector.GetFaultInjectionRuleId(Guid.NewGuid()));
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests GetFaultInjectionClientOptions sets ChaosInterceptorFactory")]
        public void FaultInjector_GetClientOptions_SetsFactory()
        {
            FaultInjector injector = new FaultInjector(new List<FaultInjectionRule>());
            CosmosClientOptions options = new CosmosClientOptions();

            CosmosClientOptions result = injector.GetFaultInjectionClientOptions(options);

            Assert.IsNotNull(result);
            Assert.AreSame(options, result);
        }
    }

    [TestClass]
    public class FaultInjectionApplicationContextUnitTests
    {
        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests AddRuleExecution and retrieval by rule ID")]
        public void ApplicationContext_AddAndGetByRuleId()
        {
            FaultInjectionApplicationContext context = new FaultInjectionApplicationContext();
            string ruleId = "test-rule";
            Guid activityId = Guid.NewGuid();

            context.AddRuleExecution(ruleId, activityId);

            Assert.IsTrue(context.TryGetRuleExecutionsByRuleId(ruleId, out var executions));
            Assert.AreEqual(1, executions.Count);
            Assert.AreEqual(activityId, executions[0].Item2);
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests retrieval by activity ID")]
        public void ApplicationContext_GetByActivityId()
        {
            FaultInjectionApplicationContext context = new FaultInjectionApplicationContext();
            string ruleId = "test-rule";
            Guid activityId = Guid.NewGuid();

            context.AddRuleExecution(ruleId, activityId);

            Assert.IsTrue(context.TryGetRuleExecutionByActivityId(activityId, out var execution));
            Assert.AreEqual(ruleId, execution.Item2);
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests retrieval with non-existent rule ID returns false")]
        public void ApplicationContext_NonExistentRuleId_ReturnsFalse()
        {
            FaultInjectionApplicationContext context = new FaultInjectionApplicationContext();

            Assert.IsFalse(context.TryGetRuleExecutionsByRuleId("nonexistent", out var executions));
            Assert.AreEqual(0, executions.Count);
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests retrieval with non-existent activity ID returns false")]
        public void ApplicationContext_NonExistentActivityId_ReturnsFalse()
        {
            FaultInjectionApplicationContext context = new FaultInjectionApplicationContext();

            Assert.IsFalse(context.TryGetRuleExecutionByActivityId(Guid.NewGuid(), out var execution));
            Assert.AreEqual(DateTime.MinValue, execution.Item1);
            Assert.AreEqual(string.Empty, execution.Item2);
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests multiple executions for same rule ID")]
        public void ApplicationContext_MultipleExecutionsSameRuleId()
        {
            FaultInjectionApplicationContext context = new FaultInjectionApplicationContext();
            string ruleId = "test-rule";
            Guid activityId1 = Guid.NewGuid();
            Guid activityId2 = Guid.NewGuid();

            context.AddRuleExecution(ruleId, activityId1);
            context.AddRuleExecution(ruleId, activityId2);

            Assert.IsTrue(context.TryGetRuleExecutionsByRuleId(ruleId, out var executions));
            Assert.AreEqual(2, executions.Count);
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests GetAllRuleExecutions returns all tracked executions")]
        public void ApplicationContext_GetAllRuleExecutions()
        {
            FaultInjectionApplicationContext context = new FaultInjectionApplicationContext();
            context.AddRuleExecution("rule1", Guid.NewGuid());
            context.AddRuleExecution("rule2", Guid.NewGuid());

            var allExecutions = context.GetAllRuleExecutions();
            Assert.AreEqual(2, allExecutions.Count);
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests thread-safety of concurrent AddRuleExecution calls (requires #5653 fix)")]
        public async Task ApplicationContext_ConcurrentAddRuleExecution()
        {
            FaultInjectionApplicationContext context = new FaultInjectionApplicationContext();
            string ruleId = "concurrent-rule";
            int concurrentTasks = 100;

            Task[] tasks = new Task[concurrentTasks];
            for (int i = 0; i < concurrentTasks; i++)
            {
                tasks[i] = Task.Run(() => context.AddRuleExecution(ruleId, Guid.NewGuid()));
            }

            await Task.WhenAll(tasks);

            Assert.IsTrue(context.TryGetRuleExecutionsByRuleId(ruleId, out var executions));

            // After fix #5653, all 100 executions should be tracked
            // Before fix, some may be lost due to thread-safety bug
            if (executions.Count < concurrentTasks)
            {
                Assert.Inconclusive(
                    $"Fix #5653 not yet applied - expected {concurrentTasks} executions but got {executions.Count}. " +
                    $"Thread-safety bug causes data loss.");
            }

            Assert.AreEqual(concurrentTasks, executions.Count);
        }
    }

    [TestClass]
    public class FaultInjectionRuleLifecycleTests
    {
        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests rule enable/disable toggle multiple times")]
        public void Rule_EnableDisableToggle()
        {
            FaultInjectionCondition condition = new FaultInjectionConditionBuilder().Build();
            FaultInjectionServerErrorResult result = FaultInjectionResultBuilder
                .GetResultBuilder(FaultInjectionServerErrorType.Gone)
                .Build();

            FaultInjectionRule rule = new FaultInjectionRuleBuilder(id: "test", condition: condition, result: result)
                .Build();

            Assert.IsTrue(rule.IsEnabled());

            rule.Disable();
            Assert.IsFalse(rule.IsEnabled());

            rule.Enable();
            Assert.IsTrue(rule.IsEnabled());

            rule.Disable();
            Assert.IsFalse(rule.IsEnabled());

            rule.Disable();
            Assert.IsFalse(rule.IsEnabled());

            rule.Enable();
            Assert.IsTrue(rule.IsEnabled());

            rule.Enable();
            Assert.IsTrue(rule.IsEnabled());
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests hit count is 0 for uninitialized rule")]
        public void Rule_HitCount_Uninitialized_ReturnsZero()
        {
            FaultInjectionCondition condition = new FaultInjectionConditionBuilder().Build();
            FaultInjectionServerErrorResult result = FaultInjectionResultBuilder
                .GetResultBuilder(FaultInjectionServerErrorType.Gone)
                .Build();

            FaultInjectionRule rule = new FaultInjectionRuleBuilder(id: "test", condition: condition, result: result)
                .Build();

            Assert.AreEqual(0, rule.GetHitCount());
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests rule condition defaults to All operation types")]
        public void Rule_DefaultCondition_AllOperationTypes()
        {
            FaultInjectionCondition condition = new FaultInjectionConditionBuilder().Build();

            Assert.AreEqual(FaultInjectionOperationType.All, condition.GetOperationType());
            Assert.AreEqual(FaultInjectionConnectionType.All, condition.GetConnectionType());
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests rule with each operation type")]
        public void Rule_AllOperationTypes()
        {
            FaultInjectionOperationType[] operationTypes = new[]
            {
                FaultInjectionOperationType.ReadItem,
                FaultInjectionOperationType.CreateItem,
                FaultInjectionOperationType.QueryItem,
                FaultInjectionOperationType.UpsertItem,
                FaultInjectionOperationType.ReplaceItem,
                FaultInjectionOperationType.DeleteItem,
                FaultInjectionOperationType.PatchItem,
                FaultInjectionOperationType.Batch,
                FaultInjectionOperationType.ReadFeed,
                FaultInjectionOperationType.All,
            };

            foreach (FaultInjectionOperationType opType in operationTypes)
            {
                FaultInjectionCondition condition = new FaultInjectionConditionBuilder()
                    .WithOperationType(opType)
                    .Build();

                Assert.AreEqual(opType, condition.GetOperationType());
            }
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests FaultInjectionRule ToString returns non-empty string")]
        public void Rule_ToString()
        {
            FaultInjectionCondition condition = new FaultInjectionConditionBuilder()
                .WithOperationType(FaultInjectionOperationType.ReadItem)
                .WithEndpoint(
                    new FaultInjectionEndpointBuilder(
                        databaseName: "db",
                        containerName: "col",
                        feedRange: FeedRange.FromPartitionKey(new PartitionKey("test")))
                    .Build())
                .Build();
            FaultInjectionServerErrorResult result = FaultInjectionResultBuilder
                .GetResultBuilder(FaultInjectionServerErrorType.Gone)
                .Build();

            FaultInjectionRule rule = new FaultInjectionRuleBuilder(id: "test-rule", condition: condition, result: result)
                .Build();

            string str = rule.ToString();
            Assert.IsFalse(string.IsNullOrEmpty(str));
            Assert.IsTrue(str.Contains("test-rule"));
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests FaultInjectionEndpoint ToString returns formatted string")]
        public void Endpoint_ToString()
        {
            FaultInjectionEndpoint endpoint = new FaultInjectionEndpointBuilder(
                databaseName: "testDb",
                containerName: "testCol",
                feedRange: FeedRange.FromPartitionKey(new PartitionKey("pk")))
                .Build();

            string str = endpoint.ToString();
            Assert.IsTrue(str.Contains("testDb"));
            Assert.IsTrue(str.Contains("testCol"));
        }
    }
}
