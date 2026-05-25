//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Performance.Tests
{
    /// <summary>
    /// Public surrogate for the SDK's internal <see cref="JsonProcessor"/> enum so it can be used
    /// as a <see cref="BenchmarkDotNet.Attributes.ParamsAttribute"/> value on a public benchmark
    /// property. BenchmarkDotNet rejects [Params] on non-public members, so a public surrogate is
    /// required for the perf project to load.
    /// </summary>
    public enum JsonProcessorOption
    {
        Newtonsoft = 0,
#if NET8_0_OR_GREATER
        Stream = 1,
#endif
    }
}
