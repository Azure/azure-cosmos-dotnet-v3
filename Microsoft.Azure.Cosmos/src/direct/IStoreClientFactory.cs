//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;

    internal interface IStoreClientFactory: IDisposable
    {
        StoreClient CreateStoreClient(
            IAddressResolver addressResolver,
            ISessionContainer sessionContainer,
            IServiceConfigurationReader serviceConfigurationReader,
            IAuthorizationTokenProvider authorizationTokenProvider,
            bool enableRequestDiagnostics = false,
            bool enableReadRequestsFallback = false,
            bool useFallbackClient = true,
            bool useMultipleWriteLocations = false,
            bool detectClientConnectivityIssues = false);
    }
}
