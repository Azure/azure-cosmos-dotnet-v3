//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.IO;

    /// <summary>
    /// Placeholder for VST Logger.
    /// </summary>
    internal class CosmosSerializerHelper : CosmosSerializer
    {
        private CosmosSerializer cosmosSerializer = TestCommon.Serializer;
        private Action<dynamic> fromStreamCallback;
        private Action<dynamic> toStreamCallBack;

        public CosmosSerializerHelper(
            Action<dynamic> fromStreamCallback,
            Action<dynamic> toStreamCallBack)
        {
            this.fromStreamCallback = fromStreamCallback;
            this.toStreamCallBack = toStreamCallBack;
        }

        public override T FromStream<T>(Stream stream)
        {
            T item = this.cosmosSerializer.FromStream<T>(stream);
            this.fromStreamCallback?.Invoke(item);

            return item;
        }

        public override Stream ToStream<T>(T input)
        {
            this.toStreamCallBack?.Invoke(input);
            return this.cosmosSerializer.ToStream<T>(input);
        }
    }
}
