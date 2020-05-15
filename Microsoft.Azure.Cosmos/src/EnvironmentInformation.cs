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
        private const int MaxClientId = 10;
        private static readonly string clientSDKVersion;
        private static readonly string directPackageVersion;
        private static readonly string framework;
        private static readonly string architecture;
        private static readonly string os;
        private static readonly object clientCountLock = new object();
        private static int clientId = 0;

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
            lock (EnvironmentInformation.clientCountLock)
            {
                int newClientId = EnvironmentInformation.MaxClientId;
                if (EnvironmentInformation.clientId <= EnvironmentInformation.MaxClientId)
                {
                    newClientId = EnvironmentInformation.clientId++;
                }

                this.ClientId = newClientId.ToString().PadLeft(2, '0');
            }
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

        /// <summary>
        /// Only used to reset counter on tests.
        /// </summary>
        public static void ResetCounter()
        {
            EnvironmentInformation.clientId = 0;
        }
    }
}
