//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    internal sealed class ServerProperties
    {
        public ServerProperties (string agent, string version)
        {
            this.Agent = agent;
            this.Version = version;
        }

        public string Agent { get; private set; }

        public string Version { get; private set; }
    }
}
