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
        /// Scenario Outline: Validate if the y partition key is part of the x feed range
        ///   Given the x feed range with a specific PartitionKeyDefinitionVersion (V1 or V2)
        ///   And a y partition key
        ///   When the y partition key is compared to the x feed range
        ///   Then determine whether the y partition key is part of the x feed range
        /// ]]>
        /// </summary>
        /// <param name="xMinimum">The starting value of the x feed range.</param>
        /// <param name="xMaximum">The ending value of the x feed range.</param>
        /// <param name="expectedIsFeedRangePartOfAsync">Indicates whether the y partition key is expected to be part of the x feed range (true if it is, false if it is not).</param>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [DataRow("", "FFFFFFFFFFFFFFFF", true, DisplayName = "Full range is subset")]
        [DataRow("3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, DisplayName = "Range 3FFFFFFFFFFFFFFF-7FFFFFFFFFFFFFFF is not subset")]
        [DataRow("", "FFFFFFFFFFFFFFFF", true, DisplayName = "Full range is subset using V2 hash testContext")]
        [DataRow("3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, DisplayName = "Range 3FFFFFFFFFFFFFFF-7FFFFFFFFFFFFFFF is not subset")]
        [Description("Validate if the y partition key is part of the x feed range using either V1 or V2 PartitionKeyDefinitionVersion.")]
        public async Task GivenFeedRangeYPartitionKeyIsPartOfXFeedRange(
            string xMinimum,
            string xMaximum,
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
                            new FeedRangeEpk(new Documents.Routing.Range<string>(xMinimum, xMaximum, true, false)),
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
        /// Scenario Outline: Validate if the y hierarchical partition key is part of the x feed range
        ///   Given the x feed range with a specific PartitionKeyDefinitionVersion (V1 or V2)
        ///   And a y hierarchical partition key
        ///   When the y hierarchical partition key is compared to the x feed range
        ///   Then determine whether the y hierarchical partition key is part of the x feed range
        /// ]]>
        /// </summary>
        /// <param name="xMinimum">The starting value of the x feed range.</param>
        /// <param name="xMaximum">The ending value of the x feed range.</param>
        /// <param name="expectedIsFeedRangePartOfAsync">A boolean value indicating whether the y hierarchical partition key is expected to be part of the x feed range (true if it is, false if it is not).</param>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [DataRow("", "FFFFFFFFFFFFFFFF", true, DisplayName = "Full range")]
        [DataRow("3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, DisplayName = "Made-up range 3FFFFFFFFFFFFFFF-7FFFFFFFFFFFFFFF")]
        [Description("Validate if the y hierarchical partition key is part of the x feed range.")]
        public async Task GivenFeedRangeYHierarchicalPartitionKeyIsPartOfXFeedRange(
            string xMinimum,
            string xMaximum,
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
                            new FeedRangeEpk(new Documents.Routing.Range<string>(xMinimum, xMaximum, true, false)),
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
        /// Scenario Outline: Validate that an ArgumentNullException is thrown when the y feed range is null
        ///   Given the x feed range is defined with a specific PartitionKeyDefinitionVersion (V1 or V2)
        ///   And the y feed range is null
        ///   When the y feed range is compared to the x feed range
        ///   Then an ArgumentNullException should be thrown
        /// ]]>
        /// </summary>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        public async Task GivenFeedRangeThrowsArgumentNullExceptionWhenYFeedRangeIsNull()
        {
            FeedRange feedRange = default;

            await this.GivenInvalidYFeedRangeExpectsArgumentExceptionIsFeedRangePartOfAsyncTestAsync<ArgumentNullException>(
                feedRange: feedRange,
                expectedMessage: $"Argument cannot be null.");
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation ArgumentNullException
        ///
        /// Scenario Outline: Validate that an ArgumentNullException is thrown when the y feed range has no JSON representation
        ///   Given the x feed range is defined with a specific PartitionKeyDefinitionVersion (V1 or V2)
        ///   And the y feed range has no JSON representation
        ///   When the y feed range is compared to the x feed range
        ///   Then an ArgumentNullException should be thrown
        /// ]]>
        /// </summary>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        public async Task GivenFeedRangeThrowsArgumentNullExceptionWhenYFeedRangeHasNoJson()
        {
            FeedRange feedRange = Mock.Of<FeedRange>();

            await this.GivenInvalidYFeedRangeExpectsArgumentExceptionIsFeedRangePartOfAsyncTestAsync<ArgumentNullException>(
                feedRange: feedRange,
                expectedMessage: $"Value cannot be null. (Parameter 'value')");
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation ArgumentException
        ///
        /// Scenario Outline: Validate that an ArgumentException is thrown when the y feed range has an invalid JSON representation
        ///   Given the x feed range is defined with a specific PartitionKeyDefinitionVersion (V1 or V2)
        ///   And the y feed range has an invalid JSON representation
        ///   When the y feed range is compared to the x feed range
        ///   Then an ArgumentException should be thrown
        /// ]]>
        /// </summary>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        public async Task GivenFeedRangeThrowsArgumentExceptionWhenYFeedRangeHasInvalidJson()
        {
            Mock<FeedRange> mockFeedRange = new Mock<FeedRange>(MockBehavior.Strict);
            mockFeedRange.Setup(feedRange => feedRange.ToJsonString()).Returns("<xml />");
            FeedRange feedRange = mockFeedRange.Object;

            await this.GivenInvalidYFeedRangeExpectsArgumentExceptionIsFeedRangePartOfAsyncTestAsync<ArgumentException>(
                feedRange: feedRange,
                expectedMessage: $"The provided string, '<xml />', for 'y', does not represent any known format.");
        }

        private async Task GivenInvalidYFeedRangeExpectsArgumentExceptionIsFeedRangePartOfAsyncTestAsync<TExceeption>(
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
        /// Scenario Outline: Validate that an ArgumentNullException is thrown when the x feed range is null
        ///   Given the x feed range is null with a specific PartitionKeyDefinitionVersion (V1 or V2)
        ///   And the y feed range is defined
        ///   When the y feed range is compared to the x feed range
        ///   Then an ArgumentNullException should be thrown
        /// ]]>
        /// </summary>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        public async Task GivenFeedRangeThrowsArgumentNullExceptionWhenXFeedRangeIsNull()
        {
            FeedRange feedRange = default;

            await this.GivenInvalidXFeedRangeExpectsArgumentExceptionIsFeedRangePartOfAsyncTestAsync<ArgumentNullException>(
                feedRange: feedRange,
                expectedMessage: $"Argument cannot be null.");
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation ArgumentNullException
        ///
        /// Scenario Outline: Validate that an ArgumentNullException is thrown when the x feed range has no JSON representation
        ///   Given the x feed range has no JSON representation with a specific PartitionKeyDefinitionVersion (V1 or V2)
        ///   And the y feed range is defined
        ///   When the y feed range is compared to the x feed range
        ///   Then an ArgumentNullException should be thrown
        /// ]]>
        /// </summary>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        public async Task GivenFeedRangeThrowsArgumentNullExceptionWhenXFeedRangeHasNoJson()
        {
            FeedRange feedRange = Mock.Of<FeedRange>();

            await this.GivenInvalidXFeedRangeExpectsArgumentExceptionIsFeedRangePartOfAsyncTestAsync<ArgumentNullException>(
                feedRange: feedRange,
                expectedMessage: $"Value cannot be null. (Parameter 'value')");
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation ArgumentException
        ///
        /// Scenario Outline: Validate that an ArgumentException is thrown when the x feed range has an invalid JSON representation
        ///   Given the x feed range has an invalid JSON representation with a specific PartitionKeyDefinitionVersion (V1 or V2)
        ///   And the y feed range is defined
        ///   When the y feed range is compared to the x feed range
        ///   Then an ArgumentException should be thrown
        /// ]]>
        /// </summary>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        public async Task GivenFeedRangeThrowsArgumentExceptionWhenXFeedRangeHasInvalidJson()
        {
            Mock<FeedRange> mockFeedRange = new Mock<FeedRange>(MockBehavior.Strict);
            mockFeedRange.Setup(feedRange => feedRange.ToJsonString()).Returns("<xml />");
            FeedRange feedRange = mockFeedRange.Object;

            await this.GivenInvalidXFeedRangeExpectsArgumentExceptionIsFeedRangePartOfAsyncTestAsync<ArgumentException>(
                feedRange: feedRange,
                expectedMessage: $"The provided string, '<xml />', for 'x', does not represent any known format.");
        }

        private async Task GivenInvalidXFeedRangeExpectsArgumentExceptionIsFeedRangePartOfAsyncTestAsync<TException>(
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
        /// Scenario Outline: Y feed range is or is not part of the x feed range when both the y's and x's isMaxInclusive can be set to true or false
        ///   Given the x feed range with isMaxInclusive set to true or false with a specific PartitionKeyDefinitionVersion (V1 or V2)
        ///   And the y feed range with isMaxInclusive set to true or false with the same PartitionKeyDefinitionVersion
        ///   When the y feed range is compared to the x feed range
        ///   Then the y feed range is either part of or not part of the x feed range
        /// ]]>
        /// </summary>
        /// <param name="yMinimum">The starting value of the y feed range.</param>
        /// <param name="yMaximum">The ending value of the y feed range.</param>
        /// <param name="yIsMaxInclusive">Specifies whether the maximum value of the y feed range is inclusive.</param>
        /// <param name="xMinimum">The starting value of the x feed range.</param>
        /// <param name="xMaximum">The ending value of the x feed range.</param>
        /// <param name="xIsMaxInclusive">Specifies whether the maximum value of the x feed range is inclusive.</param>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [DynamicData(nameof(IsFeedRangePartOfAsyncTests.FeedRangeThrowsNotSupportedExceptionWhenYIsMaxExclusiveAndXIsMaxInclusive), DynamicDataSourceType.Method)]
        public async Task GivenFeedRangeYPartOfOrNotPartOfXWhenBothIsMaxInclusiveCanBeTrueOrFalseNotSupportedExceptionTestAsync(
            string yMinimum,
            string yMaximum,
            bool yIsMaxInclusive,
            string xMinimum,
            string xMaximum,
            bool xIsMaxInclusive)
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
                                new FeedRangeEpk(new Documents.Routing.Range<string>(xMinimum, xMaximum, true, xIsMaxInclusive)),
                                new FeedRangeEpk(new Documents.Routing.Range<string>(yMinimum, yMaximum, true, yIsMaxInclusive)),
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
        /// Scenario Outline: Y feed range is or is not part of the x feed range when both the y's and x's isMaxInclusive can be set to true or false
        ///   Given the x feed range with isMaxInclusive set to true or false with a specific PartitionKeyDefinitionVersion (V1 or V2)
        ///   And the y feed range with isMaxInclusive set to true or false with the same PartitionKeyDefinitionVersion
        ///   When the y feed range is compared to the x feed range
        ///   Then the y feed range is either part of or not part of the x feed range
        /// ]]>
        /// </summary>
        /// <param name="yMinimum">The starting value of the y feed range.</param>
        /// <param name="yMaximum">The ending value of the y feed range.</param>
        /// <param name="yIsMaxInclusive">Specifies whether the maximum value of the y feed range is inclusive.</param>
        /// <param name="xMinimum">The starting value of the x feed range.</param>
        /// <param name="xMaximum">The ending value of the x feed range.</param>
        /// <param name="xIsMaxInclusive">Specifies whether the maximum value of the x feed range is inclusive.</param>
        /// <param name="expectedIsFeedRangePartOfAsync">Indicates whether the y feed range is expected to be a subset of the x feed range.</param>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [DynamicData(nameof(IsFeedRangePartOfAsyncTests.FeedRangeYPartOfXWhenBothYAndXIsMaxInclusiveTrue), DynamicDataSourceType.Method)]
        [DynamicData(nameof(IsFeedRangePartOfAsyncTests.FeedRangeYNotPartOfXWhenBothYAndXIsMaxInclusiveTrue), DynamicDataSourceType.Method)]
        [DynamicData(nameof(IsFeedRangePartOfAsyncTests.FeedRangeYNotPartOfXWhenBothIsMaxInclusiveAreFalse), DynamicDataSourceType.Method)]
        [DynamicData(nameof(IsFeedRangePartOfAsyncTests.FeedRangeYNotPartOfXWhenYAndXIsMaxInclusiveAreFalse), DynamicDataSourceType.Method)]
        [DynamicData(nameof(IsFeedRangePartOfAsyncTests.FeedRangeYPartOfXWhenYIsMaxInclusiveTrueAndXIsMaxInclusiveFalse), DynamicDataSourceType.Method)]
        [DynamicData(nameof(IsFeedRangePartOfAsyncTests.FeedRangeYNotPartOfXWhenYIsMaxInclusiveTrueAndXIsMaxInclusiveFalse), DynamicDataSourceType.Method)]
        public async Task GivenFeedRangeYPartOfOrNotPartOfXWhenBothIsMaxInclusiveCanBeTrueOrFalseTestAsync(
            string yMinimum,
            string yMaximum,
            bool yIsMaxInclusive,
            string xMinimum,
            string xMaximum,
            bool xIsMaxInclusive,
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
                            new FeedRangeEpk(new Documents.Routing.Range<string>(xMinimum, xMaximum, true, xIsMaxInclusive)),
                            new FeedRangeEpk(new Documents.Routing.Range<string>(yMinimum, yMaximum, true, yIsMaxInclusive)),
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
        /// Scenario Outline: Y feed range is not part of the x feed range with both isMaxInclusive set to false
        ///   Given the x feed range with isMaxInclusive set to false
        ///   And the y feed range with isMaxInclusive set to false
        ///   When the y feed range is compared to the x feed range
        ///   Then the y feed range is part of the x feed range
        ///   
        /// Arguments: string yMinimum, string yMaximum, bool yIsMaxInclusive, string xMinimum, string xMaximum, bool xIsMaxInclusive, bool expectedIsFeedRangePartOfAsync
        /// ]]>
        /// </summary>
        private static IEnumerable<object[]> FeedRangeYNotPartOfXWhenBothIsMaxInclusiveAreFalse()
        {
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", false, true }; // The y range, starting from a lower bound minimum and ending just before 3FFFFFFFFFFFFFFF, fits entirely within the x range, which starts from a lower bound minimum and ends just before FFFFFFFFFFFFFFFF.
            yield return new object[] { "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", false, true }; // The y range, from 3FFFFFFFFFFFFFFF to just before 7FFFFFFFFFFFFFFF, fits entirely within the x range, which starts from a lower bound minimum and ends just before FFFFFFFFFFFFFFFF.
            yield return new object[] { "7FFFFFFFFFFFFFFF", "BFFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", false, true }; // The y range, from 7FFFFFFFFFFFFFFF to just before BFFFFFFFFFFFFFFF, fits entirely within the x range, which starts from a lower bound minimum and ends just before FFFFFFFFFFFFFFFF.
            yield return new object[] { "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", false, true }; // The y range, from BFFFFFFFFFFFFFFF to just before FFFFFFFFFFFFFFFF, does fit within the x range, which starts from a lower bound minimum and ends just before FFFFFFFFFFFFFFFF.
            yield return new object[] { "3FFFFFFFFFFFFFFF", "4CCCCCCCCCCCCCCC", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true }; // The y range, from 3FFFFFFFFFFFFFFF to just before 4CCCCCCCCCCCCCCC, fits entirely within the x range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "4CCCCCCCCCCCCCCC", "5999999999999999", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true }; // The y range, from 4CCCCCCCCCCCCCCC to just before 5999999999999999, fits entirely within the x range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "5999999999999999", "6666666666666666", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true }; // The y range, from 5999999999999999 to just before 6666666666666666, fits entirely within the x range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "6666666666666666", "7333333333333333", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true }; // The y range, from 6666666666666666 to just before 7333333333333333, fits entirely within the x range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "7333333333333333", "7FFFFFFFFFFFFFFF", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true }; // The y range, from 7333333333333333 to just before 7FFFFFFFFFFFFFFF, does fit within the x range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "", "3FFFFFFFFFFFFFFF", false, true }; // The y range, starting from a lower bound minimum and ending just before 3FFFFFFFFFFFFFFF, does not fit within the x range, which starts from a lower bound minimum and ends just before 3FFFFFFFFFFFFFFF.
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation
        ///
        /// Scenario Outline: Y feed range is not part of the x feed range with both y’s and x’s isMaxInclusive set to false
        ///   Given the x feed range with isMaxInclusive set to false
        ///   And the y feed range with isMaxInclusive set to false
        ///   When the y feed range is compared to the x feed range
        ///   Then the y feed range is not part of the x feed range
        /// ]]>
        /// </summary>
        private static IEnumerable<object[]> FeedRangeYNotPartOfXWhenYAndXIsMaxInclusiveAreFalse()
        {
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false }; // The y range ends just before 3FFFFFFFFFFFFFFF, but is not part of the x range from 3FFFFFFFFFFFFFFF to 7FFFFFFFFFFFFFFF.
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "7FFFFFFFFFFFFFFF", "BFFFFFFFFFFFFFFF", false, false }; // The y range ends just before 3FFFFFFFFFFFFFFF, but is not part of the x range from 7FFFFFFFFFFFFFFF to BFFFFFFFFFFFFFFF.
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", false, false }; // The y range ends just before 3FFFFFFFFFFFFFFF, but is not part of the x range from BFFFFFFFFFFFFFFF to FFFFFFFFFFFFFFFF.
            yield return new object[] { "", "3333333333333333", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false }; // The y range ends just before 3333333333333333, but is not part of the x range from 3FFFFFFFFFFFFFFF to 7FFFFFFFFFFFFFFF.
            yield return new object[] { "3333333333333333", "6666666666666666", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false }; // The y range from 3333333333333333 to just before 6666666666666666 is not part of the x range from 3FFFFFFFFFFFFFFF to 7FFFFFFFFFFFFFFF.
            yield return new object[] { "7333333333333333", "FFFFFFFFFFFFFFFF", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false }; // The y range from 7333333333333333 to just before FFFFFFFFFFFFFFFF is not part of the x range from 3FFFFFFFFFFFFFFF to 7FFFFFFFFFFFFFFF.
            yield return new object[] { "", "7333333333333333", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false }; // The y range ends just before 7333333333333333, but is not part of the x range from 3FFFFFFFFFFFFFFF to 7FFFFFFFFFFFFFFF.
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation
        ///
        /// Scenario Outline: Y feed range is part of the x feed range with the y’s isMaxInclusive set to true and the x’s isMaxInclusive set to false
        ///   Given the x feed range with isMaxInclusive set to false
        ///   And the y feed range with isMaxInclusive set to true
        ///   When the y feed range is compared to the x feed range
        ///   Then the y feed range is part of the x feed range
        /// ]]>
        /// </summary>
        private static IEnumerable<object[]> FeedRangeYPartOfXWhenYIsMaxInclusiveTrueAndXIsMaxInclusiveFalse()
        {
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "", "FFFFFFFFFFFFFFFF", false, true }; // The y range, starting from a lower bound minimum and ending at 3FFFFFFFFFFFFFFF (inclusive), fits within the x range, which starts from a lower bound minimum and ends just before FFFFFFFFFFFFFFFF.
            yield return new object[] { "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, "", "FFFFFFFFFFFFFFFF", false, true }; // The y range, from 3FFFFFFFFFFFFFFF to 7FFFFFFFFFFFFFFF (inclusive), fits within the x range, starting from a lower bound minimum and ending just before FFFFFFFFFFFFFFFF.
            yield return new object[] { "7FFFFFFFFFFFFFFF", "BFFFFFFFFFFFFFFF", true, "", "FFFFFFFFFFFFFFFF", false, true }; // The y range, from 7FFFFFFFFFFFFFFF to BFFFFFFFFFFFFFFF (inclusive), fits within the x range, starting from a lower bound minimum and ending just before FFFFFFFFFFFFFFFF.
            yield return new object[] { "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", true, "", "FFFFFFFFFFFFFFFF", false, false }; // "The y range, from BFFFFFFFFFFFFFFF to FFFFFFFFFFFFFFFF (inclusive), does not fit within the x range, which starts from a lower bound minimum and ends just before FFFFFFFFFFFFFFFF.
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "", "3FFFFFFFFFFFFFFF", false, false }; // The y range, from a lower bound minimum to 3FFFFFFFFFFFFFFF (inclusive), does not fit within the x range, which starts from a lower bound minimum and ends just before 3FFFFFFFFFFFFFFF.
            yield return new object[] { "3FFFFFFFFFFFFFFF", "4CCCCCCCCCCCCCCC", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true }; // The y range, from 3FFFFFFFFFFFFFFF to 4CCCCCCCCCCCCCCC (inclusive), fits within the x range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "4CCCCCCCCCCCCCCC", "5999999999999999", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true }; // The y range, from 4CCCCCCCCCCCCCCC to 5999999999999999 (inclusive), fits within the x range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "5999999999999999", "6666666666666666", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true }; // The y range, from 5999999999999999 to 6666666666666666 (inclusive), fits within the x range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "6666666666666666", "7333333333333333", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true }; // The y range, from 6666666666666666 to 7333333333333333 (inclusive), fits within the x range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "7333333333333333", "7FFFFFFFFFFFFFFF", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false }; // The y range, from 7333333333333333 to 7FFFFFFFFFFFFFFF (inclusive), does not fit within the x range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation
        ///
        /// Scenario Outline: Y feed range is not part of the x feed range with the y’s isMaxInclusive set to true and the x’s isMaxInclusive set to false
        ///   Given the x feed range with isMaxInclusive set to false
        ///   And the y feed range with isMaxInclusive set to true
        ///   When the y feed range is compared to the x feed range
        ///   Then the y feed range is not part of the x feed range
        /// ]]>
        /// </summary>
        private static IEnumerable<object[]> FeedRangeYNotPartOfXWhenYIsMaxInclusiveTrueAndXIsMaxInclusiveFalse()
        {
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false }; // The y range, starting from a lower bound minimum and ending at 3FFFFFFFFFFFFFFF (inclusive), does not fit within the x range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "7FFFFFFFFFFFFFFF", "BFFFFFFFFFFFFFFF", false, false }; // The y range, starting from a lower bound minimum and ending at 3FFFFFFFFFFFFFFF (inclusive), does not fit within the x range, which starts from 7FFFFFFFFFFFFFFF and ends just before BFFFFFFFFFFFFFFF.
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", false, false }; // The y range, starting from a lower bound minimum and ending at 3FFFFFFFFFFFFFFF (inclusive), does not fit within the x range, which starts from BFFFFFFFFFFFFFFF and ends just before FFFFFFFFFFFFFFFF.
            yield return new object[] { "", "3333333333333333", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false }; // The y range, starting from a lower bound minimum and ending at 3333333333333333 (inclusive), does not fit within the x range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "3333333333333333", "6666666666666666", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false }; // The y range, from 3333333333333333 to 6666666666666666 (inclusive), does not fit within the x range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "7333333333333333", "FFFFFFFFFFFFFFFF", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false }; // The y range, from 7333333333333333 to FFFFFFFFFFFFFFFF (inclusive), does not fit within the x range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "", "7333333333333333", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false }; // The y range, starting from a lower bound minimum and ending at 7333333333333333 (inclusive), does not fit within the x range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "AA", "AA", true, "", "AA", false, false }; // The y range, which starts and ends at AA (inclusive), does not fit within the x range, which starts from a lower bound minimum and ends just before AA (non-inclusive), due to the x's non-inclusive upper boundary.
            yield return new object[] { "AA", "AA", true, "AA", "BB", false, true }; // The y range, which starts and ends at AA (inclusive), fits entirely within the x range, which starts at AA and ends just before BB (non-inclusive), due to the y's inclusive boundary at AA.
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation
        ///
        /// Scenario Outline: Y feed range is part of the x feed range with the y’s isMaxInclusive set to false and the x’s isMaxInclusive set to true
        ///   Given the x feed range with isMaxInclusive set to true
        ///   And the y feed range with isMaxInclusive set to false
        ///   When the y feed range is compared to the x feed range
        ///   Then the y feed range is part of the x feed range
        /// ]]>
        /// </summary>
        private static IEnumerable<object[]> FeedRangeYPartOfXWhenYIsMaxInclusiveFalseAndXIsMaxInclusiveTrue()
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
        /// Scenario Outline: A NotSupportedException is thrown when the y feed range's isMaxInclusive is false and the x feed range's isMaxInclusive is true.
        ///   Given the x feed range with isMaxInclusive set to true
        ///   And the y feed range with isMaxInclusive set to false
        ///   When the y feed range is compared to the x feed range
        ///   Then a NotSupportedException is thrown because this combination of inclusive/exclusive boundaries is not supported.
        /// ]]>
        /// </summary>
        private static IEnumerable<object[]> FeedRangeThrowsNotSupportedExceptionWhenYIsMaxExclusiveAndXIsMaxInclusive()
        {
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true }; // NotSupportedException thrown for y max exclusive and x max inclusive (Range: '' to '3FFFFFFFFFFFFFFF' vs '3FFFFFFFFFFFFFFF' to '7FFFFFFFFFFFFFFF')
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "7FFFFFFFFFFFFFFF", "BFFFFFFFFFFFFFFF", true }; // NotSupportedException thrown for y max exclusive and x max inclusive (Range: '' to '3FFFFFFFFFFFFFFF' vs '7FFFFFFFFFFFFFFF' to 'BFFFFFFFFFFFFFFF')
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", true }; // NotSupportedException thrown for y max exclusive and x max inclusive (Range: '' to '3FFFFFFFFFFFFFFF' vs 'BFFFFFFFFFFFFFFF' to 'FFFFFFFFFFFFFFFF')
            yield return new object[] { "", "3333333333333333", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true }; // NotSupportedException thrown for y max exclusive and x max inclusive (Range: '' to '3333333333333333' vs '3FFFFFFFFFFFFFFF' to '7FFFFFFFFFFFFFFF')
            yield return new object[] { "3333333333333333", "6666666666666666", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true }; // NotSupportedException thrown for y max exclusive and x max inclusive (Range: '3333333333333333' to '6666666666666666' vs '3FFFFFFFFFFFFFFF' to '7FFFFFFFFFFFFFFF')
            yield return new object[] { "7333333333333333", "FFFFFFFFFFFFFFFF", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true }; // NotSupportedException thrown for y max exclusive and x max inclusive (Range: '7333333333333333' to 'FFFFFFFFFFFFFFFF' vs '3FFFFFFFFFFFFFFF' to '7FFFFFFFFFFFFFFF')
            yield return new object[] { "", "7333333333333333", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true }; // NotSupportedException thrown for y max exclusive and x max inclusive (Range: '' to '7333333333333333' vs '3FFFFFFFFFFFFFFF' to '7FFFFFFFFFFFFFFF')
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", true }; // NotSupportedException thrown for y max exclusive and x max inclusive (Range: '' to '3FFFFFFFFFFFFFFF' vs '' to 'FFFFFFFFFFFFFFFF')
            yield return new object[] { "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", true }; // NotSupportedException thrown for y max exclusive and x max inclusive (Range: '3FFFFFFFFFFFFFFF' to '7FFFFFFFFFFFFFFF' vs '' to 'FFFFFFFFFFFFFFFF')
            yield return new object[] { "7FFFFFFFFFFFFFFF", "BFFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", true }; // NotSupportedException thrown for y max exclusive and x max inclusive (Range: '7FFFFFFFFFFFFFFF' to 'BFFFFFFFFFFFFFFF' vs '' to 'FFFFFFFFFFFFFFFF')
            yield return new object[] { "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", true }; // NotSupportedException thrown for y max exclusive and x max inclusive (Range: 'BFFFFFFFFFFFFFFF' to 'FFFFFFFFFFFFFFFF' vs '' to 'FFFFFFFFFFFFFFFF')
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "", "3FFFFFFFFFFFFFFF", true }; // NotSupportedException thrown for y max exclusive and x max inclusive (Range: '' to '3FFFFFFFFFFFFFFF' vs '' to '3FFFFFFFFFFFFFFF')
            yield return new object[] { "3FFFFFFFFFFFFFFF", "4CCCCCCCCCCCCCCC", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true }; // NotSupportedException thrown for y max exclusive and x max inclusive (Range: '3FFFFFFFFFFFFFFF' to '4CCCCCCCCCCCCCCC' vs '3FFFFFFFFFFFFFFF' to '7FFFFFFFFFFFFFFF')
            yield return new object[] { "4CCCCCCCCCCCCCCC", "5999999999999999", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true }; // NotSupportedException thrown for y max exclusive and x max inclusive (Range: '4CCCCCCCCCCCCCCC' to '5999999999999999' vs '3FFFFFFFFFFFFFFF' to '7FFFFFFFFFFFFFFF')
            yield return new object[] { "5999999999999999", "6666666666666666", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true }; // NotSupportedException thrown for y max exclusive and x max inclusive (Range: '5999999999999999' to '6666666666666666' vs '3FFFFFFFFFFFFFFF' to '7FFFFFFFFFFFFFFF')
            yield return new object[] { "6666666666666666", "7333333333333333", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true }; // NotSupportedException thrown for y max exclusive and x max inclusive (Range: '6666666666666666' to '7333333333333333' vs '3FFFFFFFFFFFFFFF' to '7FFFFFFFFFFFFFFF')
            yield return new object[] { "7333333333333333", "7FFFFFFFFFFFFFFF", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true }; // NotSupportedException thrown for y max exclusive and x max inclusive (Range: '7333333333333333' to '7FFFFFFFFFFFFFFF' vs '3FFFFFFFFFFFFFFF' to '7FFFFFFFFFFFFFFF')
            yield return new object[] { "10", "11", false, "10", "10", true }; // NotSupportedException thrown for y max exclusive and x max inclusive (Range: '10' to '11' vs '10' to '10')
            yield return new object[] { "A", "B", false, "A", "A", true }; // NotSupportedException thrown for y max exclusive and x max inclusive (Range: 'A' to 'B' vs 'A' to 'A')
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation
        ///
        /// Scenario Outline: Y feed range is part of the x feed range with both the y’s and x’s isMaxInclusive set to true
        ///   Given the x feed range with isMaxInclusive set to true
        ///   And the y feed range with isMaxInclusive set to true
        ///   When the y feed range is compared to the x feed range
        ///   Then the y feed range is part of the x feed range
        /// ]]>
        /// </summary>
        private static IEnumerable<object[]> FeedRangeYPartOfXWhenBothYAndXIsMaxInclusiveTrue()
        {
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "", "FFFFFFFFFFFFFFFF", true, true }; // The y range, starting from a lower bound minimum and ending at 3FFFFFFFFFFFFFFF (inclusive), fits entirely within the x range, which starts from a lower bound minimum and ends at FFFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, "", "FFFFFFFFFFFFFFFF", true, true }; // The y range, from 3FFFFFFFFFFFFFFF to 7FFFFFFFFFFFFFFF (inclusive), fits entirely within the x range, which starts from a lower bound minimum and ends at FFFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "7FFFFFFFFFFFFFFF", "BFFFFFFFFFFFFFFF", true, "", "FFFFFFFFFFFFFFFF", true, true }; // The y range, from 7FFFFFFFFFFFFFFF to BFFFFFFFFFFFFFFF (inclusive), fits entirely within the x range, which starts from a lower bound minimum and ends at FFFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", true, "", "FFFFFFFFFFFFFFFF", true, true }; // The y range, from BFFFFFFFFFFFFFFF to FFFFFFFFFFFFFFFF (inclusive), fits entirely within the x range, which starts from a lower bound minimum and ends at FFFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "", "3FFFFFFFFFFFFFFF", true, true }; // The y range, from a lower bound minimum to 3FFFFFFFFFFFFFFF (inclusive), fits entirely within the x range, which starts from a lower bound minimum and ends at 3FFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "3FFFFFFFFFFFFFFF", "4CCCCCCCCCCCCCCC", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, true }; // The y range, from 3FFFFFFFFFFFFFFF to 4CCCCCCCCCCCCCCC (inclusive), fits entirely within the x range, which starts from 3FFFFFFFFFFFFFFF and ends at 7FFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "4CCCCCCCCCCCCCCC", "5999999999999999", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, true }; // The y range, from 4CCCCCCCCCCCCCCC to 5999999999999999 (inclusive), fits entirely within the x range, which starts from 3FFFFFFFFFFFFFFF and ends at 7FFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "5999999999999999", "6666666666666666", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, true }; // The y range, from 5999999999999999 to 6666666666666666 (inclusive), fits entirely within the x range, which starts from 3FFFFFFFFFFFFFFF and ends at 7FFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "6666666666666666", "7333333333333333", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, true }; // The y range, from 6666666666666666 to 7333333333333333 (inclusive), fits entirely within the x range, which starts from 3FFFFFFFFFFFFFFF and ends at 7FFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "7333333333333333", "7FFFFFFFFFFFFFFF", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, true }; // The y range, from 7333333333333333 to 7FFFFFFFFFFFFFFF (inclusive), fits entirely within the x range, which starts from 3FFFFFFFFFFFFFFF and ends at 7FFFFFFFFFFFFFFF (inclusive).
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation
        ///
        /// Scenario Outline: Y feed range is not part of the x feed range with both the y’s and x’s isMaxInclusive set to true
        ///   Given the x feed range with isMaxInclusive set to true
        ///   And the y feed range with isMaxInclusive set to true
        ///   When the y feed range is compared to the x feed range
        ///   Then the y feed range is not part of the x feed range
        /// ]]>
        /// </summary>
        private static IEnumerable<object[]> FeedRangeYNotPartOfXWhenBothYAndXIsMaxInclusiveTrue()
        {
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, false }; // The y range, starting from a lower bound minimum and ending at 3FFFFFFFFFFFFFFF (inclusive), does not fit within the x range, which starts from 3FFFFFFFFFFFFFFF and ends at 7FFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "7FFFFFFFFFFFFFFF", "BFFFFFFFFFFFFFFF", true, false }; // The y range, starting from a lower bound minimum and ending at 3FFFFFFFFFFFFFFF (inclusive), does not fit within the x range, which starts from 7FFFFFFFFFFFFFFF and ends at BFFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", true, false }; // The y range, starting from a lower bound minimum and ending at 3FFFFFFFFFFFFFFF (inclusive), does not fit within the x range, which starts from BFFFFFFFFFFFFFFF and ends at FFFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "", "3333333333333333", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, false }; // The y range, starting from a lower bound minimum and ending at 3333333333333333 (inclusive), does not fit within the x range, which starts from 3FFFFFFFFFFFFFFF and ends at 7FFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "3333333333333333", "6666666666666666", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, false }; // The y range, from 3333333333333333 to 6666666666666666 (inclusive), does not fit within the x range, which starts from 3FFFFFFFFFFFFFFF and ends at 7FFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "7333333333333333", "FFFFFFFFFFFFFFFF", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, false }; // The y range, from 7333333333333333 to FFFFFFFFFFFFFFFF (inclusive), does not fit within the x range, which starts from 3FFFFFFFFFFFFFFF and ends at 7FFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "", "7333333333333333", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, false }; // The y range, starting from a lower bound minimum and ending at 7333333333333333 (inclusive), does not fit within the x range, which starts from 3FFFFFFFFFFFFFFF and ends at 7FFFFFFFFFFFFFFF (inclusive).
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation ArgumentOutOfRangeException
        ///
        /// Scenario Outline: Validate if an ArgumentOutOfRangeException is thrown when the y feed range is compared to the x feed range with the x's IsMinInclusive set to false
        ///   Given the x feed range with IsMinInclusive set to false with a specific PartitionKeyDefinitionVersion (V1 or V2)
        ///   And the y feed range with a valid range
        ///   When the y feed range is compared to the x feed range
        ///   Then an ArgumentOutOfRangeException should be thrown
        /// ]]>
        /// </summary>
        /// <param name="version">The version of the PartitionKeyDefinition (V1 or V2) used for the validation.</param>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        public async Task GivenFeedRangeThrowsArgumentOutOfRangeExceptionWhenYComparedToXWithXIsMinInclusiveFalse()
        {
            await this.FeedRangeThrowsArgumentOutOfRangeExceptionWhenIsMinInclusiveFalse(
                xFeedRange: new Documents.Routing.Range<string>("", "3FFFFFFFFFFFFFFF", false, true),
                yFeedRange: new Documents.Routing.Range<string>("", "FFFFFFFFFFFFFFFF", true, false));
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation ArgumentOutOfRangeException
        ///
        /// Scenario Outline: Validate if an ArgumentOutOfRangeException is thrown when the y feed range is compared to the x feed range with the y's IsMinInclusive set to false
        ///   Given the x feed range with IsMinInclusive set to false with a specific PartitionKeyDefinitionVersion (V1 or V2)
        ///   And the y feed range with a valid range
        ///   When the y feed range is compared to the x feed range
        ///   Then an ArgumentOutOfRangeException should be thrown
        /// ]]>
        /// </summary>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        public async Task GivenFeedRangeThrowsArgumentOutOfRangeExceptionWhenYComparedToXWithYIsMinInclusiveFalse()
        {
            await this.FeedRangeThrowsArgumentOutOfRangeExceptionWhenIsMinInclusiveFalse(
                xFeedRange: new Documents.Routing.Range<string>("", "3FFFFFFFFFFFFFFF", true, false),
                yFeedRange: new Documents.Routing.Range<string>("", "FFFFFFFFFFFFFFFF", false, true));
        }

        private async Task FeedRangeThrowsArgumentOutOfRangeExceptionWhenIsMinInclusiveFalse(
            Documents.Routing.Range<string> xFeedRange,
            Documents.Routing.Range<string> yFeedRange)
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
                                    new FeedRangeEpk(xFeedRange),
                                    new FeedRangeEpk(yFeedRange),
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
        ///   Scenario Outline: Verify if the y feed range is a subset of the x feed range
        ///     Given a x feed range with min value <xMinValue> and max value <xMaxValue>
        ///       And the x feed range is <xIsMinInclusive> inclusive of its min value
        ///       And the x feed range is <xIsMaxInclusive> inclusive of its max value
        ///     When a y feed range with min value <yMinValue> and max value <yMaxValue> is compared
        ///       And the y feed range is <yIsMinInclusive> inclusive of its min value
        ///       And the y feed range is <yIsMaxInclusive> inclusive of its max value
        ///     Then the result should be <expectedIsSubset> indicating if the y is a subset of the x
        /// ]]>
        /// </summary>
        /// <param name="xIsMinInclusive">Indicates whether the x range's minimum value is inclusive.</param>
        /// <param name="xIsMaxInclusive">Indicates whether the x range's maximum value is inclusive.</param>
        /// <param name="xMinValue">The minimum value of the x range.</param>
        /// <param name="xMaxValue">The maximum value of the x range.</param>
        /// <param name="yIsMinInclusive">Indicates whether the y range's minimum value is inclusive.</param>
        /// <param name="yIsMaxInclusive">Indicates whether the y range's maximum value is inclusive.</param>
        /// <param name="yMinValue">The minimum value of the y range.</param>
        /// <param name="yMaxValue">The maximum value of the y range.</param>
        /// <param name="expectedIsSubset">A boolean indicating whether the y feed range is expected to be a subset of the x feed range. True if the y is a subset, false otherwise.</param>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [DataRow(true, true, "A", "Z", true, true, "A", "Z", true, DisplayName = "(true, true) Given both x and y ranges (A to Z) are fully inclusive and equal, y is a subset")]
        [DataRow(true, true, "A", "A", true, true, "A", "A", true, DisplayName = "(true, true) Given both x and y ranges (A to A) are fully inclusive and equal, and min and max range is the same, y is a subset")]
        [DataRow(true, true, "A", "A", true, true, "B", "B", false, DisplayName = "(true, true) Given both x and y ranges are fully inclusive but min and max ranges are not the same (A to A, B to B), y is not a subset")]
        [DataRow(true, true, "B", "B", true, true, "A", "A", false, DisplayName = "(true, true) Given x range (B to B) is fully inclusive and y range (A to A) is fully inclusive, y is not a subset")]
        [DataRow(true, false, "A", "Z", true, true, "A", "Y", true, DisplayName = "(false, true) Given x range (A to Z) has an exclusive max and y range (A to Y) is fully inclusive, y is a subset")]
        [DataRow(true, false, "A", "Y", true, true, "A", "Z", false, DisplayName = "(false, true) Given x range (A to Y) has an exclusive max but y range (A to Z) exceeds the x’s max with an inclusive bound, y is not a subset")]
        [DataRow(true, false, "A", "Z", true, true, "A", "Z", false, DisplayName = "(false, true) Given x range (A to Z) has an exclusive max and y range (A to Z) is fully inclusive, y is not a subset")]
        [DataRow(true, false, "A", "Y", true, false, "A", "Y", true, DisplayName = "(false, false) Given x range (A to Y) is inclusive at min and exclusive at max, and y range (A to Y) is inclusive at min and exclusive at max, y is a subset")]
        [DataRow(true, false, "A", "W", true, false, "A", "Y", false, DisplayName = "(false, false) Given x range (A to W) is inclusive at min and exclusive at max, and y range (A to Y) is inclusive at min and exclusive at max, y is not a subset")]
        [DataRow(true, false, "A", "Y", true, false, "A", "W", true, DisplayName = "(false, false) Given x range (A to Y) is inclusive at min and exclusive at max, and y range (A to W) is inclusive at min and exclusive at max, y is a subset")]
        public void GivenXRangeWhenYRangeComparedThenValidateIfSubset(
            bool xIsMinInclusive,
            bool xIsMaxInclusive,
            string xMinValue,
            string xMaxValue,
            bool yIsMinInclusive,
            bool yIsMaxInclusive,
            string yMinValue,
            string yMaxValue,
            bool expectedIsSubset)
        {
            bool actualIsSubset = ContainerCore.IsSubset(
                new Documents.Routing.Range<string>(isMinInclusive: xIsMinInclusive, isMaxInclusive: xIsMaxInclusive, min: xMinValue, max: xMaxValue),
                new Documents.Routing.Range<string>(isMinInclusive: yIsMinInclusive, isMaxInclusive: yIsMaxInclusive, min: yMinValue, max: yMaxValue));

            Assert.AreEqual(
                expected: expectedIsSubset,
                actual: actualIsSubset);
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Feed Range Subset Verification NotSupportedException
        /// 
        /// Scenario Outline: X MaxInclusive, Y MaxExclusive (true/false)
        ///   Given a x feed range with inclusive minimum and maximum values and a y feed range with an inclusive minimum and exclusive maximum, 
        ///   When the y feed range is compared against the x, 
        ///   Then an exception is expected if the y's maximum value is exclusive and the x's maximum value is inclusive.
        /// ]]>
        /// </summary>
        /// <param name="xIsMinInclusive">Indicates whether the x range's minimum value is inclusive.</param>
        /// <param name="xIsMaxInclusive">Indicates whether the x range's maximum value is inclusive.</param>
        /// <param name="xMinValue">The minimum value of the x range.</param>
        /// <param name="xMaxValue">The maximum value of the x range.</param>
        /// <param name="yIsMinInclusive">Indicates whether the y range's minimum value is inclusive.</param>
        /// <param name="yIsMaxInclusive">Indicates whether the y range's maximum value is inclusive.</param>
        /// <param name="yMinValue">The minimum value of the y range.</param>
        /// <param name="yMaxValue">The maximum value of the y range.</param>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [DataRow(true, true, "A", "Y", true, false, "A", "W", DisplayName = "(true, false) Given x range (A to Y) is inclusive at min and max, and y range (A to W) is inclusive at min and exclusive at max, expects NotSupportedException")]
        [DataRow(true, true, "A", "Z", true, false, "A", "X", DisplayName = "(true, false) Given x range (A to Z) is inclusive at min and max, and y range (A to X) is inclusive at min and exclusive at max, expects NotSupportedException")]
        [DataRow(true, true, "A", "Y", true, false, "A", "Y", DisplayName = "(true, false) Given x range (A to Y) is inclusive at min and max, and y range (A to Y) is inclusive at min and exclusive at max, expects NotSupportedException")]
        public void GivenXMaxInclusiveYMaxExclusiveWhenCallingIsSubsetThenExpectNotSupportedExceptionIsThrown(
            bool xIsMinInclusive,
            bool xIsMaxInclusive,
            string xMinValue,
            string xMaxValue,
            bool yIsMinInclusive,
            bool yIsMaxInclusive,
            string yMinValue,
            string yMaxValue)
        {
            NotSupportedException exception = Assert.ThrowsException<NotSupportedException>(() => ContainerCore.IsSubset(
                new Documents.Routing.Range<string>(min: xMinValue, max: xMaxValue, isMinInclusive: xIsMinInclusive, isMaxInclusive: xIsMaxInclusive),
                new Documents.Routing.Range<string>(min: yMinValue, max: yMaxValue, isMinInclusive: yIsMinInclusive, isMaxInclusive: yIsMaxInclusive)));

            Assert.IsNotNull(exception);
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Verify IsSubset method
        ///
        ///   Scenario Outline: Handle null x range input
        ///     Given a null x feed range
        ///     And a valid y feed range
        ///     When calling the IsSubset method
        ///     Then an ArgumentNullException is thrown
        /// ]]>
        /// </summary>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        public void GivenNullXFeedRangeWhenCallingIsSubsetThenArgumentNullExceptionIsThrown()
        {
            ArgumentNullException exception = Assert.ThrowsException<ArgumentNullException>(() => ContainerCore.IsSubset(
                null,
                new Documents.Routing.Range<string>(min: "A", max: "Z", isMinInclusive: true, isMaxInclusive: true)));

            Assert.IsNotNull(exception);
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Verify IsSubset method
        ///
        ///   Scenario Outline: Handle null y range input
        ///     Given a valid x feed range
        ///     And a null y feed range
        ///     When calling the IsSubset method
        ///     Then an ArgumentNullException is thrown
        /// ]]>
        /// </summary>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        public void GivenNullYFeedRangeWhenCallingIsSubsetThenArgumentNullExceptionIsThrown()
        {
            ArgumentNullException exception = Assert.ThrowsException<ArgumentNullException>(() => ContainerCore.IsSubset(
                new Documents.Routing.Range<string>(min: "A", max: "Z", isMinInclusive: true, isMaxInclusive: true),
                null));

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
