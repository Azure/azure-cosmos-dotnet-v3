//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.IO;

    internal class PatchOperationCore<T> : PatchOperation<T>
    {
        public PatchOperationCore(
            PatchOperationType operationType,
            string path,
            T value)
            : base(operationType, path, value)
        {
        }

        internal override bool TrySerializeValueParameter(
            CosmosSerializer cosmosSerializer,
            out string valueParam)
        {
            // Use the user serializer so custom conversions are correctly handled
            using (Stream stream = cosmosSerializer.ToStream(this.Value))
            {
                using (StreamReader streamReader = new StreamReader(stream))
                {
                    valueParam = streamReader.ReadToEnd();
                }
            }

            return true;
        }
    }
}
