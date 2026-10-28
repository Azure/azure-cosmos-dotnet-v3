namespace Microsoft.Azure.Cosmos.Direct.Test
{
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class SDKSupportedCapabilitiesHelpersTests
    {
        [TestMethod]
        [Owner("dikshibahl")]
        public void GetSDKSupportedCapabilities_ReturnsExpectedCapabilities()
        {
            // Arrange
            ulong expectedCapabilities = (ulong)(SDKSupportedCapabilities.PartitionMerge | SDKSupportedCapabilities.IgnoreUnknownRntbdTokens);

            // Act
            ulong actualCapabilities = SDKSupportedCapabilitiesHelpers.GetSDKSupportedCapabilities();

            // Assert
            Assert.AreEqual(expectedCapabilities, actualCapabilities, "The SDK supported capabilities do not match the expected value.");
        }
    }
}