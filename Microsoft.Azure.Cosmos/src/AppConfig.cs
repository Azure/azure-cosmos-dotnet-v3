//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Azure.Cosmos
{
    internal sealed class AppConfig
    {
        [FeatureSwitchDefinition("Microsoft.Azure.Cosmos.AppConfig.IsEnabled")]
        internal static bool IsEnabled { get; } = GetIsEnabled();

        private static bool GetIsEnabled()
        {
#if NETSTANDARD20
            // GetEntryAssembly returns null when loaded from native netstandard2.0
            return System.Reflection.Assembly.GetEntryAssembly() != null;
#else
            return true;
#endif
        }
    }
}
