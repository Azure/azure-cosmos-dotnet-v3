//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using Newtonsoft.Json.Linq;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using System;

#if PREVIEW
    public
#else
    internal
#endif
    class EncryptionFeedIterator : FeedIterator
    {
        private readonly FeedIterator FeedIterator;
        private readonly Encryptor Encryptor;
        Action<byte[], Exception> ErrorHandler;

        public EncryptionFeedIterator(
            FeedIterator feedIterator,
            Encryptor encryptor,
            Action<byte[], Exception> errorHandler = null)
        {
            this.FeedIterator = feedIterator;
            this.Encryptor = encryptor;
            this.ErrorHandler = errorHandler;
        }

        public override bool HasMoreResults => this.FeedIterator.HasMoreResults;

        public async override Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(null);
            using (diagnosticsContext.CreateScope("FeedIterator.ReadNext"))
            {
                ResponseMessage responseMessage = await this.FeedIterator.ReadNextAsync(cancellationToken);

                if(responseMessage.Content != null)
                {
                    responseMessage.Content = await this.DeserializeAndDecryptResponseAsync(
                        responseMessage.Content,
                        diagnosticsContext,
                        cancellationToken);
                }

                return responseMessage;
            }
        }

        private async Task<Stream> DeserializeAndDecryptResponseAsync(
            Stream content,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            JObject contentJObj = EncryptionProcessor.baseSerializer.FromStream<JObject>(content);
            JToken containerRidProperty = contentJObj.Property(Constants.ContainerRid).Value;
            JToken containerRid = null;
            if(containerRidProperty != null)
            {
                containerRid = containerRidProperty.Value<JToken>();
            }

            JArray result = new JArray();            
            if (contentJObj.SelectToken("Documents") is JArray documents)
            {
                foreach(JObject document in documents)
                {
                    try
                    {
                        JObject decryptedDocument = await EncryptionProcessor.DecryptAsync(
                            document,
                            this.Encryptor,
                            diagnosticsContext,
                            cancellationToken);

                        result.Add(decryptedDocument);
                    }
                    catch (Exception ex)
                    {
                        result.Add(document);
                        if (this.ErrorHandler != null)
                        {
                            MemoryStream memoryStream = EncryptionProcessor.baseSerializer.ToStream(document) as MemoryStream;
                            this.ErrorHandler(memoryStream.ToArray(), ex);
                        }
                    }
                }
            }
            else
            {
                throw new InvalidOperationException($"Response Body Contract was violated. Response did not have an array of Documents");
            }

            JObject decryptedResponse = new JObject();
            if (containerRid != null)
            {
                decryptedResponse.Add(Constants.ContainerRid, containerRid);
            }
            decryptedResponse.Add("Documents", (JToken)result);
            decryptedResponse.Add("_count", (JToken)(result.Count));

            return EncryptionProcessor.baseSerializer.ToStream(decryptedResponse); 
        }
    }
}
