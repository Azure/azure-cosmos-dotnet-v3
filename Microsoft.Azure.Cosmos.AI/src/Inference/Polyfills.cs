//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

// Contains internal constants copied from the Direct package to avoid a dependency on it for Microsoft.Azure.Cosmos.AI.

namespace Microsoft.Azure.Documents;

internal static class HttpConstants
{
    internal static class HttpMethods
    {
        public const string Post = "POST";
    }

    internal static class HttpHeaders
    {
        public const string Accept = "Accept";
        public const string UserAgent = "User-Agent";
        public const string Version = "x-ms-version";
    }

    internal static class Versions
    {
        public const string CurrentVersion = "2023-07-15";
    }
}

internal static class RuntimeConstants
{
    internal static class MediaTypes
    {
        public const string Json = "application/json";
    }
}
