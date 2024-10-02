//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class IsFeedRangePartOfAsyncTests
    {
        private CosmosClient cosmosClient = null;
        private Cosmos.Database cosmosDatabase = null;
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public async Task TestInit()
        {
            this.cosmosClient = TestCommon.CreateCosmosClient();
            this.cosmosDatabase = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(id: Guid.NewGuid().ToString());

            await this.TestContext.SetContainerContextsAsync(
                cosmosDatabase: this.cosmosDatabase,
                createSinglePartitionContainerAsync: IsFeedRangePartOfAsyncTests.CreateSinglePartitionContainerAsync,
                createHierarchicalPartitionContainerAsync: IsFeedRangePartOfAsyncTests.CreateHierarchicalPartitionContainerAsync);
        }

        [TestCleanup]
        public async Task TestCleanup()
        {
            if (this.cosmosClient == null)
            {
                return;
            }

            if (this.cosmosDatabase != null)
            {
                await this.cosmosDatabase.DeleteStreamAsync();
            }

            this.cosmosClient.Dispose();
        }

        private async static Task<ContainerInternal> CreateSinglePartitionContainerAsync(Database cosmosDatabase, PartitionKeyDefinitionVersion version)
        {
            ContainerResponse containerResponse = await cosmosDatabase.CreateContainerIfNotExistsAsync(
                new()
                {
                    PartitionKeyDefinitionVersion = version,
                    Id = Guid.NewGuid().ToString(),
                    PartitionKeyPaths = new Collection<string> { "/pk" }
                });

            return (ContainerInternal)containerResponse.Container;
        }

        private async static Task<ContainerInternal> CreateHierarchicalPartitionContainerAsync(Database cosmosDatabase, PartitionKeyDefinitionVersion version)
        {
            ContainerResponse containerResponse = await cosmosDatabase.CreateContainerIfNotExistsAsync(
                new()
                {
                    PartitionKeyDefinitionVersion = version,
                    Id = Guid.NewGuid().ToString(),
                    PartitionKeyPaths = new Collection<string> { "/pk", "/id" }
                });

            return (ContainerInternal)containerResponse.Container;
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation
        /// 
        /// Scenario Outline: Validate if the child partition key is part of the parent feed range
        ///   Given the parent feed range with a specific PartitionKeyDefinitionVersion (V1 or V2)
        ///   And a child partition key
        ///   When the child partition key is compared to the parent feed range
        ///   Then determine whether the child partition key is part of the parent feed range
        /// ]]>
        /// </summary>
        /// <param name="parentMinimum">The starting value of the parent feed range.</param>
        /// <param name="parentMaximum">The ending value of the parent feed range.</param>
        /// <param name="expectedIsFeedRangePartOfAsync">Indicates whether the child partition key is expected to be part of the parent feed range (true if it is, false if it is not).</param>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [DataRow("", "FFFFFFFFFFFFFFFF", true, DisplayName = "Full range is subset")]
        [DataRow("3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, DisplayName = "Range 3FFFFFFFFFFFFFFF-7FFFFFFFFFFFFFFF is not subset")]
        [DataRow("", "FFFFFFFFFFFFFFFF", true, DisplayName = "Full range is subset using V2 hash testContext")]
        [DataRow("3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, DisplayName = "Range 3FFFFFFFFFFFFFFF-7FFFFFFFFFFFFFFF is not subset")]
        [Description("Validate if the child partition key is part of the parent feed range using either V1 or V2 PartitionKeyDefinitionVersion.")]
        public async Task GivenFeedRangeChildPartitionKeyIsPartOfParentFeedRange(
            string parentMinimum,
            string parentMaximum,
            bool expectedIsFeedRangePartOfAsync)
        {
            try
            {
                PartitionKey partitionKey = new("WA");
                FeedRange feedRange = FeedRange.FromPartitionKey(partitionKey);
                if (!this.TestContext.TryGetContainerContexts(out List<ContainerContext> containerContexts))
                {
                    this.TestContext.WriteLine("ContainerContexts do not exist in TestContext.Properties.");
                }

                ConcurrentBag<Exception> exceptions = new();
                object lockObject = new();

                IEnumerable<Task> tasks = containerContexts
                    .Where(context => !context.IsHierarchicalPartition)
                    .Select(async containerContext =>
                    {
                        this.TestContext.LogTestExecutionForContainer(containerContext);

                        bool actualIsFeedRangePartOfAsync = await containerContext.Container.IsFeedRangePartOfAsync(
                            new FeedRangeEpk(new Documents.Routing.Range<string>(parentMinimum, parentMaximum, true, false)),
                            feedRange,
                            cancellationToken: CancellationToken.None);

                        if (actualIsFeedRangePartOfAsync != expectedIsFeedRangePartOfAsync)
                        {
                            lock (lockObject)
                            {
                                exceptions.Add(
                                    new Exception(
                                        string.Format(
                                            TestContextExtensions.FeedRangeComparisonFailure,
                                            containerContext.Container.Id,
                                            containerContext.Version,
                                            containerContext.IsHierarchicalPartition ? "Hierarchical Partitioning" : "Single Partitioning",
                                            expectedIsFeedRangePartOfAsync,
                                            actualIsFeedRangePartOfAsync)));
                            }
                        }
                    });

                await Task.WhenAll(tasks);

                this.TestContext.HandleAggregatedExceptions(exceptions);
            }
            catch (Exception exception)
            {
                Assert.Fail(exception.Message);
            }
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation
        ///
        /// Scenario Outline: Validate if the child hierarchical partition key is part of the parent feed range
        ///   Given the parent feed range with a specific PartitionKeyDefinitionVersion (V1 or V2)
        ///   And a child hierarchical partition key
        ///   When the child hierarchical partition key is compared to the parent feed range
        ///   Then determine whether the child hierarchical partition key is part of the parent feed range
        /// ]]>
        /// </summary>
        /// <param name="parentMinimum">The starting value of the parent feed range.</param>
        /// <param name="parentMaximum">The ending value of the parent feed range.</param>
        /// <param name="expectedIsFeedRangePartOfAsync">A boolean value indicating whether the child hierarchical partition key is expected to be part of the parent feed range (true if it is, false if it is not).</param>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [DataRow("", "FFFFFFFFFFFFFFFF", true, DisplayName = "Full range")]
        [DataRow("3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, DisplayName = "Made-up range 3FFFFFFFFFFFFFFF-7FFFFFFFFFFFFFFF")]
        [Description("Validate if the child hierarchical partition key is part of the parent feed range.")]
        public async Task GivenFeedRangeChildHierarchicalPartitionKeyIsPartOfParentFeedRange(
            string parentMinimum,
            string parentMaximum,
            bool expectedIsFeedRangePartOfAsync)
        {
            try
            {
                PartitionKey partitionKey = new PartitionKeyBuilder()
                    .Add("WA")
                    .Add(Guid.NewGuid().ToString())
                    .Build();
                FeedRange feedRange = FeedRange.FromPartitionKey(partitionKey);
                if (!this.TestContext.TryGetContainerContexts(out List<ContainerContext> containerContexts))
                {
                    this.TestContext.WriteLine("ContainerContexts do not exist in TestContext.Properties.");
                }

                ConcurrentBag<Exception> exceptions = new();
                object lockObject = new();

                IEnumerable<Task> tasks = containerContexts
                    .Where(context => context.IsHierarchicalPartition)
                    .Select(async containerContext =>
                    {
                        this.TestContext.LogTestExecutionForContainer(containerContext);

                        bool actualIsFeedRangePartOfAsync = await containerContext.Container.IsFeedRangePartOfAsync(
                            new FeedRangeEpk(new Documents.Routing.Range<string>(parentMinimum, parentMaximum, true, false)),
                            feedRange,
                            cancellationToken: CancellationToken.None);

                        if (actualIsFeedRangePartOfAsync != expectedIsFeedRangePartOfAsync)
                        {
                            lock (lockObject)
                            {
                                exceptions.Add(
                                    new Exception(
                                        string.Format(
                                            TestContextExtensions.FeedRangeComparisonFailure,
                                            containerContext.Container.Id,
                                            containerContext.Version,
                                            containerContext.IsHierarchicalPartition ? "Hierarchical Partitioning" : "Single Partitioning",
                                            expectedIsFeedRangePartOfAsync,
                                            actualIsFeedRangePartOfAsync)));
                            }
                        }
                    });

                await Task.WhenAll(tasks);

                this.TestContext.HandleAggregatedExceptions(exceptions);
            }
            catch (Exception exception)
            {
                Assert.Fail(exception.Message);
            }
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation ArgumentNullException
        ///
        /// Scenario Outline: Validate that an ArgumentNullException is thrown when the child feed range is null
        ///   Given the parent feed range is defined with a specific PartitionKeyDefinitionVersion (V1 or V2)
        ///   And the child feed range is null
        ///   When the child feed range is compared to the parent feed range
        ///   Then an ArgumentNullException should be thrown
        /// ]]>
        /// </summary>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        public async Task GivenFeedRangeThrowsArgumentNullExceptionWhenChildFeedRangeIsNull()
        {
            FeedRange feedRange = default;

            await this.GivenInvalidChildFeedRangeExpectsArgumentExceptionIsFeedRangePartOfAsyncTestAsync<ArgumentNullException>(
                feedRange: feedRange,
                expectedMessage: $"Argument cannot be null.");
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation ArgumentNullException
        ///
        /// Scenario Outline: Validate that an ArgumentNullException is thrown when the child feed range has no JSON representation
        ///   Given the parent feed range is defined with a specific PartitionKeyDefinitionVersion (V1 or V2)
        ///   And the child feed range has no JSON representation
        ///   When the child feed range is compared to the parent feed range
        ///   Then an ArgumentNullException should be thrown
        /// ]]>
        /// </summary>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        public async Task GivenFeedRangeThrowsArgumentNullExceptionWhenChildFeedRangeHasNoJson()
        {
            FeedRange feedRange = Mock.Of<FeedRange>();

            await this.GivenInvalidChildFeedRangeExpectsArgumentExceptionIsFeedRangePartOfAsyncTestAsync<ArgumentNullException>(
                feedRange: feedRange,
                expectedMessage: $"Value cannot be null. (Parameter 'value')");
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation ArgumentException
        ///
        /// Scenario Outline: Validate that an ArgumentException is thrown when the child feed range has an invalid JSON representation
        ///   Given the parent feed range is defined with a specific PartitionKeyDefinitionVersion (V1 or V2)
        ///   And the child feed range has an invalid JSON representation
        ///   When the child feed range is compared to the parent feed range
        ///   Then an ArgumentException should be thrown
        /// ]]>
        /// </summary>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        public async Task GivenFeedRangeThrowsArgumentExceptionWhenChildFeedRangeHasInvalidJson()
        {
            Mock<FeedRange> mockFeedRange = new Mock<FeedRange>(MockBehavior.Strict);
            mockFeedRange.Setup(feedRange => feedRange.ToJsonString()).Returns("<xml />");
            FeedRange feedRange = mockFeedRange.Object;

            await this.GivenInvalidChildFeedRangeExpectsArgumentExceptionIsFeedRangePartOfAsyncTestAsync<ArgumentException>(
                feedRange: feedRange,
                expectedMessage: $"The provided string, '<xml />', for 'childFeedRange', does not represent any known format.");
        }

        private async Task GivenInvalidChildFeedRangeExpectsArgumentExceptionIsFeedRangePartOfAsyncTestAsync<TExceeption>(
            FeedRange feedRange,
            string expectedMessage)
            where TExceeption : Exception
        {
            try
            {
                if (!this.TestContext.TryGetContainerContexts(out List<ContainerContext> containerContexts))
                {
                    this.TestContext.WriteLine("ContainerContexts do not exist in TestContext.Properties.");
                }

                ConcurrentBag<Exception> exceptions = new();
                object lockObject = new();

                IEnumerable<Task> tasks = containerContexts
                    .Select(async containerContext =>
                    {
                        this.TestContext.LogTestExecutionForContainer(containerContext);

                        TExceeption exception = await Assert.ThrowsExceptionAsync<TExceeption>(
                            async () => await containerContext.Container.IsFeedRangePartOfAsync(
                                new FeedRangeEpk(new Documents.Routing.Range<string>("", "FFFFFFFFFFFFFFFF", true, false)),
                                feedRange,
                                cancellationToken: CancellationToken.None));

                        if (exception == null)
                        {
                            lock (lockObject)
                            {
                                exceptions.Add(new Exception("Failed: {testContext}. Expected exception was null."));
                            }
                        }
                        else if (!exception.Message.Contains(expectedMessage))
                        {
                            lock (lockObject)
                            {
                                exceptions.Add(
                                    new Exception(
                                        string.Format(
                                            TestContextExtensions.ExceptionMessageMismatch,
                                            containerContext.Container.Id,
                                            containerContext.Version,
                                            containerContext.IsHierarchicalPartition ? "Hierarchical Partitioning" : "Single Partitioning",
                                            expectedMessage,
                                            exception.Message)));
                            }
                        }
                    });

                await Task.WhenAll(tasks);

                this.TestContext.HandleAggregatedExceptions(exceptions);
            }
            catch (Exception exception)
            {
                Assert.Fail(exception.Message);
            }
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation ArgumentNullException
        ///
        /// Scenario Outline: Validate that an ArgumentNullException is thrown when the parent feed range is null
        ///   Given the parent feed range is null with a specific PartitionKeyDefinitionVersion (V1 or V2)
        ///   And the child feed range is defined
        ///   When the child feed range is compared to the parent feed range
        ///   Then an ArgumentNullException should be thrown
        /// ]]>
        /// </summary>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        public async Task GivenFeedRangeThrowsArgumentNullExceptionWhenParentFeedRangeIsNull()
        {
            FeedRange feedRange = default;

            await this.GivenInvalidParentFeedRangeExpectsArgumentExceptionIsFeedRangePartOfAsyncTestAsync<ArgumentNullException>(
                feedRange: feedRange,
                expectedMessage: $"Argument cannot be null.");
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation ArgumentNullException
        ///
        /// Scenario Outline: Validate that an ArgumentNullException is thrown when the parent feed range has no JSON representation
        ///   Given the parent feed range has no JSON representation with a specific PartitionKeyDefinitionVersion (V1 or V2)
        ///   And the child feed range is defined
        ///   When the child feed range is compared to the parent feed range
        ///   Then an ArgumentNullException should be thrown
        /// ]]>
        /// </summary>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        public async Task GivenFeedRangeThrowsArgumentNullExceptionWhenParentFeedRangeHasNoJson()
        {
            FeedRange feedRange = Mock.Of<FeedRange>();

            await this.GivenInvalidParentFeedRangeExpectsArgumentExceptionIsFeedRangePartOfAsyncTestAsync<ArgumentNullException>(
                feedRange: feedRange,
                expectedMessage: $"Value cannot be null. (Parameter 'value')");
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation ArgumentException
        ///
        /// Scenario Outline: Validate that an ArgumentException is thrown when the parent feed range has an invalid JSON representation
        ///   Given the parent feed range has an invalid JSON representation with a specific PartitionKeyDefinitionVersion (V1 or V2)
        ///   And the child feed range is defined
        ///   When the child feed range is compared to the parent feed range
        ///   Then an ArgumentException should be thrown
        /// ]]>
        /// </summary>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        public async Task GivenFeedRangeThrowsArgumentExceptionWhenParentFeedRangeHasInvalidJson()
        {
            Mock<FeedRange> mockFeedRange = new Mock<FeedRange>(MockBehavior.Strict);
            mockFeedRange.Setup(feedRange => feedRange.ToJsonString()).Returns("<xml />");
            FeedRange feedRange = mockFeedRange.Object;

            await this.GivenInvalidParentFeedRangeExpectsArgumentExceptionIsFeedRangePartOfAsyncTestAsync<ArgumentException>(
                feedRange: feedRange,
                expectedMessage: $"The provided string, '<xml />', for 'parentFeedRange', does not represent any known format.");
        }

        private async Task GivenInvalidParentFeedRangeExpectsArgumentExceptionIsFeedRangePartOfAsyncTestAsync<TException>(
            FeedRange feedRange,
            string expectedMessage)
            where TException : Exception
        {
            try
            {
                if (!this.TestContext.TryGetContainerContexts(out List<ContainerContext> containerContexts))
                {
                    this.TestContext.WriteLine("ContainerContexts do not exist in TestContext.Properties.");
                }

                ConcurrentBag<Exception> exceptions = new();
                object lockObject = new();

                IEnumerable<Task> tasks = containerContexts
                    .Select(async containerContext =>
                    {
                        this.TestContext.LogTestExecutionForContainer(containerContext);

                        TException exception = await Assert.ThrowsExceptionAsync<TException>(
                            async () => await containerContext.Container.IsFeedRangePartOfAsync(
                                feedRange,
                                new FeedRangeEpk(new Documents.Routing.Range<string>("", "3FFFFFFFFFFFFFFF", true, false)),
                                cancellationToken: CancellationToken.None));

                        if (exception == null)
                        {
                            lock (lockObject)
                            {
                                exceptions.Add(new Exception($"Failed: {containerContext}. Expected exception was null."));
                            }
                        }                        
                        else if (!exception.Message.Contains(expectedMessage))
                        {
                            lock (lockObject)
                            {
                                exceptions.Add(
                                    new Exception(
                                        string.Format(
                                            TestContextExtensions.ExceptionMessageMismatch,
                                            containerContext.Container.Id,
                                            containerContext.Version,
                                            containerContext.IsHierarchicalPartition ? "Hierarchical Partitioning" : "Single Partitioning",
                                            expectedMessage,
                                            exception.Message)));
                            }
                        }
                    });

                await Task.WhenAll(tasks);

                this.TestContext.HandleAggregatedExceptions(exceptions);
            }
            catch (Exception exception)
            {
                Assert.Fail(exception.Message);
            }
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation
        ///
        /// Scenario Outline: Child feed range is or is not part of the parent feed range when both the child's and parent's isMaxInclusive can be set to true or false
        ///   Given the parent feed range with isMaxInclusive set to true or false with a specific PartitionKeyDefinitionVersion (V1 or V2)
        ///   And the child feed range with isMaxInclusive set to true or false with the same PartitionKeyDefinitionVersion
        ///   When the child feed range is compared to the parent feed range
        ///   Then the child feed range is either part of or not part of the parent feed range
        /// ]]>
        /// </summary>
        /// <param name="childMinimum">The starting value of the child feed range.</param>
        /// <param name="childMaximum">The ending value of the child feed range.</param>
        /// <param name="childIsMaxInclusive">Specifies whether the maximum value of the child feed range is inclusive.</param>
        /// <param name="parentMinimum">The starting value of the parent feed range.</param>
        /// <param name="parentMaximum">The ending value of the parent feed range.</param>
        /// <param name="parentIsMaxInclusive">Specifies whether the maximum value of the parent feed range is inclusive.</param>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [DynamicData(nameof(IsFeedRangePartOfAsyncTests.FeedRangeThrowsNotSupportedExceptionWhenChildIsMaxExclusiveAndParentIsMaxInclusive), DynamicDataSourceType.Method)]
        public async Task GivenFeedRangeChildPartOfOrNotPartOfParentWhenBothIsMaxInclusiveCanBeTrueOrFalseNotSupportedExceptionTestAsync(
            string childMinimum,
            string childMaximum,
            bool childIsMaxInclusive,
            string parentMinimum,
            string parentMaximum,
            bool parentIsMaxInclusive)
        {
            try
            {
                if (!this.TestContext.TryGetContainerContexts(out List<ContainerContext> containerContexts))
                {
                    this.TestContext.WriteLine("ContainerContexts do not exist in TestContext.Properties.");
                }

                ConcurrentBag<Exception> exceptions = new();
                object lockObject = new();

                IEnumerable<Task> tasks = containerContexts
                    .Select(async containerContext =>
                    {
                        this.TestContext.LogTestExecutionForContainer(containerContext);

                        NotSupportedException exception = await Assert.ThrowsExceptionAsync<NotSupportedException>(
                            async () => 
                            await containerContext.Container.IsFeedRangePartOfAsync(
                                new FeedRangeEpk(new Documents.Routing.Range<string>(parentMinimum, parentMaximum, true, parentIsMaxInclusive)),
                                new FeedRangeEpk(new Documents.Routing.Range<string>(childMinimum, childMaximum, true, childIsMaxInclusive)),
                                cancellationToken: CancellationToken.None));

                        if (exception == null)
                        {
                            lock (lockObject)
                            {
                                exceptions.Add(new Exception($"Failed: {containerContext}. Expected exception was null."));
                            }
                        }
                    });

                // Await all tasks to complete
                await Task.WhenAll(tasks);

                // Handle the aggregated exceptions using the extension method
                this.TestContext.HandleAggregatedExceptions(exceptions);
            }
            catch (Exception exception)
            {
                Assert.Fail(exception.Message);
            }
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation
        ///
        /// Scenario Outline: Child feed range is or is not part of the parent feed range when both the child's and parent's isMaxInclusive can be set to true or false
        ///   Given the parent feed range with isMaxInclusive set to true or false with a specific PartitionKeyDefinitionVersion (V1 or V2)
        ///   And the child feed range with isMaxInclusive set to true or false with the same PartitionKeyDefinitionVersion
        ///   When the child feed range is compared to the parent feed range
        ///   Then the child feed range is either part of or not part of the parent feed range
        /// ]]>
        /// </summary>
        /// <param name="childMinimum">The starting value of the child feed range.</param>
        /// <param name="childMaximum">The ending value of the child feed range.</param>
        /// <param name="childIsMaxInclusive">Specifies whether the maximum value of the child feed range is inclusive.</param>
        /// <param name="parentMinimum">The starting value of the parent feed range.</param>
        /// <param name="parentMaximum">The ending value of the parent feed range.</param>
        /// <param name="parentIsMaxInclusive">Specifies whether the maximum value of the parent feed range is inclusive.</param>
        /// <param name="expectedIsFeedRangePartOfAsync">Indicates whether the child feed range is expected to be a subset of the parent feed range.</param>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [DynamicData(nameof(IsFeedRangePartOfAsyncTests.FeedRangeChildPartOfParentWhenBothChildAndParentIsMaxInclusiveTrue), DynamicDataSourceType.Method)]
        [DynamicData(nameof(IsFeedRangePartOfAsyncTests.FeedRangeChildNotPartOfParentWhenBothChildAndParentIsMaxInclusiveTrue), DynamicDataSourceType.Method)]
        [DynamicData(nameof(IsFeedRangePartOfAsyncTests.FeedRangeChildNotPartOfParentWhenBothIsMaxInclusiveAreFalse), DynamicDataSourceType.Method)]
        [DynamicData(nameof(IsFeedRangePartOfAsyncTests.FeedRangeChildNotPartOfParentWhenChildAndParentIsMaxInclusiveAreFalse), DynamicDataSourceType.Method)]
        [DynamicData(nameof(IsFeedRangePartOfAsyncTests.FeedRangeChildPartOfParentWhenChildIsMaxInclusiveTrueAndParentIsMaxInclusiveFalse), DynamicDataSourceType.Method)]
        [DynamicData(nameof(IsFeedRangePartOfAsyncTests.FeedRangeChildNotPartOfParentWhenChildIsMaxInclusiveTrueAndParentIsMaxInclusiveFalse), DynamicDataSourceType.Method)]
        public async Task GivenFeedRangeChildPartOfOrNotPartOfParentWhenBothIsMaxInclusiveCanBeTrueOrFalseTestAsync(
            string childMinimum,
            string childMaximum,
            bool childIsMaxInclusive,
            string parentMinimum,
            string parentMaximum,
            bool parentIsMaxInclusive,
            bool expectedIsFeedRangePartOfAsync)
        {
            try
            {
                if (!this.TestContext.TryGetContainerContexts(out List<ContainerContext> containerContexts))
                {
                    this.TestContext.WriteLine("ContainerContexts do not exist in TestContext.Properties.");
                }

                ConcurrentBag<Exception> exceptions = new();
                object lockObject = new();

                IEnumerable<Task> tasks = containerContexts
                    .Select(async containerContext =>
                    {
                        this.TestContext.LogTestExecutionForContainer(containerContext);

                        bool actualIsFeedRangePartOfAsync = await containerContext.Container.IsFeedRangePartOfAsync(
                            new FeedRangeEpk(new Documents.Routing.Range<string>(parentMinimum, parentMaximum, true, parentIsMaxInclusive)),
                            new FeedRangeEpk(new Documents.Routing.Range<string>(childMinimum, childMaximum, true, childIsMaxInclusive)),
                            cancellationToken: CancellationToken.None);

                        if (expectedIsFeedRangePartOfAsync != actualIsFeedRangePartOfAsync)
                        {
                            lock (lockObject)
                            {
                                exceptions.Add(
                                    new Exception(
                                        string.Format(
                                            TestContextExtensions.FeedRangeComparisonFailure,
                                                containerContext.Container.Id,
                                                containerContext.Version,
                                                containerContext.IsHierarchicalPartition ? "Hierarchical Partitioning" : "Single Partitioning",
                                                expectedIsFeedRangePartOfAsync,
                                                actualIsFeedRangePartOfAsync)));
                            }
                        }
                    });

                await Task.WhenAll(tasks);

                this.TestContext.HandleAggregatedExceptions(exceptions);
            }
            catch (Exception exception)
            {
                Assert.Fail(exception.Message);
            }
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation
        ///
        /// Scenario Outline: Child feed range is not part of the parent feed range with both isMaxInclusive set to false
        ///   Given the parent feed range with isMaxInclusive set to false
        ///   And the child feed range with isMaxInclusive set to false
        ///   When the child feed range is compared to the parent feed range
        ///   Then the child feed range is part of the parent feed range
        ///   
        /// Arguments: string childMinimum, string childMaximum, bool childIsMaxInclusive, string parentMinimum, string parentMaximum, bool parentIsMaxInclusive, bool expectedIsFeedRangePartOfAsync
        /// ]]>
        /// </summary>
        private static IEnumerable<object[]> FeedRangeChildNotPartOfParentWhenBothIsMaxInclusiveAreFalse()
        {
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", false, true }; // The child range, starting from a lower bound minimum and ending just before 3FFFFFFFFFFFFFFF, fits entirely within the parent range, which starts from a lower bound minimum and ends just before FFFFFFFFFFFFFFFF.
            yield return new object[] { "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", false, true }; // The child range, from 3FFFFFFFFFFFFFFF to just before 7FFFFFFFFFFFFFFF, fits entirely within the parent range, which starts from a lower bound minimum and ends just before FFFFFFFFFFFFFFFF.
            yield return new object[] { "7FFFFFFFFFFFFFFF", "BFFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", false, true }; // The child range, from 7FFFFFFFFFFFFFFF to just before BFFFFFFFFFFFFFFF, fits entirely within the parent range, which starts from a lower bound minimum and ends just before FFFFFFFFFFFFFFFF.
            yield return new object[] { "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", false, true }; // The child range, from BFFFFFFFFFFFFFFF to just before FFFFFFFFFFFFFFFF, does fit within the parent range, which starts from a lower bound minimum and ends just before FFFFFFFFFFFFFFFF.
            yield return new object[] { "3FFFFFFFFFFFFFFF", "4CCCCCCCCCCCCCCC", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true }; // The child range, from 3FFFFFFFFFFFFFFF to just before 4CCCCCCCCCCCCCCC, fits entirely within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "4CCCCCCCCCCCCCCC", "5999999999999999", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true }; // The child range, from 4CCCCCCCCCCCCCCC to just before 5999999999999999, fits entirely within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "5999999999999999", "6666666666666666", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true }; // The child range, from 5999999999999999 to just before 6666666666666666, fits entirely within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "6666666666666666", "7333333333333333", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true }; // The child range, from 6666666666666666 to just before 7333333333333333, fits entirely within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "7333333333333333", "7FFFFFFFFFFFFFFF", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true }; // The child range, from 7333333333333333 to just before 7FFFFFFFFFFFFFFF, does fit within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "", "3FFFFFFFFFFFFFFF", false, true }; // The child range, starting from a lower bound minimum and ending just before 3FFFFFFFFFFFFFFF, does not fit within the parent range, which starts from a lower bound minimum and ends just before 3FFFFFFFFFFFFFFF.
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation
        ///
        /// Scenario Outline: Child feed range is not part of the parent feed range with both child’s and parent’s isMaxInclusive set to false
        ///   Given the parent feed range with isMaxInclusive set to false
        ///   And the child feed range with isMaxInclusive set to false
        ///   When the child feed range is compared to the parent feed range
        ///   Then the child feed range is not part of the parent feed range
        /// ]]>
        /// </summary>
        private static IEnumerable<object[]> FeedRangeChildNotPartOfParentWhenChildAndParentIsMaxInclusiveAreFalse()
        {
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false }; // The child range ends just before 3FFFFFFFFFFFFFFF, but is not part of the parent range from 3FFFFFFFFFFFFFFF to 7FFFFFFFFFFFFFFF.
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "7FFFFFFFFFFFFFFF", "BFFFFFFFFFFFFFFF", false, false }; // The child range ends just before 3FFFFFFFFFFFFFFF, but is not part of the parent range from 7FFFFFFFFFFFFFFF to BFFFFFFFFFFFFFFF.
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", false, false }; // The child range ends just before 3FFFFFFFFFFFFFFF, but is not part of the parent range from BFFFFFFFFFFFFFFF to FFFFFFFFFFFFFFFF.
            yield return new object[] { "", "3333333333333333", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false }; // The child range ends just before 3333333333333333, but is not part of the parent range from 3FFFFFFFFFFFFFFF to 7FFFFFFFFFFFFFFF.
            yield return new object[] { "3333333333333333", "6666666666666666", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false }; // The child range from 3333333333333333 to just before 6666666666666666 is not part of the parent range from 3FFFFFFFFFFFFFFF to 7FFFFFFFFFFFFFFF.
            yield return new object[] { "7333333333333333", "FFFFFFFFFFFFFFFF", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false }; // The child range from 7333333333333333 to just before FFFFFFFFFFFFFFFF is not part of the parent range from 3FFFFFFFFFFFFFFF to 7FFFFFFFFFFFFFFF.
            yield return new object[] { "", "7333333333333333", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false }; // The child range ends just before 7333333333333333, but is not part of the parent range from 3FFFFFFFFFFFFFFF to 7FFFFFFFFFFFFFFF.
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation
        ///
        /// Scenario Outline: Child feed range is part of the parent feed range with the child’s isMaxInclusive set to true and the parent’s isMaxInclusive set to false
        ///   Given the parent feed range with isMaxInclusive set to false
        ///   And the child feed range with isMaxInclusive set to true
        ///   When the child feed range is compared to the parent feed range
        ///   Then the child feed range is part of the parent feed range
        /// ]]>
        /// </summary>
        private static IEnumerable<object[]> FeedRangeChildPartOfParentWhenChildIsMaxInclusiveTrueAndParentIsMaxInclusiveFalse()
        {
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "", "FFFFFFFFFFFFFFFF", false, true }; // The child range, starting from a lower bound minimum and ending at 3FFFFFFFFFFFFFFF (inclusive), fits within the parent range, which starts from a lower bound minimum and ends just before FFFFFFFFFFFFFFFF.
            yield return new object[] { "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, "", "FFFFFFFFFFFFFFFF", false, true }; // The child range, from 3FFFFFFFFFFFFFFF to 7FFFFFFFFFFFFFFF (inclusive), fits within the parent range, starting from a lower bound minimum and ending just before FFFFFFFFFFFFFFFF.
            yield return new object[] { "7FFFFFFFFFFFFFFF", "BFFFFFFFFFFFFFFF", true, "", "FFFFFFFFFFFFFFFF", false, true }; // The child range, from 7FFFFFFFFFFFFFFF to BFFFFFFFFFFFFFFF (inclusive), fits within the parent range, starting from a lower bound minimum and ending just before FFFFFFFFFFFFFFFF.
            yield return new object[] { "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", true, "", "FFFFFFFFFFFFFFFF", false, false }; // "The child range, from BFFFFFFFFFFFFFFF to FFFFFFFFFFFFFFFF (inclusive), does not fit within the parent range, which starts from a lower bound minimum and ends just before FFFFFFFFFFFFFFFF.
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "", "3FFFFFFFFFFFFFFF", false, false }; // The child range, from a lower bound minimum to 3FFFFFFFFFFFFFFF (inclusive), does not fit within the parent range, which starts from a lower bound minimum and ends just before 3FFFFFFFFFFFFFFF.
            yield return new object[] { "3FFFFFFFFFFFFFFF", "4CCCCCCCCCCCCCCC", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true }; // The child range, from 3FFFFFFFFFFFFFFF to 4CCCCCCCCCCCCCCC (inclusive), fits within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "4CCCCCCCCCCCCCCC", "5999999999999999", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true }; // The child range, from 4CCCCCCCCCCCCCCC to 5999999999999999 (inclusive), fits within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "5999999999999999", "6666666666666666", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true }; // The child range, from 5999999999999999 to 6666666666666666 (inclusive), fits within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "6666666666666666", "7333333333333333", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true }; // The child range, from 6666666666666666 to 7333333333333333 (inclusive), fits within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "7333333333333333", "7FFFFFFFFFFFFFFF", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false }; // The child range, from 7333333333333333 to 7FFFFFFFFFFFFFFF (inclusive), does not fit within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation
        ///
        /// Scenario Outline: Child feed range is not part of the parent feed range with the child’s isMaxInclusive set to true and the parent’s isMaxInclusive set to false
        ///   Given the parent feed range with isMaxInclusive set to false
        ///   And the child feed range with isMaxInclusive set to true
        ///   When the child feed range is compared to the parent feed range
        ///   Then the child feed range is not part of the parent feed range
        /// ]]>
        /// </summary>
        private static IEnumerable<object[]> FeedRangeChildNotPartOfParentWhenChildIsMaxInclusiveTrueAndParentIsMaxInclusiveFalse()
        {
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false }; // The child range, starting from a lower bound minimum and ending at 3FFFFFFFFFFFFFFF (inclusive), does not fit within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "7FFFFFFFFFFFFFFF", "BFFFFFFFFFFFFFFF", false, false }; // The child range, starting from a lower bound minimum and ending at 3FFFFFFFFFFFFFFF (inclusive), does not fit within the parent range, which starts from 7FFFFFFFFFFFFFFF and ends just before BFFFFFFFFFFFFFFF.
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", false, false }; // The child range, starting from a lower bound minimum and ending at 3FFFFFFFFFFFFFFF (inclusive), does not fit within the parent range, which starts from BFFFFFFFFFFFFFFF and ends just before FFFFFFFFFFFFFFFF.
            yield return new object[] { "", "3333333333333333", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false }; // The child range, starting from a lower bound minimum and ending at 3333333333333333 (inclusive), does not fit within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "3333333333333333", "6666666666666666", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false }; // The child range, from 3333333333333333 to 6666666666666666 (inclusive), does not fit within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "7333333333333333", "FFFFFFFFFFFFFFFF", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false }; // The child range, from 7333333333333333 to FFFFFFFFFFFFFFFF (inclusive), does not fit within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "", "7333333333333333", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false }; // The child range, starting from a lower bound minimum and ending at 7333333333333333 (inclusive), does not fit within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "AA", "AA", true, "", "AA", false, false }; // The child range, which starts and ends at AA (inclusive), does not fit within the parent range, which starts from a lower bound minimum and ends just before AA (non-inclusive), due to the parent's non-inclusive upper boundary.
            yield return new object[] { "AA", "AA", true, "AA", "BB", false, true }; // The child range, which starts and ends at AA (inclusive), fits entirely within the parent range, which starts at AA and ends just before BB (non-inclusive), due to the child's inclusive boundary at AA.
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation
        ///
        /// Scenario Outline: Child feed range is part of the parent feed range with the child’s isMaxInclusive set to false and the parent’s isMaxInclusive set to true
        ///   Given the parent feed range with isMaxInclusive set to true
        ///   And the child feed range with isMaxInclusive set to false
        ///   When the child feed range is compared to the parent feed range
        ///   Then the child feed range is part of the parent feed range
        /// ]]>
        /// </summary>
        private static IEnumerable<object[]> FeedRangeChildPartOfParentWhenChildIsMaxInclusiveFalseAndParentIsMaxInclusiveTrue()
        {
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", true }; // 
            yield return new object[] { "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", true }; // 
            yield return new object[] { "7FFFFFFFFFFFFFFF", "BFFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", true }; // 
            yield return new object[] { "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", true }; // 
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "", "3FFFFFFFFFFFFFFF", true }; // 
            yield return new object[] { "3FFFFFFFFFFFFFFF", "4CCCCCCCCCCCCCCC", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true }; // 
            yield return new object[] { "4CCCCCCCCCCCCCCC", "5999999999999999", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true }; // 
            yield return new object[] { "5999999999999999", "6666666666666666", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true }; // 
            yield return new object[] { "6666666666666666", "7333333333333333", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true}; // 
            yield return new object[] { "7333333333333333", "7FFFFFFFFFFFFFFF", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true }; // 
            yield return new object[] { "10", "11", false, "10", "10", true }; // 
            yield return new object[] { "A", "B", false, "A", "A", true }; // 
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: NotSupportedException for Feed Range Validation
        ///
        /// Scenario Outline: A NotSupportedException is thrown when the child feed range's isMaxInclusive is false and the parent feed range's isMaxInclusive is true.
        ///   Given the parent feed range with isMaxInclusive set to true
        ///   And the child feed range with isMaxInclusive set to false
        ///   When the child feed range is compared to the parent feed range
        ///   Then a NotSupportedException is thrown because this combination of inclusive/exclusive boundaries is not supported.
        /// ]]>
        /// </summary>
        private static IEnumerable<object[]> FeedRangeThrowsNotSupportedExceptionWhenChildIsMaxExclusiveAndParentIsMaxInclusive()
        {
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true }; // NotSupportedException thrown for child max exclusive and parent max inclusive (Range: '' to '3FFFFFFFFFFFFFFF' vs '3FFFFFFFFFFFFFFF' to '7FFFFFFFFFFFFFFF')
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "7FFFFFFFFFFFFFFF", "BFFFFFFFFFFFFFFF", true }; // NotSupportedException thrown for child max exclusive and parent max inclusive (Range: '' to '3FFFFFFFFFFFFFFF' vs '7FFFFFFFFFFFFFFF' to 'BFFFFFFFFFFFFFFF')
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", true }; // NotSupportedException thrown for child max exclusive and parent max inclusive (Range: '' to '3FFFFFFFFFFFFFFF' vs 'BFFFFFFFFFFFFFFF' to 'FFFFFFFFFFFFFFFF')
            yield return new object[] { "", "3333333333333333", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true }; // NotSupportedException thrown for child max exclusive and parent max inclusive (Range: '' to '3333333333333333' vs '3FFFFFFFFFFFFFFF' to '7FFFFFFFFFFFFFFF')
            yield return new object[] { "3333333333333333", "6666666666666666", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true }; // NotSupportedException thrown for child max exclusive and parent max inclusive (Range: '3333333333333333' to '6666666666666666' vs '3FFFFFFFFFFFFFFF' to '7FFFFFFFFFFFFFFF')
            yield return new object[] { "7333333333333333", "FFFFFFFFFFFFFFFF", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true }; // NotSupportedException thrown for child max exclusive and parent max inclusive (Range: '7333333333333333' to 'FFFFFFFFFFFFFFFF' vs '3FFFFFFFFFFFFFFF' to '7FFFFFFFFFFFFFFF')
            yield return new object[] { "", "7333333333333333", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true }; // NotSupportedException thrown for child max exclusive and parent max inclusive (Range: '' to '7333333333333333' vs '3FFFFFFFFFFFFFFF' to '7FFFFFFFFFFFFFFF')
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", true }; // NotSupportedException thrown for child max exclusive and parent max inclusive (Range: '' to '3FFFFFFFFFFFFFFF' vs '' to 'FFFFFFFFFFFFFFFF')
            yield return new object[] { "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", true }; // NotSupportedException thrown for child max exclusive and parent max inclusive (Range: '3FFFFFFFFFFFFFFF' to '7FFFFFFFFFFFFFFF' vs '' to 'FFFFFFFFFFFFFFFF')
            yield return new object[] { "7FFFFFFFFFFFFFFF", "BFFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", true }; // NotSupportedException thrown for child max exclusive and parent max inclusive (Range: '7FFFFFFFFFFFFFFF' to 'BFFFFFFFFFFFFFFF' vs '' to 'FFFFFFFFFFFFFFFF')
            yield return new object[] { "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", true }; // NotSupportedException thrown for child max exclusive and parent max inclusive (Range: 'BFFFFFFFFFFFFFFF' to 'FFFFFFFFFFFFFFFF' vs '' to 'FFFFFFFFFFFFFFFF')
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "", "3FFFFFFFFFFFFFFF", true }; // NotSupportedException thrown for child max exclusive and parent max inclusive (Range: '' to '3FFFFFFFFFFFFFFF' vs '' to '3FFFFFFFFFFFFFFF')
            yield return new object[] { "3FFFFFFFFFFFFFFF", "4CCCCCCCCCCCCCCC", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true }; // NotSupportedException thrown for child max exclusive and parent max inclusive (Range: '3FFFFFFFFFFFFFFF' to '4CCCCCCCCCCCCCCC' vs '3FFFFFFFFFFFFFFF' to '7FFFFFFFFFFFFFFF')
            yield return new object[] { "4CCCCCCCCCCCCCCC", "5999999999999999", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true }; // NotSupportedException thrown for child max exclusive and parent max inclusive (Range: '4CCCCCCCCCCCCCCC' to '5999999999999999' vs '3FFFFFFFFFFFFFFF' to '7FFFFFFFFFFFFFFF')
            yield return new object[] { "5999999999999999", "6666666666666666", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true }; // NotSupportedException thrown for child max exclusive and parent max inclusive (Range: '5999999999999999' to '6666666666666666' vs '3FFFFFFFFFFFFFFF' to '7FFFFFFFFFFFFFFF')
            yield return new object[] { "6666666666666666", "7333333333333333", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true }; // NotSupportedException thrown for child max exclusive and parent max inclusive (Range: '6666666666666666' to '7333333333333333' vs '3FFFFFFFFFFFFFFF' to '7FFFFFFFFFFFFFFF')
            yield return new object[] { "7333333333333333", "7FFFFFFFFFFFFFFF", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true }; // NotSupportedException thrown for child max exclusive and parent max inclusive (Range: '7333333333333333' to '7FFFFFFFFFFFFFFF' vs '3FFFFFFFFFFFFFFF' to '7FFFFFFFFFFFFFFF')
            yield return new object[] { "10", "11", false, "10", "10", true }; // NotSupportedException thrown for child max exclusive and parent max inclusive (Range: '10' to '11' vs '10' to '10')
            yield return new object[] { "A", "B", false, "A", "A", true }; // NotSupportedException thrown for child max exclusive and parent max inclusive (Range: 'A' to 'B' vs 'A' to 'A')
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation
        ///
        /// Scenario Outline: Child feed range is part of the parent feed range with both the child’s and parent’s isMaxInclusive set to true
        ///   Given the parent feed range with isMaxInclusive set to true
        ///   And the child feed range with isMaxInclusive set to true
        ///   When the child feed range is compared to the parent feed range
        ///   Then the child feed range is part of the parent feed range
        /// ]]>
        /// </summary>
        private static IEnumerable<object[]> FeedRangeChildPartOfParentWhenBothChildAndParentIsMaxInclusiveTrue()
        {
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "", "FFFFFFFFFFFFFFFF", true, true }; // The child range, starting from a lower bound minimum and ending at 3FFFFFFFFFFFFFFF (inclusive), fits entirely within the parent range, which starts from a lower bound minimum and ends at FFFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, "", "FFFFFFFFFFFFFFFF", true, true }; // The child range, from 3FFFFFFFFFFFFFFF to 7FFFFFFFFFFFFFFF (inclusive), fits entirely within the parent range, which starts from a lower bound minimum and ends at FFFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "7FFFFFFFFFFFFFFF", "BFFFFFFFFFFFFFFF", true, "", "FFFFFFFFFFFFFFFF", true, true }; // The child range, from 7FFFFFFFFFFFFFFF to BFFFFFFFFFFFFFFF (inclusive), fits entirely within the parent range, which starts from a lower bound minimum and ends at FFFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", true, "", "FFFFFFFFFFFFFFFF", true, true }; // The child range, from BFFFFFFFFFFFFFFF to FFFFFFFFFFFFFFFF (inclusive), fits entirely within the parent range, which starts from a lower bound minimum and ends at FFFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "", "3FFFFFFFFFFFFFFF", true, true }; // The child range, from a lower bound minimum to 3FFFFFFFFFFFFFFF (inclusive), fits entirely within the parent range, which starts from a lower bound minimum and ends at 3FFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "3FFFFFFFFFFFFFFF", "4CCCCCCCCCCCCCCC", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, true }; // The child range, from 3FFFFFFFFFFFFFFF to 4CCCCCCCCCCCCCCC (inclusive), fits entirely within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends at 7FFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "4CCCCCCCCCCCCCCC", "5999999999999999", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, true }; // The child range, from 4CCCCCCCCCCCCCCC to 5999999999999999 (inclusive), fits entirely within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends at 7FFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "5999999999999999", "6666666666666666", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, true }; // The child range, from 5999999999999999 to 6666666666666666 (inclusive), fits entirely within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends at 7FFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "6666666666666666", "7333333333333333", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, true }; // The child range, from 6666666666666666 to 7333333333333333 (inclusive), fits entirely within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends at 7FFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "7333333333333333", "7FFFFFFFFFFFFFFF", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, true }; // The child range, from 7333333333333333 to 7FFFFFFFFFFFFFFF (inclusive), fits entirely within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends at 7FFFFFFFFFFFFFFF (inclusive).
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation
        ///
        /// Scenario Outline: Child feed range is not part of the parent feed range with both the child’s and parent’s isMaxInclusive set to true
        ///   Given the parent feed range with isMaxInclusive set to true
        ///   And the child feed range with isMaxInclusive set to true
        ///   When the child feed range is compared to the parent feed range
        ///   Then the child feed range is not part of the parent feed range
        /// ]]>
        /// </summary>
        private static IEnumerable<object[]> FeedRangeChildNotPartOfParentWhenBothChildAndParentIsMaxInclusiveTrue()
        {
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, false }; // The child range, starting from a lower bound minimum and ending at 3FFFFFFFFFFFFFFF (inclusive), does not fit within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends at 7FFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "7FFFFFFFFFFFFFFF", "BFFFFFFFFFFFFFFF", true, false }; // The child range, starting from a lower bound minimum and ending at 3FFFFFFFFFFFFFFF (inclusive), does not fit within the parent range, which starts from 7FFFFFFFFFFFFFFF and ends at BFFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", true, false }; // The child range, starting from a lower bound minimum and ending at 3FFFFFFFFFFFFFFF (inclusive), does not fit within the parent range, which starts from BFFFFFFFFFFFFFFF and ends at FFFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "", "3333333333333333", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, false }; // The child range, starting from a lower bound minimum and ending at 3333333333333333 (inclusive), does not fit within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends at 7FFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "3333333333333333", "6666666666666666", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, false }; // The child range, from 3333333333333333 to 6666666666666666 (inclusive), does not fit within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends at 7FFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "7333333333333333", "FFFFFFFFFFFFFFFF", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, false }; // The child range, from 7333333333333333 to FFFFFFFFFFFFFFFF (inclusive), does not fit within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends at 7FFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "", "7333333333333333", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, false }; // The child range, starting from a lower bound minimum and ending at 7333333333333333 (inclusive), does not fit within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends at 7FFFFFFFFFFFFFFF (inclusive).
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation ArgumentOutOfRangeException
        ///
        /// Scenario Outline: Validate if an ArgumentOutOfRangeException is thrown when the child feed range is compared to the parent feed range with the parent's IsMinInclusive set to false
        ///   Given the parent feed range with IsMinInclusive set to false with a specific PartitionKeyDefinitionVersion (V1 or V2)
        ///   And the child feed range with a valid range
        ///   When the child feed range is compared to the parent feed range
        ///   Then an ArgumentOutOfRangeException should be thrown
        /// ]]>
        /// </summary>
        /// <param name="version">The version of the PartitionKeyDefinition (V1 or V2) used for the validation.</param>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        public async Task GivenFeedRangeThrowsArgumentOutOfRangeExceptionWhenChildComparedToParentWithParentIsMinInclusiveFalse()
        {
            await this.FeedRangeThrowsArgumentOutOfRangeExceptionWhenIsMinInclusiveFalse(
                parentFeedRange: new Documents.Routing.Range<string>("", "3FFFFFFFFFFFFFFF", false, true),
                childFeedRange: new Documents.Routing.Range<string>("", "FFFFFFFFFFFFFFFF", true, false));
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation ArgumentOutOfRangeException
        ///
        /// Scenario Outline: Validate if an ArgumentOutOfRangeException is thrown when the child feed range is compared to the parent feed range with the child's IsMinInclusive set to false
        ///   Given the parent feed range with IsMinInclusive set to false with a specific PartitionKeyDefinitionVersion (V1 or V2)
        ///   And the child feed range with a valid range
        ///   When the child feed range is compared to the parent feed range
        ///   Then an ArgumentOutOfRangeException should be thrown
        /// ]]>
        /// </summary>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        public async Task GivenFeedRangeThrowsArgumentOutOfRangeExceptionWhenChildComparedToParentWithChildIsMinInclusiveFalse()
        {
            await this.FeedRangeThrowsArgumentOutOfRangeExceptionWhenIsMinInclusiveFalse(
                parentFeedRange: new Documents.Routing.Range<string>("", "3FFFFFFFFFFFFFFF", true, false),
                childFeedRange: new Documents.Routing.Range<string>("", "FFFFFFFFFFFFFFFF", false, true));
        }

        private async Task FeedRangeThrowsArgumentOutOfRangeExceptionWhenIsMinInclusiveFalse(
            Documents.Routing.Range<string> parentFeedRange,
            Documents.Routing.Range<string> childFeedRange)
        {
            try
            {
                if (!this.TestContext.TryGetContainerContexts(out List<ContainerContext> containerContexts))
                {
                    this.TestContext.WriteLine("ContainerContexts do not exist in TestContext.Properties.");
                }

                ConcurrentBag<Exception> exceptions = new();
                object lockObject = new();

                IEnumerable<Task> tasks = containerContexts
                    .Select(async containerContext =>
                    {
                        this.TestContext.LogTestExecutionForContainer(containerContext);

                        ArgumentOutOfRangeException exception = await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(
                            async () => await containerContext.Container
                                .IsFeedRangePartOfAsync(
                                    new FeedRangeEpk(parentFeedRange),
                                    new FeedRangeEpk(childFeedRange),
                                    cancellationToken: CancellationToken.None));

                        if (exception == null)
                        {
                            lock (lockObject)
                            {
                                exceptions.Add(new Exception($"Failed: {containerContext}. Expected exception was null."));
                            }
                        }
                        else if (!exception.Message.Contains("IsMinInclusive must be true."))
                        {
                            lock (lockObject)
                            {
                                exceptions.Add(
                                    new Exception(
                                        string.Format(
                                            TestContextExtensions.IsMinInclusiveExceptionMismatch,
                                            containerContext.Container.Id,
                                            containerContext.Version,
                                            containerContext.IsHierarchicalPartition ? "Hierarchical Partitioning" : "Single Partitioning",
                                            exception.Message)));
                            }
                        }
                    });

                await Task.WhenAll(tasks);

                this.TestContext.HandleAggregatedExceptions(exceptions);
            }
            catch (Exception exception)
            {
                Assert.Fail(exception.Message);
            }
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Feed Range Subset Verification
        ///
        ///   Scenario Outline: Verify if the child feed range is a subset of the parent feed range
        ///     Given a parent feed range with min value <parentMinValue> and max value <parentMaxValue>
        ///       And the parent feed range is <parentIsMinInclusive> inclusive of its min value
        ///       And the parent feed range is <parentIsMaxInclusive> inclusive of its max value
        ///     When a child feed range with min value <childMinValue> and max value <childMaxValue> is compared
        ///       And the child feed range is <childIsMinInclusive> inclusive of its min value
        ///       And the child feed range is <childIsMaxInclusive> inclusive of its max value
        ///     Then the result should be <expectedIsSubset> indicating if the child is a subset of the parent
        /// ]]>
        /// </summary>
        /// <param name="parentIsMinInclusive">Indicates whether the parent range's minimum value is inclusive.</param>
        /// <param name="parentIsMaxInclusive">Indicates whether the parent range's maximum value is inclusive.</param>
        /// <param name="parentMinValue">The minimum value of the parent range.</param>
        /// <param name="parentMaxValue">The maximum value of the parent range.</param>
        /// <param name="childIsMinInclusive">Indicates whether the child range's minimum value is inclusive.</param>
        /// <param name="childIsMaxInclusive">Indicates whether the child range's maximum value is inclusive.</param>
        /// <param name="childMinValue">The minimum value of the child range.</param>
        /// <param name="childMaxValue">The maximum value of the child range.</param>
        /// <param name="expectedIsSubset">A boolean indicating whether the child feed range is expected to be a subset of the parent feed range. True if the child is a subset, false otherwise.</param>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [DataRow(true, true, "A", "Z", true, true, "A", "Z", true, DisplayName = "(true, true) Given both parent and child ranges (A to Z) are fully inclusive and equal, child is a subset")]
        [DataRow(true, true, "A", "A", true, true, "A", "A", true, DisplayName = "(true, true) Given both parent and child ranges (A to A) are fully inclusive and equal, and min and max range is the same, child is a subset")]
        [DataRow(true, true, "A", "A", true, true, "B", "B", false, DisplayName = "(true, true) Given both parent and child ranges are fully inclusive but min and max ranges are not the same (A to A, B to B), child is not a subset")]
        [DataRow(true, true, "B", "B", true, true, "A", "A", false, DisplayName = "(true, true) Given parent range (B to B) is fully inclusive and child range (A to A) is fully inclusive, child is not a subset")]
        [DataRow(true, false, "A", "Z", true, true, "A", "Y", true, DisplayName = "(false, true) Given parent range (A to Z) has an exclusive max and child range (A to Y) is fully inclusive, child is a subset")]
        [DataRow(true, false, "A", "Y", true, true, "A", "Z", false, DisplayName = "(false, true) Given parent range (A to Y) has an exclusive max but child range (A to Z) exceeds the parent’s max with an inclusive bound, child is not a subset")]
        [DataRow(true, false, "A", "Z", true, true, "A", "Z", false, DisplayName = "(false, true) Given parent range (A to Z) has an exclusive max and child range (A to Z) is fully inclusive, child is not a subset")]
        [DataRow(true, false, "A", "Y", true, false, "A", "Y", true, DisplayName = "(false, false) Given parent range (A to Y) is inclusive at min and exclusive at max, and child range (A to Y) is inclusive at min and exclusive at max, child is a subset")]
        [DataRow(true, false, "A", "W", true, false, "A", "Y", false, DisplayName = "(false, false) Given parent range (A to W) is inclusive at min and exclusive at max, and child range (A to Y) is inclusive at min and exclusive at max, child is not a subset")]
        [DataRow(true, false, "A", "Y", true, false, "A", "W", true, DisplayName = "(false, false) Given parent range (A to Y) is inclusive at min and exclusive at max, and child range (A to W) is inclusive at min and exclusive at max, child is a subset")]
        public void GivenParentRangeWhenChildRangeComparedThenValidateIfSubset(
            bool parentIsMinInclusive,
            bool parentIsMaxInclusive,
            string parentMinValue,
            string parentMaxValue,
            bool childIsMinInclusive,
            bool childIsMaxInclusive,
            string childMinValue,
            string childMaxValue,
            bool expectedIsSubset)
        {
            bool actualIsSubset = ContainerCore.IsSubset(
                new Documents.Routing.Range<string>(isMinInclusive: parentIsMinInclusive, isMaxInclusive: parentIsMaxInclusive, min: parentMinValue, max: parentMaxValue),
                new Documents.Routing.Range<string>(isMinInclusive: childIsMinInclusive, isMaxInclusive: childIsMaxInclusive, min: childMinValue, max: childMaxValue));

            Assert.AreEqual(
                expected: expectedIsSubset,
                actual: actualIsSubset);
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Feed Range Subset Verification NotSupportedException
        /// 
        /// Scenario Outline: Parent MaxInclusive, Child MaxExclusive (true/false)
        ///   Given a parent feed range with inclusive minimum and maximum values and a child feed range with an inclusive minimum and exclusive maximum, 
        ///   When the child feed range is compared against the parent, 
        ///   Then an exception is expected if the child's maximum value is exclusive and the parent's maximum value is inclusive.
        /// ]]>
        /// </summary>
        /// <param name="parentIsMinInclusive">Indicates whether the parent range's minimum value is inclusive.</param>
        /// <param name="parentIsMaxInclusive">Indicates whether the parent range's maximum value is inclusive.</param>
        /// <param name="parentMinValue">The minimum value of the parent range.</param>
        /// <param name="parentMaxValue">The maximum value of the parent range.</param>
        /// <param name="childIsMinInclusive">Indicates whether the child range's minimum value is inclusive.</param>
        /// <param name="childIsMaxInclusive">Indicates whether the child range's maximum value is inclusive.</param>
        /// <param name="childMinValue">The minimum value of the child range.</param>
        /// <param name="childMaxValue">The maximum value of the child range.</param>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [DataRow(true, true, "A", "Y", true, false, "A", "W", DisplayName = "(true, false) Given parent range (A to Y) is inclusive at min and max, and child range (A to W) is inclusive at min and exclusive at max, expects NotSupportedException")]
        [DataRow(true, true, "A", "Z", true, false, "A", "X", DisplayName = "(true, false) Given parent range (A to Z) is inclusive at min and max, and child range (A to X) is inclusive at min and exclusive at max, expects NotSupportedException")]
        [DataRow(true, true, "A", "Y", true, false, "A", "Y", DisplayName = "(true, false) Given parent range (A to Y) is inclusive at min and max, and child range (A to Y) is inclusive at min and exclusive at max, expects NotSupportedException")]
        public void GivenParentMaxInclusiveChildMaxExclusiveWhenCallingIsSubsetThenExpectNotSupportedExceptionIsThrown(
            bool parentIsMinInclusive,
            bool parentIsMaxInclusive,
            string parentMinValue,
            string parentMaxValue,
            bool childIsMinInclusive,
            bool childIsMaxInclusive,
            string childMinValue,
            string childMaxValue)
        {
            NotSupportedException exception = Assert.ThrowsException<NotSupportedException>(() => ContainerCore.IsSubset(
                parentRange: new Documents.Routing.Range<string>(min: parentMinValue, max: parentMaxValue, isMinInclusive: parentIsMinInclusive, isMaxInclusive: parentIsMaxInclusive),
                childRange: new Documents.Routing.Range<string>(min: childMinValue, max: childMaxValue, isMinInclusive: childIsMinInclusive, isMaxInclusive: childIsMaxInclusive)));

            Assert.IsNotNull(exception);
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Verify IsSubset method
        ///
        ///   Scenario Outline: Handle null parent range input
        ///     Given a null parent feed range
        ///     And a valid child feed range
        ///     When calling the IsSubset method
        ///     Then an ArgumentNullException is thrown
        /// ]]>
        /// </summary>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        public void GivenNullParentFeedRangeWhenCallingIsSubsetThenArgumentNullExceptionIsThrown()
        {
            ArgumentNullException exception = Assert.ThrowsException<ArgumentNullException>(() => ContainerCore.IsSubset(
                parentRange: null,
                childRange: new Documents.Routing.Range<string>(min: "A", max: "Z", isMinInclusive: true, isMaxInclusive: true)));

            Assert.IsNotNull(exception);
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Verify IsSubset method
        ///
        ///   Scenario Outline: Handle null child range input
        ///     Given a valid parent feed range
        ///     And a null child feed range
        ///     When calling the IsSubset method
        ///     Then an ArgumentNullException is thrown
        /// ]]>
        /// </summary>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        public void GivenNullChildFeedRangeWhenCallingIsSubsetThenArgumentNullExceptionIsThrown()
        {
            ArgumentNullException exception = Assert.ThrowsException<ArgumentNullException>(() => ContainerCore.IsSubset(
                parentRange: new Documents.Routing.Range<string>(min: "A", max: "Z", isMinInclusive: true, isMaxInclusive: true),
                childRange: null));

            Assert.IsNotNull(exception);
        }

        /// <summary>
        /// Validates if all ranges in the list have consistent inclusivity for both IsMinInclusive and IsMaxInclusive.
        /// Throws InvalidOperationException if any inconsistencies are found.
        ///
        /// <example>
        /// <![CDATA[
        /// Feature: Validate range inclusivity
        ///
        ///   Scenario Outline: All ranges are consistent
        ///     Given a list of ranges where all have the same IsMinInclusive and IsMaxInclusive values
        ///     When the inclusivity is validated
        ///     Then no exception is thrown
        ///
        ///   Scenario Outline: Inconsistent MinInclusive values
        ///     Given a list of ranges where IsMinInclusive values differ
        ///     When the inclusivity is validated
        ///     Then an InvalidOperationException is thrown
        ///
        ///   Scenario Outline: Inconsistent MaxInclusive values
        ///     Given a list of ranges where IsMaxInclusive values differ
        ///     When the inclusivity is validated
        ///     Then an InvalidOperationException is thrown
        /// ]]>
        /// </example>
        /// </summary>
        /// <param name="shouldNotThrow">Indicates if the test should pass without throwing an exception.</param>
        /// <param name="isMin1">IsMinInclusive value for first range.</param>
        /// <param name="isMax1">IsMaxInclusive value for first range.</param>
        /// <param name="isMin2">IsMinInclusive value for second range.</param>
        /// <param name="isMax2">IsMaxInclusive value for second range.</param>
        /// <param name="isMin3">IsMinInclusive value for third range.</param>
        /// <param name="isMax3">IsMaxInclusive value for third range.</param>
        /// <param name="expectedMessage">The expected exception message.></param>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [DataRow(true, true, false, true, false, true, false, "", DisplayName = "All ranges consistent")]
        [DataRow(false, true, false, false, false, true, false, "Not all 'IsMinInclusive' or 'IsMaxInclusive' values are the same. IsMinInclusive found: True, False, IsMaxInclusive found: False.", DisplayName = "Inconsistent MinInclusive")]
        [DataRow(false, true, false, true, true, true, false, "Not all 'IsMinInclusive' or 'IsMaxInclusive' values are the same. IsMinInclusive found: True, IsMaxInclusive found: False, True.", DisplayName = "Inconsistent MaxInclusive")]
        [DataRow(false, true, false, false, true, true, false, "Not all 'IsMinInclusive' or 'IsMaxInclusive' values are the same. IsMinInclusive found: True, False, IsMaxInclusive found: False, True.", DisplayName = "Inconsistent Min and Max Inclusive")]
        [DataRow(true, null, null, null, null, null, null, "", DisplayName = "Empty range list")]
        public void GivenListOfFeedRangesEnsureConsistentInclusivityValidatesRangesTest(
            bool shouldNotThrow,
            bool? isMin1,
            bool? isMax1,
            bool? isMin2,
            bool? isMax2,
            bool? isMin3,
            bool? isMax3,
            string expectedMessage)
        {
            List<Documents.Routing.Range<string>> ranges = new List<Documents.Routing.Range<string>>();

            if (isMin1.HasValue && isMax1.HasValue)
            {
                ranges.Add(new Documents.Routing.Range<string>(min: "A", max: "B", isMinInclusive: isMin1.Value, isMaxInclusive: isMax1.Value));
            }

            if (isMin2.HasValue && isMax2.HasValue)
            {
                ranges.Add(new Documents.Routing.Range<string>(min: "C", max: "D", isMinInclusive: isMin2.Value, isMaxInclusive: isMax2.Value));
            }

            if (isMin3.HasValue && isMax3.HasValue)
            {
                ranges.Add(new Documents.Routing.Range<string>(min: "E", max: "F", isMinInclusive: isMin3.Value, isMaxInclusive: isMax3.Value));
            }

            InvalidOperationException exception = default;

            if (!shouldNotThrow)
            {
                exception = Assert.ThrowsException<InvalidOperationException>(() => ContainerCore.EnsureConsistentInclusivity(ranges));

                Assert.IsNotNull(exception);
                Assert.AreEqual(expected: expectedMessage, actual: exception.Message);

                return;
            }

            Assert.IsNull(exception);
        }
    }

    internal record struct ContainerContext(
        ContainerInternal Container,
        PartitionKeyDefinitionVersion Version,
        bool IsHierarchicalPartition)
    {
        public override readonly string ToString()
        {
            return $"{{\"Container\": \"{this.Container.Id}\", \"Version\": \"{this.Version}\", \"IsHierarchicalPartition\": {this.IsHierarchicalPartition.ToString().ToLower()}}}";
        }
    }

    internal static class TestContextExtensions
    {
        public const string FeedRangeComparisonFailure = "Test failed for container '{0}' using Partition Key Definition Version '{1}' with {2}. Expected the feed range comparison result to be '{3}', but the actual result was '{4}'.";

        public const string ExceptionMessageMismatch = "Test failed for container '{0}' using Partition Key Definition Version '{1}' with {2}. Expected the exception message to contain '{3}', but the actual message was '{4}'.";

        public const string IsMinInclusiveExceptionMismatch = "Test failed for container '{0}' using Partition Key Definition Version '{1}' with {2}. Expected the exception message to contain 'IsMinInclusive must be true.', but the actual message was '{3}'.";

        /// <summary>
        /// Attempts to retrieve the list of <see cref="ContainerContext"/> objects stored in the <see cref="TestContext"/> properties.
        /// If the property "ContainerContexts" exists and is of type <see cref="List{ContainerContext}"/>, it is returned via the <paramref name="containerContexts"/> out parameter.
        /// Returns <c>true</c> if successful, <c>false</c> otherwise.
        /// </summary>
        /// <param name="testContext">The <see cref="TestContext"/> instance from which to attempt retrieving the container contexts.</param>
        /// <param name="containerContexts">When this method returns, contains the <see cref="List{ContainerContext}"/> if the retrieval was successful; otherwise, <c>null</c>.</param>
        /// <returns><c>true</c> if the retrieval was successful; otherwise, <c>false</c>.</returns>
        public static bool TryGetContainerContexts(this TestContext testContext, out List<ContainerContext> containerContexts)
        {
            if (testContext.Properties["ContainerContexts"] is List<ContainerContext> contexts)
            {
                containerContexts = contexts;
                return true;
            }
            else
            {
                containerContexts = null;
                return false;
            }
        }

        /// <summary>
        /// Logs exceptions, prints details, and throws an AssertFailedException with the aggregated exceptions.
        /// </summary>
        /// <param name="testContext">The <see cref="TestContext"/> instance from which to attempt retrieving the container contexts.</param>
        /// <param name="exceptions">A collection of exceptions to aggregate and log.</param>
        public static void HandleAggregatedExceptions(this TestContext testContext, ConcurrentBag<Exception> exceptions)
        {
            // Check if any exceptions were captured
            if (exceptions.Any())
            {
                // Aggregate the exceptions
                AggregateException aggregateException = new AggregateException(exceptions);

                // Log out the details of each inner exception
                foreach (Exception innerException in aggregateException.InnerExceptions)
                {
                    testContext.WriteLine($"Exception: {innerException.Message}");
                    testContext.WriteLine(innerException.StackTrace);
                }

                // Throw an AssertFailedException with the aggregated exceptions
                throw new AssertFailedException("One or more assertions failed. See inner exceptions for details.", aggregateException);
            }
        }

        // <summary>
        /// Asynchronously sets up the ContainerContexts property in the TestContext by creating containers with specified partition key definitions.
        /// </summary>
        /// <param name="testContext">The <see cref="TestContext"/> instance from which to attempt retrieving the container contexts.</param>
        /// <param name="cosmosDatabase">The Cosmos database used for creating the containers.</param>
        /// <param name="createSinglePartitionContainerAsync">A delegate function that creates a container with a single partition key definition version asynchronously.</param>
        /// <param name="createHierarchicalPartitionContainerAsync">A delegate function that creates a container with a hierarchical partition key definition version asynchronously.</param>
        /// <returns>A task representing the asynchronous operation, which sets up the ContainerContexts property in the TestContext.</returns>
        public static async Task SetContainerContextsAsync(
            this TestContext testContext,
            Database cosmosDatabase,
            Func<Database, PartitionKeyDefinitionVersion, Task<ContainerInternal>> createSinglePartitionContainerAsync,
            Func<Database, PartitionKeyDefinitionVersion, Task<ContainerInternal>> createHierarchicalPartitionContainerAsync)
        {
            testContext.Properties["ContainerContexts"] = new List<ContainerContext>()
            {
                new (await createSinglePartitionContainerAsync(cosmosDatabase, PartitionKeyDefinitionVersion.V1), PartitionKeyDefinitionVersion.V1, false),
                new (await createSinglePartitionContainerAsync(cosmosDatabase, PartitionKeyDefinitionVersion.V2), PartitionKeyDefinitionVersion.V2, false),
                new (await createHierarchicalPartitionContainerAsync(cosmosDatabase, PartitionKeyDefinitionVersion.V1), PartitionKeyDefinitionVersion.V1, true),
                new (await createHierarchicalPartitionContainerAsync(cosmosDatabase, PartitionKeyDefinitionVersion.V2), PartitionKeyDefinitionVersion.V2, true),
            };
        }

        /// <summary>
        /// Logs a message indicating the current container test being executed.
        /// </summary>
        /// <param name="testContext">The <see cref="TestContext"/> instance from which to attempt retrieving the container contexts.</param>
        /// <param name="containerContext">The container context that is being executed.</param>
        public static void LogTestExecutionForContainer(this TestContext testContext, ContainerContext containerContext)
        {
            string partitionType = containerContext.IsHierarchicalPartition ? "Hierarchical Partition" : "Single Partition";

            testContext.WriteLine($"Executing test for container with ID: '{containerContext.Container.Id}', " +
                $"Partition Key Definition Version: '{containerContext.Version}', " +
                $"{partitionType}.");
        }
    }
}
