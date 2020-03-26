namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using Microsoft.Azure.Cosmos.CosmosElements;

    internal static class CosmosElementTestExtensions
    {
        public static double ToDouble(this CosmosElement element)
        {
            if (!(element is CosmosNumber cosmosNumber))
            {
                throw new ArgumentException($"Expected cosmos number: {element}.");
            }

            return Number64.ToDouble(cosmosNumber.Value);
        }
    }
}
