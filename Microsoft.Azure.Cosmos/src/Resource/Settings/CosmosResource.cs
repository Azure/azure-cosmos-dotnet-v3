//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using Microsoft.Azure.Documents;

    /// <summary> 
    ///  Represents an abstract resource type in the Azure Cosmos DB service.
    ///  All Azure Cosmos DB resources, such as <see cref="Database"/>, <see cref="DocumentCollection"/>, and <see cref="Document"/> extend this abstract type.
    /// </summary>
    internal static class CosmosResource
    {
        private static readonly CosmosJsonDotNetSerializer cosmosDefaultJsonSerializer = new CosmosJsonDotNetSerializer();

        internal static T FromStream<T>(DocumentServiceResponse response)
        {
            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            if (response.ResponseBody != null && (!response.ResponseBody.CanSeek || response.ResponseBody.Length > 0))
            {
                return CosmosResource.FromStream<T>(response.ResponseBody);
            }

            return default(T);
        }

        internal static Stream ToStream<T>(T input)
        {
            return CosmosResource.cosmosDefaultJsonSerializer.ToStream(input);
        }

        internal static T FromStream<T>(Stream stream)
        {
            return CosmosResource.cosmosDefaultJsonSerializer.FromStream<T>(stream);
        }
    }
}
