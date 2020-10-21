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
                "/status");

                ToDoActivity toDoActivity = ToDoActivity.CreateRandomToDoActivity();
                await container.CreateItemAsync(toDoActivity, new PartitionKey(toDoActivity.status));
                await container.ReadItemAsync<ToDoActivity>(toDoActivity.id, new PartitionKey(toDoActivity.status));
                await container.UpsertItemAsync<ToDoActivity>(toDoActivity, new PartitionKey(toDoActivity.status));
                toDoActivity.cost = 8923498;
                await container.ReplaceItemAsync<ToDoActivity>(toDoActivity, toDoActivity.id, new PartitionKey(toDoActivity.status));
                await container.DeleteItemAsync<ToDoActivity>(toDoActivity.id, new PartitionKey(toDoActivity.status));
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

                this.ValidateLazyHeadersAreNotCreated(request.Headers.InternalHeaders);

                ResponseMessage responseMessage = await base.SendAsync(request, cancellationToken);

                this.ValidateLazyHeadersAreNotCreated(request.Headers.InternalHeaders);

                return responseMessage;
            }

            private void ValidateLazyHeadersAreNotCreated(InternalHeaders internalHeaders)
            {
                StoreRequestHeaders storeRequestHeaders = (StoreRequestHeaders)internalHeaders;
                FieldInfo lazyHeaders = typeof(StoreRequestHeaders).GetField("lazyNotCommonHeaders", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Lazy<Dictionary<string, string>> lazyNotCommonHeaders = (Lazy<Dictionary<string, string>>)lazyHeaders.GetValue(storeRequestHeaders);
                // Use the if instead of Assert.IsFalse to avoid creating the dictionary in the error message
                if (lazyNotCommonHeaders.IsValueCreated)
                {
                    Assert.Fail($"The lazy dictionary should not be created. Please add the following headers to the {nameof(StoreRequestHeaders)}: {JsonConvert.SerializeObject(lazyNotCommonHeaders.Value)}");
                }
            }
        }
    }
}
