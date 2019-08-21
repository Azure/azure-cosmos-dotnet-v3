//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Documents;

    internal class PointOperationStatistics
    {
        private ClientSideRequestStatistics clientSideRequestStatistics;

        public PointOperationStatistics(ClientSideRequestStatistics clientSideRequestStatistics)
        {
            this.clientSideRequestStatistics = clientSideRequestStatistics;
        }

        public override string ToString()
        {
            return this.clientSideRequestStatistics.ToString();
        }
    }
}
