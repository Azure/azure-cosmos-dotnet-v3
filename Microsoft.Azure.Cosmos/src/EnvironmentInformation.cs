//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Reflection;
    using System.Runtime.InteropServices;

    internal sealed class EnvironmentInformation
    {
        internal const string Delimiter = " ";
        private static readonly string clientId;
        private static readonly string clientSDKVersion;
        private static readonly string framework;
        private static readonly string architecture;

        static EnvironmentInformation()
        {
            Version sdkVersion = Assembly.GetAssembly(typeof(CosmosClient)).GetName().Version;
            EnvironmentInformation.clientSDKVersion = $"{sdkVersion.Major}.{sdkVersion.Minor}.{sdkVersion.Build}";
            EnvironmentInformation.framework = RuntimeInformation.FrameworkDescription;
            EnvironmentInformation.architecture = RuntimeInformation.ProcessArchitecture.ToString();
            EnvironmentInformation.clientId = DateTime.UtcNow.Ticks.ToString();
        }

        /// <summary>
        /// Unique identifier of a client
        /// </summary>
        public string ClientId => EnvironmentInformation.clientId;

        /// <summary>
        /// Version of the current client.
        /// </summary>
        public string ClientVersion => EnvironmentInformation.clientSDKVersion;

        /// <summary>
        /// Identifier of the Framework.
        /// </summary>
        /// <seealso cref="RuntimeInformation.FrameworkDescription"/>
        public string RuntimeFramework => EnvironmentInformation.framework;

        /// <summary>
        /// Type of architecture being used.
        /// </summary>
        /// <seealso cref="RuntimeInformation.ProcessArchitecture"/>
        public string ProcessArchitecture => EnvironmentInformation.architecture;

        public override string ToString()
        {
            return $" {this.ClientVersion}-{this.RuntimeFramework} {this.ProcessArchitecture} {this.ClientId}";
        }
    }
}
