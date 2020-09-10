//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using Newtonsoft.Json;

    internal sealed class EncryptionTransactionalBatchResponse : TransactionalBatchResponse
    {
        private readonly List<TransactionalBatchOperationResult> results;
        private readonly TransactionalBatchResponse response;
        private bool isDisposed = false;

        public EncryptionTransactionalBatchResponse(List<TransactionalBatchOperationResult> results, TransactionalBatchResponse response)
        {
            this.results = results;
            this.response = response;
        }

        public override TransactionalBatchOperationResult this[int index]
        {
            get
            {
                return this.results[index];
            }
        }

        public override TransactionalBatchOperationResult<T> GetOperationResultAtIndex<T>(int index)
        {
            TransactionalBatchOperationResult result = this.results[index];

            T resource = default(T);
            if (result.ResourceStream != null)
            {
                resource = FromStream<T>(result.ResourceStream);
            }

            return (TransactionalBatchOperationResult<T>)(TransactionalBatchOperationResult)new EncryptionTransactionalBatchOperationResult<T>(result, resource);
        }

        internal static T FromStream<T>(Stream stream)
        {
            using (StreamReader sr = new StreamReader(stream))
            using (JsonReader reader = new JsonTextReader(sr))
            {
                JsonSerializer serializer = new JsonSerializer();
                return serializer.Deserialize<T>(reader);
            }
        }

        public override IEnumerator<TransactionalBatchOperationResult> GetEnumerator()
        {
            return this.results.GetEnumerator();
        }

        public override Headers Headers => this.response.Headers;

        public override string ActivityId => this.response.ActivityId;

        public override double RequestCharge => this.response.RequestCharge;

        public override TimeSpan? RetryAfter => this.response.RetryAfter;

        public override HttpStatusCode StatusCode => this.response.StatusCode;

        public override string ErrorMessage => this.response.ErrorMessage;

        public override bool IsSuccessStatusCode => this.response.IsSuccessStatusCode;

        public override int Count => this.response.Count;

        public override CosmosDiagnostics Diagnostics => this.response.Diagnostics;

        protected override void Dispose(bool disposing)
        {
            if (disposing && !this.isDisposed)
            {
                this.isDisposed = true;

                if (this.response != null)
                {
                    this.response.Dispose();
                }
            }
        }
    }
}