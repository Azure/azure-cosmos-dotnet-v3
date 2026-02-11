//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using static Microsoft.Azure.Documents.Rntbd.Connection;
    internal interface IConnection : IDisposable
    {
        // Properties
        Uri ServerUri { get; }
        BufferProvider BufferProvider { get; }
        bool Healthy { get; }
        bool Disposed { get; }
        Guid ConnectionCorrelationId { get; }

        // Methods
        Task OpenAsync(ChannelOpenArguments args);

        Task WriteRequestAsync(
            ChannelCommonArguments args,
            TransportSerialization.SerializedRequest messagePayload,
            TransportRequestStats transportRequestStats);

        Task<Connection.ResponseMetadata> ReadResponseMetadataAsync(ChannelCommonArguments args);

        Task<MemoryStream> ReadResponseBodyAsync(ChannelCommonArguments args);

        bool IsActive(out TimeSpan timeToIdle);

        void NotifyConnectionStatus(bool isCompleted, bool isReadRequest = false);

    }
}
