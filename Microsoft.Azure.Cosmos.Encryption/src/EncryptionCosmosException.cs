// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Net;

    internal sealed class EncryptionCosmosException : CosmosException
    {
        private readonly EncryptionCosmosDiagnostics encryptionCosmosDiagnostics;

        public EncryptionCosmosException(
            string message,
            HttpStatusCode statusCode,
            int subStatusCode,
            string activityId,
            double requestCharge,
            EncryptionCosmosDiagnostics encryptionCosmosDiagnostics)
            : base(message, statusCode, subStatusCode, activityId, requestCharge)
        {
            this.encryptionCosmosDiagnostics = encryptionCosmosDiagnostics ?? throw new ArgumentNullException(nameof(encryptionCosmosDiagnostics));
        }

        public override CosmosDiagnostics Diagnostics => this.encryptionCosmosDiagnostics;
    }
}
