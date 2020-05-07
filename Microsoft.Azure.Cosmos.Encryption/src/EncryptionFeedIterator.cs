//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;

    internal sealed class EncryptionFeedIterator : FeedIterator
    {
        private readonly FeedIterator feedIterator;
        private readonly Encryptor encryptor;
        Action<DecryptionResult> DecryptionResultHandler;

        public EncryptionFeedIterator(
            FeedIterator feedIterator,
            Encryptor encryptor,
            Action<DecryptionResult> DecryptionResultHandler = null)
        {
            this.feedIterator = feedIterator;
            this.encryptor = encryptor;
            this.DecryptionResultHandler = DecryptionResultHandler;
        }

        public override bool HasMoreResults => this.feedIterator.HasMoreResults;

        public async override Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(options: null);
            using (diagnosticsContext.CreateScope("FeedIterator.ReadNext"))
            {
                using (ResponseMessage responseMessage = await this.feedIterator.ReadNextAsync(cancellationToken))
                {
                    if (responseMessage.Content != null)
                    {
                        Stream decryptedContent = await this.DeserializeAndDecryptResponseAsync(
                            responseMessage.Content,
                            diagnosticsContext,
                            cancellationToken);

                        return new DecryptedResponseMessage(responseMessage, decryptedContent);
                    }

                    return responseMessage;
                }
            }
        }

        private async Task<Stream> DeserializeAndDecryptResponseAsync(
            Stream content,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            JObject contentJObj = EncryptionProcessor.baseSerializer.FromStream<JObject>(content);
            JArray result = new JArray();
            
            if (contentJObj.SelectToken(Constants.DocumentsResourcePropertyName) is JArray documents)
            {
                foreach (JToken value in documents)
                {
                    if (value is JObject document)
                    {
                        try
                        {
                            JObject decryptedDocument = await EncryptionProcessor.DecryptAsync(
                                document,
                                this.encryptor,
                                diagnosticsContext,
                                cancellationToken);

                            result.Add(decryptedDocument);
                        }
                        catch (Exception exception)
                        {
                            if (this.DecryptionResultHandler == null)
                            {
                                throw;
                            }

                            result.Add(document);

                            MemoryStream memoryStream = EncryptionProcessor.baseSerializer.ToStream(document);
                            Debug.Assert(memoryStream != null);
                            ArraySegment<byte> encryptedStream;
                            Debug.Assert(memoryStream.TryGetBuffer(out encryptedStream));

                            this.DecryptionResultHandler(
                                new DecryptionResult(
                                    encryptedStream,
                                    exception));
                        }
                    }
                    else
                    {
                        result.Add(value);
                    }
                }
            }
            else
            {
                throw new InvalidOperationException("Feed response Body Contract was violated. Feed response did not have an array of Documents");
            }

            JObject decryptedResponse = new JObject();
            foreach (JProperty property in contentJObj.Properties())
            {
                if (property.Name.Equals(Constants.DocumentsResourcePropertyName))
                {
                    decryptedResponse.Add(property.Name, (JToken)result);
                }
                else
                {
                    decryptedResponse.Add(property.Name, property.Value);
                }
            }

            return EncryptionProcessor.baseSerializer.ToStream(decryptedResponse); 
        }
    }
}
