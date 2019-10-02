//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using global::Azure.Core.Pipeline;
    using global::Azure.Data.Cosmos;

    internal static class CoreExtensions
    {
        internal static Stream GetStream(this HttpPipelineRequestContent content)
        {
            if (content == null)
            {
                return null;
            }

            CosmosStreamContent cosmosContent = content as CosmosStreamContent;
            if (cosmosContent != null)
            {
                return cosmosContent.Detach();
            }

            // Return stream
            throw new NotImplementedException();
        }
    }
}
