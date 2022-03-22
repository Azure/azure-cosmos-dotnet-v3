namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class FeedRangeCreateFromPartitionKeyAsyncTests
    {
        /// <summary>
        /// The purpose is to expect an exception using a partial, or prefixed, Partition Key for a hashed Container.
        /// ArgumentException = PK -> C -> HC
        /// </summary>
        [TestMethod]
        [ExpectedException(exceptionType: typeof(ArgumentException))]
        public async Task WithPreFixPartitionKeyOnAHashContainerReturnsExceptionTest()
        {
            await RunTestAsync(
                arrange: () =>
                {
                    Documents.PartitionKeyDefinition partitionKeyDefinition = new()
                    {
                        Paths = new() { @"/state", @"/city", @"/zipcode" },
                        Kind = Documents.PartitionKind.Hash
                    };

                    PartitionKey partitionKey = new PartitionKeyBuilder()
                        .Add("98052")
                        .Build();

                    return (
                        new(string.Empty, string.Empty, partitionKey),
                        new(id: string.Empty, partitionKeyDefinition: partitionKeyDefinition));
                },
                actAsync: async (actInputs) =>
                {
                    try
                    {
                        return await Cosmos.FeedRange.CreateFromPartitionKeyAsync(container: actInputs.Container, partitionKey: actInputs.PartitionKey).ConfigureAwait(false);
                    }
                    catch (ArgumentException exception)
                    {
                        Assert.AreEqual(
                            expected: "This partition key definition kind is not supported for partial partition key operations",
                            actual: exception.Message);

                        throw exception;
                    }

                },
                assert: (assertInputs) =>
                {
                    Assert.IsInstanceOfType(value: assertInputs.FeedRange, expectedType: typeof(FeedRangePartitionKey));
                    FeedRangePartitionKey expected = new(assertInputs.PartitionKey);
                    Assert.AreEqual(expected: expected.ToJsonString(), actual: assertInputs.FeedRange.ToJsonString());
                });
        }

        /// <summary>
        /// The purpose is to create a new Feed Range Partition Key using a non-partial, or complete, Partition Key for a hashed Container.
        /// PK = PK -> C -> HC
        /// </summary>
        [TestMethod]
        public async Task WithPartitionKeyOnAHashContainerReturnsFeedRangePartitionKeyTest()
        {
            await RunTestAsync(
                arrange: () =>
                {
                    Documents.PartitionKeyDefinition partitionKeyDefinition = new()
                    { 
                        Paths = new() { @"/zipcode" },
                        Kind = Documents.PartitionKind.Hash
                    };

                    PartitionKey partitionKey = new PartitionKeyBuilder()
                        .Add("98052")
                        .Build();

                    return (
                        new(string.Empty, string.Empty, partitionKey),
                        new(id: string.Empty, partitionKeyDefinition: partitionKeyDefinition));
                },
                actAsync: async (actInputs) => await Cosmos.FeedRange.CreateFromPartitionKeyAsync(container: actInputs.Container, partitionKey: actInputs.PartitionKey).ConfigureAwait(false),
                assert: (assertInputs) =>
                {
                    Assert.IsInstanceOfType(value: assertInputs.FeedRange, expectedType: typeof(FeedRangePartitionKey));
                    FeedRangePartitionKey expected = new(assertInputs.PartitionKey);
                    Assert.AreEqual(expected: expected.ToJsonString(), actual: assertInputs.FeedRange.ToJsonString());
                });
        }

        /// <summary>
        /// The purpose is to create a new Feed Range Partition Key using a non-partial, or non-prefixed, Partition Key for a multi-hashed Container.
        /// PK = PK -> C -> MHC
        /// </summary>
        [TestMethod]
        public async Task WithNoPreFixPartitionKeyOnAMultiHashContainerReturnsFeedRangePartitionKeyTest()
        {
            await RunTestAsync(
                arrange: () =>
                {
                    Documents.PartitionKeyDefinition partitionKeyDefinition = new()
                    {
                        Paths = new() { @"/state", @"/city", @"/zipcode" },
                        Kind = Documents.PartitionKind.MultiHash
                    };

                    PartitionKey partitionKey = new PartitionKeyBuilder()
                        .Add("WA")
                        .Add("Redmond")
                        .Add("98052")
                        .Build();

                    return (
                       new(string.Empty, string.Empty, partitionKey),
                       new(id: string.Empty, partitionKeyDefinition: partitionKeyDefinition));
                },
                actAsync: async (actInputs) => await Cosmos.FeedRange.CreateFromPartitionKeyAsync(container: actInputs.Container, partitionKey: actInputs.PartitionKey).ConfigureAwait(false),
                assert: (assertInputs) =>
                {
                    Assert.IsInstanceOfType(value: assertInputs.FeedRange, expectedType: typeof(FeedRangePartitionKey));
                    FeedRangePartitionKey expected = new(assertInputs.PartitionKey);
                    Assert.AreEqual(expected: expected.ToJsonString(), actual: assertInputs.FeedRange.ToJsonString());
                });
        }

        /// <summary>
        /// The purpose is to create a new Feed Range EPK using a partial, or prefixed, Partition Key for a multi-hashed Container.
        /// EPK = PK -> P -> MHC
        /// </summary>
        [TestMethod]
        public async Task WithPreFixPartitionKeyOnAMultiHashContainerReturnsFeedRangeEpkTest()
        {
            await RunTestAsync(
                arrange: () =>
                {
                    Documents.PartitionKeyDefinition partitionKeyDefinition = new()
                    {
                        Paths = new() { @"/state", @"/city", @"/zipcode" },
                        Kind = Documents.PartitionKind.MultiHash
                    };

                    PartitionKey partitionKey = new PartitionKeyBuilder()
                        .Add("WA")
                        .Add("Redmond")
                        .Build();

                    return (
                       new (string.Empty, string.Empty, partitionKey),
                       new(id: string.Empty, partitionKeyDefinition: partitionKeyDefinition));
                },
                actAsync: async (actInputs) => await Cosmos.FeedRange.CreateFromPartitionKeyAsync(container: actInputs.Container, partitionKey: actInputs.PartitionKey).ConfigureAwait(false),
                assert: (assertInputs) =>
                {
                    Assert.IsInstanceOfType(value: assertInputs.FeedRange, expectedType: typeof(FeedRangeEpk));
                    Documents.Routing.Range<string> range = new("0845FB119899DE50766A2C4CEFC2FA7301620B162169497AFD85FA66E99F7376", "0845FB119899DE50766A2C4CEFC2FA7301620B162169497AFD85FA66E99F7376FF", true, false);
                    FeedRangeEpk expected = new(range);
                    Assert.AreEqual(expected: expected.ToJsonString(), actual: assertInputs.FeedRange.ToJsonString());
                });
        }

        #region Helpers
        public static async Task RunTestAsync(
            Func<(ArrangeInputs, ContainerProperties containerProperties)> arrange,
            Func<ActInputs, Task<Cosmos.FeedRange>> actAsync,
            Action<AssertInputs> assert)
        {
            (ArrangeInputs arrangeInputs, ContainerProperties containerProperties) = arrange();

            Mock<Container> mockContainer = new();
            Container container = mockContainer.Object;

            mockContainer
                .Setup(container => container.Id)
                .Returns(arrangeInputs.ContainerId)
                .Verifiable();

            mockContainer
                .Setup(container => container.ReadContainerAsync(null, default))
                .Returns(Task.FromResult(new ContainerResponse(System.Net.HttpStatusCode.OK, default, containerProperties, container, default)))
                .Verifiable();

            Cosmos.FeedRange feedRange = await actAsync(new(container, arrangeInputs.PartitionKey));

            assert(new(feedRange, arrangeInputs.PartitionKey));

            Assert.IsNotNull(feedRange);
        }
        #endregion
    }

    public record ArrangeInputs(string DatabaseId, string ContainerId, PartitionKey PartitionKey);
    public record ActInputs(Container Container, PartitionKey PartitionKey);
    public record AssertInputs(Cosmos.FeedRange FeedRange, PartitionKey PartitionKey);
}