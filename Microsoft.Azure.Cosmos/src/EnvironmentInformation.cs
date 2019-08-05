//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Reflection;
    using System.Runtime.InteropServices;

    //{SDK-id}-{process runtime}-{process-arch}-{host-rutnime}-{host-arch}
    //{SDK-id}: Should include the name, version, direct version
    internal sealed class EnvironmentInformation
    {
        internal static readonly string clientId;
        internal static readonly string clientSDKVersion;
        internal static readonly string framework;
        internal static readonly string architecture;

        static EnvironmentInformation()
        {
            Version sdkVersion = Assembly.GetAssembly(typeof(CosmosClient)).GetName().Version;
            EnvironmentInformation.clientSDKVersion = $"{sdkVersion.Major}.{sdkVersion.Minor}.{sdkVersion.Revision}";
            EnvironmentInformation.framework = RuntimeInformation.FrameworkDescription;
            EnvironmentInformation.architecture = RuntimeInformation.ProcessArchitecture.ToString();
            EnvironmentInformation.clientId = Guid.NewGuid().ToString();
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
            return $"/{this.ClientVersion}-{this.RuntimeFramework}-{this.ProcessArchitecture}-{this.ClientId}";
        }
    }
}
