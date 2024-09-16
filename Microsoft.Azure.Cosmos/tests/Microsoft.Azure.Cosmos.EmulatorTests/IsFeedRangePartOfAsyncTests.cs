//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class IsFeedRangePartOfAsyncTests
    {
        private CosmosClient cosmosClient = null;
        private Cosmos.Database cosmosDatabase = null;
        private ContainerInternal containerInternal = null;
        private ContainerInternal hierarchicalContainerInternal = null;

        [TestInitialize]
        public async Task TestInit()
        {
            this.cosmosClient = TestCommon.CreateCosmosClient();

            string databaseName = Guid.NewGuid().ToString();
            DatabaseResponse cosmosDatabaseResponse = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName);
            this.cosmosDatabase = cosmosDatabaseResponse;
            
            this.containerInternal = await IsFeedRangePartOfAsyncTests.CreateSinglePartitionContainer(this.cosmosDatabase);
            this.hierarchicalContainerInternal = await IsFeedRangePartOfAsyncTests.CreateHierachalContainer(this.cosmosDatabase);
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

        private async static Task<ContainerInternal> CreateSinglePartitionContainer(Database cosmosDatabase)
        {
            ContainerResponse containerResponse = await cosmosDatabase.CreateContainerIfNotExistsAsync(
                id: Guid.NewGuid().ToString(),
                partitionKeyPath: "/pk");

            return (ContainerInternal)containerResponse.Container;
        }

        private async static Task<ContainerInternal> CreateHierachalContainer(Database cosmosDatabase)
        {
            ContainerProperties containerProperties = new ContainerProperties()
            {
                Id = Guid.NewGuid().ToString(),
                PartitionKeyPaths = new Collection<string> { "/pk", "/id" }
            };

            ContainerResponse containerResponse = await cosmosDatabase.CreateContainerIfNotExistsAsync(containerProperties);

            return (ContainerInternal)containerResponse.Container;
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation
        ///
        /// Scenario: Validate if the child partition key is part of the parent feed range
        ///   Given the parent feed range
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
        [Description("Validate if the child partition key is part of the parent feed range.")]
        public async Task GivenFeedRangeChildPartitionKeyIsPartOfParentFeedRange(
            string parentMinimum,
            string parentMaximum,
            bool expectedIsFeedRangePartOfAsync)
        {
            try
            {
                PartitionKey partitionKey = new("WA");
                FeedRange feedRange = FeedRange.FromPartitionKey(partitionKey);

                bool actualIsFeedRangePartOfAsync = await this.containerInternal.IsFeedRangePartOfAsync(
                    parentFeedRange: new FeedRangeEpk(new Documents.Routing.Range<string>(parentMinimum, parentMaximum, true, false)),
                    childFeedRange: feedRange,
                    cancellationToken: CancellationToken.None);

                Assert.AreEqual(expected: expectedIsFeedRangePartOfAsync, actual: actualIsFeedRangePartOfAsync);
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
        /// Scenario: Validate if the child hierarchical partition key is part of the parent feed range
        ///   Given the parent feed range
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

                bool actualIsFeedRangePartOfAsync = await this.hierarchicalContainerInternal.IsFeedRangePartOfAsync(
                    parentFeedRange: new FeedRangeEpk(new Documents.Routing.Range<string>(parentMinimum, parentMaximum, true, false)),
                    childFeedRange: feedRange,
                    cancellationToken: CancellationToken.None);

                Assert.AreEqual(expected: expectedIsFeedRangePartOfAsync, actual: actualIsFeedRangePartOfAsync);
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
        /// Scenario: Validate that an ArgumentNullException is thrown when the child feed range is null
        ///   Given the parent feed range is defined
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
        /// Scenario: Validate that an ArgumentNullException is thrown when the child feed range has no JSON representation
        ///   Given the parent feed range is defined
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
        /// Scenario: Validate that an ArgumentException is thrown when the child feed range has invalid JSON representation
        ///   Given the parent feed range is defined
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
                expectedMessage: $"The provided string '<xml />' does not represent any known format.");
        }

        private async Task GivenInvalidChildFeedRangeExpectsArgumentExceptionIsFeedRangePartOfAsyncTestAsync<TExceeption>(
            FeedRange feedRange,
            string expectedMessage)
            where TExceeption : Exception
        {
            try
            {
         
                TExceeption exception = await Assert.ThrowsExceptionAsync<TExceeption>(
                    async () => await this.containerInternal.IsFeedRangePartOfAsync(
                        parentFeedRange: new FeedRangeEpk(new Documents.Routing.Range<string>("", "FFFFFFFFFFFFFFFF", true, false)),
                        childFeedRange: feedRange,
                        cancellationToken: CancellationToken.None));

                Assert.IsNotNull(exception);
                Assert.IsTrue(exception.Message.Contains(expectedMessage));
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
        /// Scenario: Validate that an ArgumentNullException is thrown when the parent feed range is null
        ///   Given the parent feed range is null
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
        /// Scenario: Validate that an ArgumentNullException is thrown when the parent feed range has no JSON representation
        ///   Given the parent feed range has no JSON representation
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
        /// Scenario: Validate that an ArgumentException is thrown when the parent feed range has an invalid JSON representation
        ///   Given the parent feed range has an invalid JSON representation
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
                expectedMessage: $"The provided string '<xml />' does not represent any known format.");
        }

        private async Task GivenInvalidParentFeedRangeExpectsArgumentExceptionIsFeedRangePartOfAsyncTestAsync<TException>(FeedRange feedRange, string expectedMessage)
            where TException : Exception
        {
            try
            {
                TException exception = await Assert.ThrowsExceptionAsync<TException>(
                    async () => await this.containerInternal.IsFeedRangePartOfAsync(
                        parentFeedRange: feedRange,
                        childFeedRange: new FeedRangeEpk(new Documents.Routing.Range<string>("", "3FFFFFFFFFFFFFFF", true, false)),
                        cancellationToken: CancellationToken.None));

                Assert.IsNotNull(exception);
                Assert.IsTrue(exception.Message.Contains(expectedMessage));
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
        /// Scenario: Child feed range is or is not part of the parent feed range when both child's and parent's isMaxInclusive can be set to true or false
        ///   Given the parent feed range with isMaxInclusive set to true or false
        ///   And the child feed range with isMaxInclusive set to true or false
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
        /// <returns></returns>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [DynamicData(nameof(IsFeedRangePartOfAsyncTests.FeedRangeChildPartOfParentWhenBothChildAndParentIsMaxInclusiveTrue), DynamicDataSourceType.Method)]
        [DynamicData(nameof(IsFeedRangePartOfAsyncTests.FeedRangeChildNotPartOfParentWhenBothChildAndParentIsMaxInclusiveTrue), DynamicDataSourceType.Method)]
        [DynamicData(nameof(IsFeedRangePartOfAsyncTests.FeedRangeChildPartOfParentWhenChildIsMaxInclusiveFalseAndParentIsMaxInclusiveTrue), DynamicDataSourceType.Method)]
        [DynamicData(nameof(IsFeedRangePartOfAsyncTests.FeedRangeChildNotPartOfParentWhenChildIsMaxInclusiveFalseAndParentIsMaxInclusiveTrue), DynamicDataSourceType.Method)]
        [DynamicData(nameof(IsFeedRangePartOfAsyncTests.FeedRangeChildNotPartOfParentWhenBothIsMaxInclusiveAreFalse), DynamicDataSourceType.Method)]
        [DynamicData(nameof(IsFeedRangePartOfAsyncTests.FeedRangeChildNotPartOfParentWhenChildAndParentIsMaxInclusiveAreFalse), DynamicDataSourceType.Method)]
        [DynamicData(nameof(IsFeedRangePartOfAsyncTests.FeedRangeChildPartOfParentWhenChildIsMaxInclusiveTrueAndParentIsMaxInclusiveFalse), DynamicDataSourceType.Method)]
        [DynamicData(nameof(IsFeedRangePartOfAsyncTests.FeedRangeChildNotPartOfParentWhenChildIsMaxInclusiveTrueAndParentIsMaxInclusiveFalse), DynamicDataSourceType.Method)]
        [Description("Child feed range is or is not part of the parent feed range when both child's and parent's isMaxInclusive can be set to true or false.")]
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
                bool actualIsFeedRangePartOfAsync = await this.containerInternal.IsFeedRangePartOfAsync(
                    parentFeedRange: new FeedRangeEpk(new Documents.Routing.Range<string>(parentMinimum, parentMaximum, true, parentIsMaxInclusive)),
                    childFeedRange: new FeedRangeEpk(new Documents.Routing.Range<string>(childMinimum, childMaximum, true, childIsMaxInclusive)),
                    cancellationToken: CancellationToken.None);

                Assert.AreEqual(expected: expectedIsFeedRangePartOfAsync, actual: actualIsFeedRangePartOfAsync);
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
        /// Scenario: Child feed range is not part of the parent feed range with both isMaxInclusive set to false
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
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", false, true };
            yield return new object[] { "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", false, true };
            yield return new object[] { "7FFFFFFFFFFFFFFF", "BFFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", false, true };
            yield return new object[] { "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", false, true };
            yield return new object[] { "3FFFFFFFFFFFFFFF", "4CCCCCCCCCCCCCCC", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true };
            yield return new object[] { "4CCCCCCCCCCCCCCC", "5999999999999999", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true };
            yield return new object[] { "5999999999999999", "6666666666666666", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true };
            yield return new object[] { "6666666666666666", "7333333333333333", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true };
            yield return new object[] { "7333333333333333", "7FFFFFFFFFFFFFFF", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true };
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "", "3FFFFFFFFFFFFFFF", false, true };
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation
        ///
        /// Scenario: Child feed range is not part of the parent feed range with both child’s and parent’s isMaxInclusive set to false
        ///   Given the parent feed range with isMaxInclusive set to false
        ///   And the child feed range with isMaxInclusive set to false
        ///   When the child feed range is compared to the parent feed range
        ///   Then the child feed range is not part of the parent feed range
        /// ]]>
        /// </summary>
        private static IEnumerable<object[]> FeedRangeChildNotPartOfParentWhenChildAndParentIsMaxInclusiveAreFalse()
        {
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false };
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "7FFFFFFFFFFFFFFF", "BFFFFFFFFFFFFFFF", false, false };
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", false, false };
            yield return new object[] { "", "3333333333333333", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false };
            yield return new object[] { "3333333333333333", "6666666666666666", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false };
            yield return new object[] { "7333333333333333", "FFFFFFFFFFFFFFFF", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false };
            yield return new object[] { "", "7333333333333333", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false };
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation
        ///
        /// Scenario: Child feed range is part of the parent feed range with the child’s isMaxInclusive set to true and the parent’s isMaxInclusive set to false
        ///   Given the parent feed range with isMaxInclusive set to false
        ///   And the child feed range with isMaxInclusive set to true
        ///   When the child feed range is compared to the parent feed range
        ///   Then the child feed range is part of the parent feed range
        /// ]]>
        /// </summary>
        private static IEnumerable<object[]> FeedRangeChildPartOfParentWhenChildIsMaxInclusiveTrueAndParentIsMaxInclusiveFalse()
        {
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "", "FFFFFFFFFFFFFFFF", false, true };
            yield return new object[] { "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, "", "FFFFFFFFFFFFFFFF", false, true };
            yield return new object[] { "7FFFFFFFFFFFFFFF", "BFFFFFFFFFFFFFFF", true, "", "FFFFFFFFFFFFFFFF", false, true };
            yield return new object[] { "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", true, "", "FFFFFFFFFFFFFFFF", false, true };
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "", "3FFFFFFFFFFFFFFF", false, true };
            yield return new object[] { "3FFFFFFFFFFFFFFF", "4CCCCCCCCCCCCCCC", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true };
            yield return new object[] { "4CCCCCCCCCCCCCCC", "5999999999999999", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true };
            yield return new object[] { "5999999999999999", "6666666666666666", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true };
            yield return new object[] { "6666666666666666", "7333333333333333", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true };
            yield return new object[] { "7333333333333333", "7FFFFFFFFFFFFFFF", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true };
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation
        ///
        /// Scenario: Child feed range is not part of the parent feed range with the child’s isMaxInclusive set to true and the parent’s isMaxInclusive set to false
        ///   Given the parent feed range with isMaxInclusive set to false
        ///   And the child feed range with isMaxInclusive set to true
        ///   When the child feed range is compared to the parent feed range
        ///   Then the child feed range is not part of the parent feed range
        /// ]]>
        /// </summary>
        private static IEnumerable<object[]> FeedRangeChildNotPartOfParentWhenChildIsMaxInclusiveTrueAndParentIsMaxInclusiveFalse()
        {
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false };
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "7FFFFFFFFFFFFFFF", "BFFFFFFFFFFFFFFF", false, false };
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", false, false };
            yield return new object[] { "", "3333333333333333", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false };
            yield return new object[] { "3333333333333333", "6666666666666666", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false };
            yield return new object[] { "7333333333333333", "FFFFFFFFFFFFFFFF", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false };
            yield return new object[] { "", "7333333333333333", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false };
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation
        ///
        /// Scenario: Child feed range is part of the parent feed range with the child’s isMaxInclusive set to false and the parent’s isMaxInclusive set to true
        ///   Given the parent feed range with isMaxInclusive set to true
        ///   And the child feed range with isMaxInclusive set to false
        ///   When the child feed range is compared to the parent feed range
        ///   Then the child feed range is part of the parent feed range
        /// ]]>
        /// </summary>
        private static IEnumerable<object[]> FeedRangeChildPartOfParentWhenChildIsMaxInclusiveFalseAndParentIsMaxInclusiveTrue()
        {
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", true, true };
            yield return new object[] { "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", true, true };
            yield return new object[] { "7FFFFFFFFFFFFFFF", "BFFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", true, true };
            yield return new object[] { "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", true, true };
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "", "3FFFFFFFFFFFFFFF", true, true };
            yield return new object[] { "3FFFFFFFFFFFFFFF", "4CCCCCCCCCCCCCCC", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, true };
            yield return new object[] { "4CCCCCCCCCCCCCCC", "5999999999999999", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, true };
            yield return new object[] { "5999999999999999", "6666666666666666", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, true };
            yield return new object[] { "6666666666666666", "7333333333333333", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, true };
            yield return new object[] { "7333333333333333", "7FFFFFFFFFFFFFFF", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, true };
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation
        ///
        /// Scenario: Child feed range is not part of the parent feed range with the child’s isMaxInclusive set to false and the parent’s isMaxInclusive set to true
        ///   Given the parent feed range with isMaxInclusive set to true
        ///   And the child feed range with isMaxInclusive set to false
        ///   When the child feed range is compared to the parent feed range
        ///   Then the child feed range is not part of the parent feed range
        /// ]]>
        /// </summary>
        private static IEnumerable<object[]> FeedRangeChildNotPartOfParentWhenChildIsMaxInclusiveFalseAndParentIsMaxInclusiveTrue()
        {
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, false };
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "7FFFFFFFFFFFFFFF", "BFFFFFFFFFFFFFFF", true, false };
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", true, false };
            yield return new object[] { "", "3333333333333333", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, false };
            yield return new object[] { "3333333333333333", "6666666666666666", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, false };
            yield return new object[] { "7333333333333333", "FFFFFFFFFFFFFFFF", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, false };
            yield return new object[] { "", "7333333333333333", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, false };
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation
        ///
        /// Scenario: Child feed range is part of the parent feed range with both the child’s and parent’s isMaxInclusive set to true
        ///   Given the parent feed range with isMaxInclusive set to true
        ///   And the child feed range with isMaxInclusive set to true
        ///   When the child feed range is compared to the parent feed range
        ///   Then the child feed range is part of the parent feed range
        /// ]]>
        /// </summary>
        private static IEnumerable<object[]> FeedRangeChildPartOfParentWhenBothChildAndParentIsMaxInclusiveTrue()
        {
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "", "FFFFFFFFFFFFFFFF", true, true };
            yield return new object[] { "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, "", "FFFFFFFFFFFFFFFF", true, true };
            yield return new object[] { "7FFFFFFFFFFFFFFF", "BFFFFFFFFFFFFFFF", true, "", "FFFFFFFFFFFFFFFF", true, true };
            yield return new object[] { "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", true, "", "FFFFFFFFFFFFFFFF", true, true };
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "", "3FFFFFFFFFFFFFFF", true, true };
            yield return new object[] { "3FFFFFFFFFFFFFFF", "4CCCCCCCCCCCCCCC", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, true };
            yield return new object[] { "4CCCCCCCCCCCCCCC", "5999999999999999", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, true };
            yield return new object[] { "5999999999999999", "6666666666666666", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, true };
            yield return new object[] { "6666666666666666", "7333333333333333", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, true };
            yield return new object[] { "7333333333333333", "7FFFFFFFFFFFFFFF", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, true };
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation
        ///
        /// Scenario: Child feed range is not part of the parent feed range with both the child’s and parent’s isMaxInclusive set to true
        ///   Given the parent feed range with isMaxInclusive set to true
        ///   And the child feed range with isMaxInclusive set to true
        ///   When the child feed range is compared to the parent feed range
        ///   Then the child feed range is not part of the parent feed range
        /// ]]>
        /// </summary>
        private static IEnumerable<object[]> FeedRangeChildNotPartOfParentWhenBothChildAndParentIsMaxInclusiveTrue()
        {
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, false };
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "7FFFFFFFFFFFFFFF", "BFFFFFFFFFFFFFFF", true, false };
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", true, false };
            yield return new object[] { "", "3333333333333333", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, false };
            yield return new object[] { "3333333333333333", "6666666666666666", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, false };
            yield return new object[] { "7333333333333333", "FFFFFFFFFFFFFFFF", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, false };
            yield return new object[] { "", "7333333333333333", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, false };
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Feed Range PartOf Validation ArgumentOutOfRangeException
        ///
        /// Scenario: Validate if an ArgumentOutOfRangeException is thrown when the child feed range is compared to the parent feed range with the parent's IsMinInclusive set to false
        ///   Given the parent feed range with IsMinInclusive set to false
        ///   And the child feed range with a valid range
        ///   When the child feed range is compared to the parent feed range
        ///   Then an ArgumentOutOfRangeException should be thrown
        /// ]]>
        /// </summary>
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
        /// Scenario: Validate if an ArgumentOutOfRangeException is thrown when the child feed range is compared to the parent feed range with the child's IsMinInclusive set to false
        ///   Given the parent feed range with IsMinInclusive set to false
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
                ArgumentOutOfRangeException exception = await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(
                    async () => await this.containerInternal
                        .IsFeedRangePartOfAsync(
                            parentFeedRange: new FeedRangeEpk(parentFeedRange),
                            childFeedRange: new FeedRangeEpk(childFeedRange),
                            cancellationToken: CancellationToken.None));

                Assert.IsNotNull(exception);
                Assert.IsTrue(exception.Message.Contains("IsMinInclusive must be true."));
            }
            catch (Exception exception)
            {
                Assert.Fail(exception.Message);
            }
        }

        /// <summary>
        /// <![CDATA[
        /// Feature: Is Subset
        ///
        /// Scenario: Validate whether the child range is a subset of the parent range for various cases.
        ///   Given various parent and child feed ranges
        ///   When the child range is checked if it is a subset of the parent range
        ///   Then the actualIsSubset should either be true or false depending on the ranges
        /// ]]>
        /// </summary>
        /// <param name="parentMinimum">The starting value of the parent range.</param>
        /// <param name="parentMaximum">The ending value of the parent range.</param>
        /// <param name="childMinimum">The starting value of the child range.</param>
        /// <param name="childMaximum">The ending value of the child range.</param>
        /// <param name="expectedIsSubset">The expected actualIsSubset: true if the child is a subset, false otherwise.</param>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [DataRow("A", "Z", "B", "Y", true, DisplayName = "Child B-Y is a perfect subset of parent A-Z. Parent A-Z encapsulates child B-Y")]
        [DataRow("A", "Z", "B", "Z", true, DisplayName = "Child B-Z is a perfect subset of parent A-Z. Parent A-Z encapsulates child B-Z")]
        [DataRow("A", "Z", "A", "Z", true, DisplayName = "Child A-Z equals parent A-Z")]
        [DataRow("A", "Z", "@", "Y", false, DisplayName = "Child @-Y has min out of parent A-Z")]
        [DataRow("A", "Z", "B", "[", false, DisplayName = "Child B-[ has max out of parent A-Z")]
        [DataRow("A", "Z", "@", "[", false, DisplayName = "Child @-[ is completely outside parent A-Z")]
        [DataRow("A", "Z", "@", "Z", false, DisplayName = "Child @-Z has max equal to parent but min out of range")]
        [DataRow("A", "Z", "A", "[", false, DisplayName = "Child A-[ has min equal to parent but max out of range")]
        [DataRow("A", "Z", "", "", false, DisplayName = "Empty child range")]
        [DataRow("", "", "B", "Y", false, DisplayName = "Empty parent range with non-empty child range")]
        public void ValidateChildRangeIsSubsetOfParentForVariousCasesTest(string parentMinimum, string parentMaximum, string childMinimum, string childMaximum, bool expectedIsSubset)
        {
            Documents.Routing.Range<string> parentRange = new Documents.Routing.Range<string>(parentMinimum, parentMaximum, true, true);
            Documents.Routing.Range<string> childRange = new Documents.Routing.Range<string>(childMinimum, childMaximum, true, true);

            bool actualIsSubset = ContainerCore.IsSubset(parentRange, childRange);

            Assert.AreEqual(expected: expectedIsSubset, actual: actualIsSubset);
        }

        /// <summary>
        /// Validates if all ranges in the list have consistent inclusivity for both IsMinInclusive and IsMaxInclusive.
        /// Throws InvalidOperationException if any inconsistencies are found.
        ///
        /// <example>
        /// <![CDATA[
        /// Feature: Validate range inclusivity
        ///
        ///   Scenario: All ranges are consistent
        ///     Given a list of ranges where all have the same IsMinInclusive and IsMaxInclusive values
        ///     When the inclusivity is validated
        ///     Then no exception is thrown
        ///
        ///   Scenario: Inconsistent MinInclusive values
        ///     Given a list of ranges where IsMinInclusive values differ
        ///     When the inclusivity is validated
        ///     Then an InvalidOperationException is thrown
        ///
        ///   Scenario: Inconsistent MaxInclusive values
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
}
