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
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", false, true }; // The child range, starting from a lower bound minimum and ending just before 3FFFFFFFFFFFFFFF, fits entirely within the parent range, which starts from a lower bound minimum and ends just before FFFFFFFFFFFFFFFF.
            yield return new object[] { "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", false, true }; // The child range, from 3FFFFFFFFFFFFFFF to just before 7FFFFFFFFFFFFFFF, fits entirely within the parent range, which starts from a lower bound minimum and ends just before FFFFFFFFFFFFFFFF.
            yield return new object[] { "7FFFFFFFFFFFFFFF", "BFFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", false, true }; // The child range, from 7FFFFFFFFFFFFFFF to just before BFFFFFFFFFFFFFFF, fits entirely within the parent range, which starts from a lower bound minimum and ends just before FFFFFFFFFFFFFFFF.
            yield return new object[] { "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", false, true }; // The child range, from BFFFFFFFFFFFFFFF to just before FFFFFFFFFFFFFFFF, fits entirely within the parent range, which starts from a lower bound minimum and ends just before FFFFFFFFFFFFFFFF.
            yield return new object[] { "3FFFFFFFFFFFFFFF", "4CCCCCCCCCCCCCCC", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true }; // The child range, from 3FFFFFFFFFFFFFFF to just before 4CCCCCCCCCCCCCCC, fits entirely within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "4CCCCCCCCCCCCCCC", "5999999999999999", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true }; // The child range, from 4CCCCCCCCCCCCCCC to just before 5999999999999999, fits entirely within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "5999999999999999", "6666666666666666", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true }; // The child range, from 5999999999999999 to just before 6666666666666666, fits entirely within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "6666666666666666", "7333333333333333", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true }; // The child range, from 6666666666666666 to just before 7333333333333333, fits entirely within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "7333333333333333", "7FFFFFFFFFFFFFFF", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true }; // The child range, from 7333333333333333 to just before 7FFFFFFFFFFFFFFF, fits entirely within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "", "3FFFFFFFFFFFFFFF", false, true }; // The child range, starting from a lower bound minimum and ending just before 3FFFFFFFFFFFFFFF, fits entirely within the parent range, which starts from a lower bound minimum and ends just before 3FFFFFFFFFFFFFFF.

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
        /// Scenario: Child feed range is part of the parent feed range with the child’s isMaxInclusive set to true and the parent’s isMaxInclusive set to false
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
            yield return new object[] { "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", true, "", "FFFFFFFFFFFFFFFF", false, true }; // The child range, from BFFFFFFFFFFFFFFF to FFFFFFFFFFFFFFFF (inclusive), fits within the parent range, starting from a lower bound minimum and ending just before FFFFFFFFFFFFFFFF.
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "", "3FFFFFFFFFFFFFFF", false, true }; // The child range, from a lower bound minimum to 3FFFFFFFFFFFFFFF (inclusive), fits entirely within the parent range, which starts from a lower bound minimum and ends just before 3FFFFFFFFFFFFFFF.
            yield return new object[] { "3FFFFFFFFFFFFFFF", "4CCCCCCCCCCCCCCC", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true }; // The child range, from 3FFFFFFFFFFFFFFF to 4CCCCCCCCCCCCCCC (inclusive), fits within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "4CCCCCCCCCCCCCCC", "5999999999999999", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true }; // The child range, from 4CCCCCCCCCCCCCCC to 5999999999999999 (inclusive), fits within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "5999999999999999", "6666666666666666", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true }; // The child range, from 5999999999999999 to 6666666666666666 (inclusive), fits within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "6666666666666666", "7333333333333333", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true }; // The child range, from 6666666666666666 to 7333333333333333 (inclusive), fits within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "7333333333333333", "7FFFFFFFFFFFFFFF", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, true }; // The child range, from 7333333333333333 to 7FFFFFFFFFFFFFFF (inclusive), fits within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
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
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false }; // The child range, starting from a lower bound minimum and ending at 3FFFFFFFFFFFFFFF (inclusive), does not fit within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "7FFFFFFFFFFFFFFF", "BFFFFFFFFFFFFFFF", false, false }; // The child range, starting from a lower bound minimum and ending at 3FFFFFFFFFFFFFFF (inclusive), does not fit within the parent range, which starts from 7FFFFFFFFFFFFFFF and ends just before BFFFFFFFFFFFFFFF.
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", true, "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", false, false }; // The child range, starting from a lower bound minimum and ending at 3FFFFFFFFFFFFFFF (inclusive), does not fit within the parent range, which starts from BFFFFFFFFFFFFFFF and ends just before FFFFFFFFFFFFFFFF.
            yield return new object[] { "", "3333333333333333", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false }; // The child range, starting from a lower bound minimum and ending at 3333333333333333 (inclusive), does not fit within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "3333333333333333", "6666666666666666", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false }; // The child range, from 3333333333333333 to 6666666666666666 (inclusive), does not fit within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "7333333333333333", "FFFFFFFFFFFFFFFF", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false }; // The child range, from 7333333333333333 to FFFFFFFFFFFFFFFF (inclusive), does not fit within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
            yield return new object[] { "", "7333333333333333", true, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, false }; // The child range, starting from a lower bound minimum and ending at 7333333333333333 (inclusive), does not fit within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends just before 7FFFFFFFFFFFFFFF.
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
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", true, true }; // The child range, starting from a lower bound minimum and ending just before 3FFFFFFFFFFFFFFF, fits entirely within the parent range, which starts from a lower bound minimum and ends at FFFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", true, true }; // The child range, from 3FFFFFFFFFFFFFFF to just before 7FFFFFFFFFFFFFFF, fits entirely within the parent range, which starts from a lower bound minimum and ends at FFFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "7FFFFFFFFFFFFFFF", "BFFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", true, true }; // The child range, from 7FFFFFFFFFFFFFFF to just before BFFFFFFFFFFFFFFF, fits entirely within the parent range, which starts from a lower bound minimum and ends at FFFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", false, "", "FFFFFFFFFFFFFFFF", true, true }; // The child range, from BFFFFFFFFFFFFFFF to just before FFFFFFFFFFFFFFFF, fits entirely within the parent range, which starts from a lower bound minimum and ends at FFFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "", "3FFFFFFFFFFFFFFF", true, true }; // The child range, from a lower bound minimum to just before 3FFFFFFFFFFFFFFF, fits entirely within the parent range, which starts from a lower bound minimum and ends at 3FFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "3FFFFFFFFFFFFFFF", "4CCCCCCCCCCCCCCC", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, true }; // The child range, from 3FFFFFFFFFFFFFFF to just before 4CCCCCCCCCCCCCCC, fits entirely within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends at 7FFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "4CCCCCCCCCCCCCCC", "5999999999999999", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, true }; // The child range, from 4CCCCCCCCCCCCCCC to just before 5999999999999999, fits entirely within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends at 7FFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "5999999999999999", "6666666666666666", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, true }; // The child range, from 5999999999999999 to just before 6666666666666666, fits entirely within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends at 7FFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "6666666666666666", "7333333333333333", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, true }; // The child range, from 6666666666666666 to just before 7333333333333333, fits entirely within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends at 7FFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "7333333333333333", "7FFFFFFFFFFFFFFF", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, true }; // The child range, from 7333333333333333 to just before 7FFFFFFFFFFFFFFF, fits entirely within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends at 7FFFFFFFFFFFFFFF (inclusive).
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
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, false }; // The child range, starting from a lower bound minimum and ending just before 3FFFFFFFFFFFFFFF, does not fit within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends at 7FFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "7FFFFFFFFFFFFFFF", "BFFFFFFFFFFFFFFF", true, false }; // The child range, starting from a lower bound minimum and ending just before 3FFFFFFFFFFFFFFF, does not fit within the parent range, which starts from 7FFFFFFFFFFFFFFF and ends at BFFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "", "3FFFFFFFFFFFFFFF", false, "BFFFFFFFFFFFFFFF", "FFFFFFFFFFFFFFFF", true, false }; // The child range, starting from a lower bound minimum and ending just before 3FFFFFFFFFFFFFFF, does not fit within the parent range, which starts from BFFFFFFFFFFFFFFF and ends at FFFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "", "3333333333333333", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, false }; // The child range, starting from a lower bound minimum and ending just before 3333333333333333, does not fit within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends at 7FFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "3333333333333333", "6666666666666666", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, false }; // The child range, from 3333333333333333 to just before 6666666666666666, does not fit within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends at 7FFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "7333333333333333", "FFFFFFFFFFFFFFFF", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, false }; // The child range, from 7333333333333333 to just before FFFFFFFFFFFFFFFF, does not fit within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends at 7FFFFFFFFFFFFFFF (inclusive).
            yield return new object[] { "", "7333333333333333", false, "3FFFFFFFFFFFFFFF", "7FFFFFFFFFFFFFFF", true, false }; // The child range, starting from a lower bound minimum and ending just before 7333333333333333, does not fit within the parent range, which starts from 3FFFFFFFFFFFFFFF and ends at 7FFFFFFFFFFFFFFF (inclusive).
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
        /// Scenario: Child feed range is not part of the parent feed range with both the child’s and parent’s isMaxInclusive set to true
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
        /// Feature: Child Range Subset Validation
        ///
        /// Scenario: Validate whether the child range is a subset of the parent range
        /// based on whether the parent and child ranges are max inclusive or max exclusive.
        ///
        /// Given a parent range with a specified max inclusivity,
        /// When a child range is compared with a specified max inclusivity,
        /// Then determine if the child range is a subset of the parent range.
        /// </summary>
        /// <param name="isParentMaxInclusive">Indicates whether the parent range's max value is inclusive (true) or exclusive (false).</param>
        /// <param name="isChildMaxInclusive">Indicates whether the child range's max value is inclusive (true) or exclusive (false).</param>
        /// <param name="expectedIsSubsetOfParent">The expected result: true if the child range is expected to be a subset of the parent range, false otherwise.</param>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [DataRow(true, true, true, DisplayName = "Given parent is max inclusive, when child is max inclusive, then child is a subset of parent")]
        [DataRow(true, false, true, DisplayName = "Given parent is max inclusive, when child is max exclusive, then child is a subset of parent")]
        [DataRow(false, false, true, DisplayName = "Given parent is max exclusive, when child is max exclusive, then child is a subset of parent")]
        [DataRow(false, true, false, DisplayName = "Given parent is max exclusive, when child is max inclusive, then child is not a subset of parent")]
        public void GivenParentRangeWhenChildRangeComparedThenValidateIfSubset(
            bool isParentMaxInclusive,
            bool isChildMaxInclusive,
            bool expectedIsSubsetOfParent)
        {
            // Define a shared min and max range for both parent and child
            string minRange = "A";  // Example shared min value
            string maxRange = "Z";  // Example shared max value

            // Create strategies based on inclusivity for the parent and child ranges
            IRangeStrategy parentRangeStrategy = isParentMaxInclusive
                ? new InclusiveMaxRangeStrategy()
                : new ExclusiveMaxRangeStrategy();

            IRangeStrategy childRangeStrategy = isChildMaxInclusive
                ? new InclusiveMaxRangeStrategy()
                : new ExclusiveMaxRangeStrategy();

            // Create range generator contexts for both parent and child using the same min and max
            RangeGeneratorContext parentContext = new RangeGeneratorContext(parentRangeStrategy);
            RangeGeneratorContext childContext = new RangeGeneratorContext(childRangeStrategy);

            // Generate ranges for parent and child based on shared min and max range
            (string min, string max) parentRange = parentContext.GenerateRange(minRange, maxRange);
            (string min, string max) childRange = childContext.GenerateRange(minRange, maxRange);

            // Note, the values of isMaxInclusive and isMinInclusive do not affect the outcome of this test, as IsSubset only depends on the min and max values of the parent and child ranges.

            Assert.AreEqual(
                expected: expectedIsSubsetOfParent,
                actual: ContainerCore.IsSubset(
                    parentRange: new Documents.Routing.Range<string>(
                        min: parentRange.min,
                        max: parentRange.max,
                        isMinInclusive: true,
                        isMaxInclusive: isParentMaxInclusive),
                    childRange: new Documents.Routing.Range<string>(
                        min: childRange.min,
                        max: childRange.max,
                        isMinInclusive: true,
                        isMaxInclusive: isChildMaxInclusive)));
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

    public interface IRangeStrategy
    {
        (string min, string max) GenerateRange(string min, string maxValue);
    }

    public class InclusiveMaxRangeStrategy : IRangeStrategy
    {
        public (string min, string max) GenerateRange(string min, string maxValue)
        {
            return (min, maxValue); // max is inclusive
        }
    }

    public class ExclusiveMaxRangeStrategy : IRangeStrategy
    {
        public (string min, string max) GenerateRange(string min, string maxValue)
        {
            // Subtract 1 lexicographically for exclusive max range
            string exclusiveMax = (maxValue.Length > 0) ? (char)(maxValue[0] - 1) + maxValue[1..] : maxValue;
            return (min, exclusiveMax);
        }
    }

    public class RangeGeneratorContext
    {
        private readonly IRangeStrategy rangeStrategy;

        public RangeGeneratorContext(IRangeStrategy rangeStrategy)
        {
            this.rangeStrategy = rangeStrategy;
        }

        public (string min, string max) GenerateRange(string min, string maxValue)
        {
            return this.rangeStrategy.GenerateRange(min, maxValue);
        }
    }
}
