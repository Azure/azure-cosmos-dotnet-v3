//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    ///  Contains the cosmos diagnostic information for the current request to Azure Cosmos DB service.
    /// </summary>
    public abstract class CosmosDiagnostics
    {
        /// <summary>
        /// This represent the end to end elapsed time of the request.
        /// If the request is still in progress it will return the current
        /// elapsed time since the start of the request.
        /// </summary>
        /// <returns>The clients end to end elapsed time of the request.</returns>
        public virtual TimeSpan GetClientElapsedTime()
        {
            // Default implementation avoids breaking change for users upgrading.
            throw new NotImplementedException($"CosmosDiagnostics.GetElapsedTime");
        }

        /// <summary>
        /// Gets the string field <see cref="CosmosDiagnostics"/> instance in the Azure CosmosDB database service.
        /// </summary>
        /// <returns>The string field <see cref="CosmosDiagnostics"/> instance in the Azure CosmosDB database service.</returns>
        public abstract override string ToString();

        /// <summary>
        /// Gets the human readable text representation of CosmosDiagnostics. 
        /// This is the representation that is best used for manual debugging and intellisense.
        /// </summary>
        /// <remarks>The format of this response is not a hard contract. It can change from SDK version to SDK version.</remarks>
        /// <returns>The human readable text representation of CosmosDiagnostics. </returns>
        public virtual string ToTextString()
        {
            // Default implementation avoids breaking change for users upgrading.
            throw new NotImplementedException($"{nameof(CosmosDiagnostics)}.{nameof(ToTextString)}");
        }

        /// <summary>
        /// Gets the JSON representation of CosmosDiagnostics. 
        /// This is the representation that is best used for automated analysis.
        /// </summary>
        /// <remarks>The format of this response is not a hard contract. It can change from SDK version to SDK version.</remarks>
        /// <returns>The JSON representation of CosmosDiagnostics. </returns>
        public virtual string ToJsonString()
        {
            // Default implementation avoids breaking change for users upgrading.
            throw new NotImplementedException($"{nameof(CosmosDiagnostics)}.{nameof(ToJsonString)}");
        }

        /// <summary>
        /// Gets the binary representation of CosmosDiagnostics. 
        /// This is the representation that is best used for storage in binary enabled logging.
        /// </summary>
        /// <remarks>
        /// The format of this response is not a hard contract. 
        /// It can change from SDK version to SDK version.
        /// If this is sent as information in a support ticket the tooling can parse it.
        /// </remarks>
        /// <returns>The binary representation of CosmosDiagnostics. </returns>
        public virtual ReadOnlyMemory<byte> ToBinary()
        {
            // Default implementation avoids breaking change for users upgrading.
            throw new NotImplementedException($"{nameof(CosmosDiagnostics)}.{nameof(ToBinary)}");
        }
    }
}
