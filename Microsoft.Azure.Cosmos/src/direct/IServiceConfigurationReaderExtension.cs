//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    /// <summary>
    /// Interface extension for IServiceConfigurationReader, should be consolidated with IServiceConfigurationReader in the future
    /// This is needed because the way compute uses SDK is using a V3 OSS clone of our public v3 repo.
    /// So, the moment you change some interface in the Direct/ Shared Files, it immediately gets reflected in the OSS code path
    /// </summary>
    internal interface IServiceConfigurationReaderExtension : IServiceConfigurationReader
    {
        IServiceRetryParams TryGetServiceRetryParams(DocumentServiceRequest documentServiceRequest);

        bool TryGetConsistencyLevel(DocumentServiceRequest documentServiceRequest, out ConsistencyLevel consistencyLevel);
    }
}
