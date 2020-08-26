//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using Microsoft.Azure.Documents;

    //Internal Test hooks.
    internal static class DocumentClientExtensions
    {
        private static readonly TransportClient transportClient =
            new Documents.Rntbd.TransportClient(
                new Documents.Rntbd.TransportClient.Options(TimeSpan.FromSeconds(5.0)));

        //This will lock the client instance to a particular replica Index.
        public static void LockClient(this DocumentClient client, uint replicaIndex)
        {
            client.initializeTask.Wait();
            if (client.StoreModel is ServerStoreModel serverStoreModel)
            {
                serverStoreModel.DefaultReplicaIndex = replicaIndex;
            }
        }

        public static void ForceAddressRefresh(this DocumentClient client, bool forceAddressRefresh)
        {
            client.initializeTask.Wait();
            if (client.StoreModel is ServerStoreModel serverStoreModel)
            {
                serverStoreModel.ForceAddressRefresh = forceAddressRefresh;
            }
        }

        //Returns the address of replica.
        public static string GetAddress(this DocumentClient client)
        {
            client.initializeTask.Wait();
            return (client.StoreModel as ServerStoreModel).LastReadAddress;
        }
    }
}