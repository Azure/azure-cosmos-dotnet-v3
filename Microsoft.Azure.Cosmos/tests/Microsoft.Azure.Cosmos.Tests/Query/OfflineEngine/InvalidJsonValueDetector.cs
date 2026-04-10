//-----------------------------------------------------------------------
// <copyright file="InvalidJsonValueDetector.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests.Query.OfflineEngine
{
    using Microsoft.Azure.Cosmos.CosmosElements;

    /// <summary>
    /// Detects invalid JSON values.
    /// </summary>
    internal sealed class InvalidJsonValueDetector : ICosmosElementVisitor<bool>
    {
        public static readonly InvalidJsonValueDetector Singleton = new InvalidJsonValueDetector();

        private InvalidJsonValueDetector()
        {
        }

        public bool Visit(CosmosArray cosmosArray)
        {
            foreach (CosmosElement item in cosmosArray)
            {
                if (item.Accept(this))
                {
                    return true;
                }
            }

            return false;
        }

        public bool Visit(CosmosBinary cosmosBinary)
        {
            return false;
        }

        public bool Visit(CosmosBoolean cosmosBoolean)
        {
            return false;
        }

        public bool Visit(CosmosGuid cosmosGuid)
        {
            return false;
        }

        public bool Visit(CosmosNull cosmosNull)
        {
            return false;
        }

        public bool Visit(CosmosNumber cosmosNumber)
        {
            if (cosmosNumber.Value.IsInteger)
            {
                return false;
            }

            double value = Number64.ToDouble(cosmosNumber.Value);
            return
                double.IsInfinity(value) ||
                double.IsNaN(value) ||
                double.IsNegativeInfinity(value) ||
                double.IsPositiveInfinity(value);
        }

        public bool Visit(CosmosObject cosmosObject)
        {
            foreach (CosmosElement value in cosmosObject.Values)
            {
                if (value.Accept(this))
                {
                    return true;
                }
            }

            return false;
        }

        public bool Visit(CosmosString cosmosString)
        {
            return false;
        }

        public bool Visit(CosmosUndefined cosmosUndefined)
        {
            return false;
        }
    }
}