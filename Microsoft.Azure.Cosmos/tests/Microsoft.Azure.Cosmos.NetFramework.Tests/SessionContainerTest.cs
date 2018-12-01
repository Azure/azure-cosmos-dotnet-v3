//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Internal;

    /// <summary>
    /// Tests for <see cref="SessionContainer"/> class.
    /// </summary>
    [TestClass]
    public class SessionContainerTest
    {
        /// <summary>
        /// Simple test for <see cref="SessionContainer"/> class.
        /// </summary>
        [TestMethod]
        public void TestSessionContainer()
        {
            this.TestSessionContainer((int n) => new SimpleSessionToken(n));
            this.TestSessionContainer((int n) =>
            {
                ISessionToken sessionToken;
                Assert.IsTrue(VectorSessionToken.TryCreate("1#100#4=90#5=" + n, out sessionToken));
                return sessionToken;
            });
        }

        private void TestSessionContainer(Func<int, ISessionToken> getSessionToken)
        {
            ConcurrentDictionary<UInt64, ConcurrentDictionary<string, ISessionToken>> sessionTokens = new ConcurrentDictionary<ulong, ConcurrentDictionary<string, ISessionToken>>();
            ConcurrentDictionary<string, ConcurrentDictionary<string, ISessionToken>> sessionTokensNameBased = new ConcurrentDictionary<string, ConcurrentDictionary<string, ISessionToken>>();

            int numCollections = 2;
            int numPartitionKeyRangeIds = 5;

            for(int i = 0; i < numCollections; i++)
            {
                string collName = "dbs/db1/colls/collName_" + i;
                ulong collId = (ulong)i;

                ConcurrentDictionary<string, ISessionToken> idToTokenMap = new ConcurrentDictionary<string, ISessionToken>();
                ConcurrentDictionary<string, ISessionToken> idToTokenMapNameBased = new ConcurrentDictionary<string, ISessionToken>();

                for(int j = 0; j < numPartitionKeyRangeIds; j++)
                {
                    string range = "range_" + j;
                    ISessionToken token = getSessionToken(j);

                    bool successFlag = idToTokenMap.TryAdd(range, token) && idToTokenMapNameBased.TryAdd(range, token);
                    
                    if(!successFlag)
                    {
                        throw new InvalidOperationException("Add should not fail!");
                    }
                }

                bool successFlag2 = sessionTokens.TryAdd(collId, idToTokenMap) && sessionTokensNameBased.TryAdd(collName, idToTokenMapNameBased);

                if(!successFlag2)
                {
                    throw new InvalidOperationException("Add should not fail!");
                }

            }

            SessionContainer sessionContainer = new SessionContainer("127.0.0.1", sessionTokens, sessionTokensNameBased);

            using(DocumentServiceRequest request =
                 DocumentServiceRequest.Create(
                     Cosmos.Internal.OperationType.ReadFeed,
                     Cosmos.Internal.ResourceType.Collection,
                     new Uri("https://foo.com/dbs/db1/colls/collName_1", UriKind.Absolute),
                     new MemoryStream(Encoding.UTF8.GetBytes("content1")),
                     AuthorizationTokenType.PrimaryMasterKey,
                     null))
            {
                ISessionToken sessionToken = sessionContainer.ResolvePartitionLocalSessionToken(request, "range_1");
                Assert.IsTrue(sessionToken.Equals(getSessionToken(1)));

                DocumentServiceRequestContext dsrContext = new DocumentServiceRequestContext();
                PartitionKeyRange resolvedPKRange = new PartitionKeyRange();
                resolvedPKRange.Id = "range_"+(numPartitionKeyRangeIds+10);
                resolvedPKRange.Parents = new Collection<string>(new List<string> {"range_2", "range_x"});
                dsrContext.ResolvedPartitionKeyRange = resolvedPKRange;
                request.RequestContext = dsrContext;

                sessionToken = sessionContainer.ResolvePartitionLocalSessionToken(request, resolvedPKRange.Id);
                Assert.IsTrue(sessionToken.Equals(getSessionToken(2)));
            }
        }
    }
}
