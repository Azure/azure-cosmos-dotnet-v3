// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Net;

    internal sealed class EncryptionDatabaseResponse : DatabaseResponse
    {
        public EncryptionDatabaseResponse(
            DatabaseResponse databaseResponse,
            EncryptionCosmosClient encryptionCosmosClient)
        {
            this.databaseResponse = databaseResponse ?? throw new ArgumentNullException(nameof(databaseResponse));
            this.encryptionCosmosClient = encryptionCosmosClient ?? throw new ArgumentNullException(nameof(encryptionCosmosClient));
        }

        public override Database Database => new EncryptionDatabase(this.databaseResponse.Database, this.encryptionCosmosClient);

        private readonly DatabaseResponse databaseResponse;

        private readonly EncryptionCosmosClient encryptionCosmosClient;

        public override Headers Headers => this.databaseResponse.Headers;

        public override DatabaseProperties Resource => this.databaseResponse.Resource;

        public override HttpStatusCode StatusCode => this.databaseResponse.StatusCode;

        public override double RequestCharge => this.databaseResponse.RequestCharge;

        public override string ActivityId => this.databaseResponse.ActivityId;

        public override string ETag => this.databaseResponse.ETag;

        public override CosmosDiagnostics Diagnostics => this.databaseResponse.Diagnostics;

        public static implicit operator Database(EncryptionDatabaseResponse response)
        {
            return response.Database;
        }
    }
}
