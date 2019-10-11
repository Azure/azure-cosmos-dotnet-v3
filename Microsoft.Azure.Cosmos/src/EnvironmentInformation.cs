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
        private static readonly string clientSDKVersion;
        private static readonly string directPackageVersion;
        private static readonly string framework;
        private static readonly string architecture;
        private static readonly string os;

        static EnvironmentInformation()
        {
            Version sdkVersion = Assembly.GetAssembly(typeof(CosmosClient)).GetName().Version;
            EnvironmentInformation.clientSDKVersion = $"{sdkVersion.Major}.{sdkVersion.Minor}.{sdkVersion.Build}";
            Version directVersion = Assembly.GetAssembly(typeof(Documents.UserAgentContainer)).GetName().Version;
            EnvironmentInformation.directPackageVersion = $"{directVersion.Major}.{directVersion.Minor}.{directVersion.Build}";
            EnvironmentInformation.framework = RuntimeInformation.FrameworkDescription;
            EnvironmentInformation.architecture = RuntimeInformation.ProcessArchitecture.ToString();
            EnvironmentInformation.os = RuntimeInformation.OSDescription;
        }

        public EnvironmentInformation()
        {
            string now = DateTime.UtcNow.Ticks.ToString();
            this.ClientId = now.Substring(now.Length - 5); // 5 most significative digits
        }

        /// <summary>
        /// Unique identifier of a client
        /// </summary>
        public string ClientId { get; }

        /// <summary>
        /// Version of the current direct package.
        /// </summary>
        public string DirectVersion => EnvironmentInformation.directPackageVersion;

        /// <summary>
        /// Version of the current client.
        /// </summary>
        public string ClientVersion => EnvironmentInformation.clientSDKVersion;

        /// <summary>
        /// Identifier of the Operating System.
        /// </summary>
        /// <seealso cref="RuntimeInformation.FrameworkDescription"/>
        public string OperatingSystem => EnvironmentInformation.os;

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
    }
}
