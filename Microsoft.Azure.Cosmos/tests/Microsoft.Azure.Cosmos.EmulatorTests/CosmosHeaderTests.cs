//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class CosmosHeaderTests 
    {
        [TestMethod]
        public async Task VerifyKnownHeaders()
        {
            HeaderValidationHandler headerValidationHandler = new HeaderValidationHandler();
            using CosmosClient client = TestCommon.CreateCosmosClient(x => x.AddCustomHandlers(headerValidationHandler));
            Database database = null;
            try
            {
                database = await client.CreateDatabaseAsync(nameof(VerifyKnownHeaders) + Guid.NewGuid().ToString());
                Container container = await database.CreateContainerAsync(
                Guid.NewGuid().ToString(),
                "/pk");

                ToDoActivity toDoActivity = ToDoActivity.CreateRandomToDoActivity();
                await container.CreateItemAsync(toDoActivity, new PartitionKey(toDoActivity.pk));
                await container.ReadItemAsync<ToDoActivity>(toDoActivity.id, new PartitionKey(toDoActivity.pk));
                await container.UpsertItemAsync<ToDoActivity>(toDoActivity, new PartitionKey(toDoActivity.pk));
                toDoActivity.cost = 8923498;
                await container.ReplaceItemAsync<ToDoActivity>(toDoActivity, toDoActivity.id, new PartitionKey(toDoActivity.pk));
                await container.DeleteItemAsync<ToDoActivity>(toDoActivity.id, new PartitionKey(toDoActivity.pk));
            }
            finally
            {
                if (database != null)
                {
                    await database.DeleteStreamAsync();
                }
            }
        }

        [TestMethod]
        public async Task VerifyRequestOptionCustomRequestHeaders()
        {
            CustomHeaderValidationHandler headerValidationHandler = new CustomHeaderValidationHandler();
            using CosmosClient client = TestCommon.CreateCosmosClient(x => x.AddCustomHandlers(headerValidationHandler));
            Database database = null;
            try
            {
                database = await client.CreateDatabaseAsync(nameof(VerifyRequestOptionCustomRequestHeaders) + Guid.NewGuid().ToString());
                Container container = await database.CreateContainerAsync(
                Guid.NewGuid().ToString(),
                "/pk");

                ToDoActivity toDoActivity = ToDoActivity.CreateRandomToDoActivity();
                ItemRequestOptions requestOptions = new ItemRequestOptions
                {
                    AddRequestHeaders = (headers) => headers["x-ms-cosmos-database-rid"] = "databaseRidValue",
                };

                await container.CreateItemAsync(toDoActivity, new PartitionKey(toDoActivity.pk), requestOptions: requestOptions);

                // null pass
                requestOptions.AddRequestHeaders = null;

                await container.ReadItemAsync<ToDoActivity>(toDoActivity.id, new PartitionKey(toDoActivity.pk), requestOptions: requestOptions);
            }
            finally
            {
                if (database != null)
                {
                    await database.DeleteStreamAsync();
                }
            }
        }

        private class HeaderValidationHandler : RequestHandler
        {
            public override async Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
            {
                if(request.ResourceType != Documents.ResourceType.Document)
                {
                    return await base.SendAsync(request, cancellationToken);
                }

                this.ValidateLazyHeadersAreNotCreated(request.Headers.CosmosMessageHeaders);

                ResponseMessage responseMessage = await base.SendAsync(request, cancellationToken);

                this.ValidateLazyHeadersAreNotCreated(request.Headers.CosmosMessageHeaders);

                return responseMessage;
            }

            private void ValidateLazyHeadersAreNotCreated(CosmosMessageHeadersInternal internalHeaders)
            {
                StoreRequestNameValueCollection storeRequestHeaders = (StoreRequestNameValueCollection)internalHeaders;
                FieldInfo lazyHeaders = typeof(StoreRequestNameValueCollection).GetField("lazyNotCommonHeaders", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Lazy<Dictionary<string, string>> lazyNotCommonHeaders = (Lazy<Dictionary<string, string>>)lazyHeaders.GetValue(storeRequestHeaders);
                // Use the if instead of Assert.IsFalse to avoid creating the dictionary in the error message
                if (lazyNotCommonHeaders.IsValueCreated)
                {
                    Assert.Fail($"The lazy dictionary should not be created. Please add the following headers to the {nameof(StoreRequestNameValueCollection)}: {JsonConvert.SerializeObject(lazyNotCommonHeaders.Value)}");
                }
            }
        }

        private class CustomHeaderValidationHandler : RequestHandler
        {
            public override async Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
            {
                if (request.ResourceType == Documents.ResourceType.Document)
                {
                    this.ValidateCustomHeaders(request.Headers.CosmosMessageHeaders);
                }

                return await base.SendAsync(request, cancellationToken);
            }

            private void ValidateCustomHeaders(CosmosMessageHeadersInternal internalHeaders)
            {
                string customHeaderValue = internalHeaders.Get("x-ms-cosmos-database-rid");

                if (!string.IsNullOrEmpty(customHeaderValue))
                {
                    Assert.AreEqual("databaseRidValue", customHeaderValue);
                }                
            }
        }
    }
}
