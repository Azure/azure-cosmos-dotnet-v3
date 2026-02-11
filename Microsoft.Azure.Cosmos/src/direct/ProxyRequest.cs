//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
#if COSMOSCLIENT
    using Microsoft.Azure.Cosmos.Rntbd;
#endif

    internal class ProxyRequest
    {
        private static readonly int[] expectedPositionsForTokensDuringReorder;
        private static readonly int[] positionsOfTokensToMakeOptionalAfterReorder;
        private static readonly Action<RntbdToken, RntbdConstants.Request>[] settersForFieldsToMakeOptional;

        static ProxyRequest()
        {
            // Proxy expects the first tokens to be the ones needed for routing
            // This is done as an optimization to avoid parsing the entire request to get the routing and authorization information
            RntbdConstants.RequestIdentifiers[] firstTokenTypes = new[]
            {
                // Proxy expects EPK to be first
                RntbdConstants.RequestIdentifiers.EffectivePartitionKey,

                // The rest of the tokens that Proxy uses are moved to first positions for faster parsing in Proxy side
                RntbdConstants.RequestIdentifiers.StartEpkHash,
                RntbdConstants.RequestIdentifiers.EndEpkHash,
                RntbdConstants.RequestIdentifiers.GlobalDatabaseAccountName,
                RntbdConstants.RequestIdentifiers.RegionalDatabaseAccountName,
                RntbdConstants.RequestIdentifiers.DatabaseName,
                RntbdConstants.RequestIdentifiers.CollectionName,
                RntbdConstants.RequestIdentifiers.CollectionRid,
                RntbdConstants.RequestIdentifiers.ResourceId,

                // Fields used for AuthZ
                RntbdConstants.RequestIdentifiers.PayloadPresent,
                RntbdConstants.RequestIdentifiers.DocumentName,
                RntbdConstants.RequestIdentifiers.AuthorizationToken,
                RntbdConstants.RequestIdentifiers.Date,
            };

            (RntbdConstants.RequestIdentifiers, Action<RntbdToken, RntbdConstants.Request>)[] tokensToMakeOptional = new (RntbdConstants.RequestIdentifiers, Action<RntbdToken, RntbdConstants.Request>)[]
            {
                // For Proxy, transportRequestId and replicapath are optional.
                (RntbdConstants.RequestIdentifiers.ReplicaPath, (token, request) => request.replicaPath = token),
                (RntbdConstants.RequestIdentifiers.TransportRequestID, (token, request) => request.transportRequestID = token),
            };

            ProxyRequest.expectedPositionsForTokensDuringReorder = new int[firstTokenTypes.Length];
            RntbdConstants.Request rntbdRequest = new();
            for (int i = 0; i < firstTokenTypes.Length; i++)
            {
                RntbdConstants.RequestIdentifiers tokenType = firstTokenTypes[i];
                int index = Array.FindIndex(rntbdRequest.tokens, x => x?.GetTokenIdentifier() == (ushort)tokenType);
                ProxyRequest.expectedPositionsForTokensDuringReorder[i] = index;
                ProxyRequest.SwapTokens(rntbdRequest.tokens, i, index);
            }

            Dictionary<RntbdConstants.RequestIdentifiers, int> indexes = new();
            for (int i = 0; i < rntbdRequest.tokens.Length; i++)
            {
                if (rntbdRequest.tokens[i] == null)
                {
                    continue;
                }

                RntbdConstants.RequestIdentifiers tokenType = (RntbdConstants.RequestIdentifiers)rntbdRequest.tokens[i].GetTokenIdentifier();
                indexes[tokenType] = i;
            }

            ProxyRequest.positionsOfTokensToMakeOptionalAfterReorder = new int[tokensToMakeOptional.Length];
            ProxyRequest.settersForFieldsToMakeOptional = new Action<RntbdToken, RntbdConstants.Request>[tokensToMakeOptional.Length];
            for (int i = 0; i < tokensToMakeOptional.Length; i++)
            {
                ProxyRequest.positionsOfTokensToMakeOptionalAfterReorder[i] = indexes[tokensToMakeOptional[i].Item1];
                ProxyRequest.settersForFieldsToMakeOptional[i] = tokensToMakeOptional[i].Item2;
            }
        }

        public RntbdConstants.Request RntbdRequest { get; } = new();
        public ProxyRequest()
        {
            for (int i = 0; i < ProxyRequest.expectedPositionsForTokensDuringReorder.Length; i++)
            {
                ProxyRequest.SwapTokens(RntbdRequest.tokens, i, ProxyRequest.expectedPositionsForTokensDuringReorder[i]);
            }

            // Make optional
            for (int i = 0; i < ProxyRequest.positionsOfTokensToMakeOptionalAfterReorder.Length; i++)
            {
                int expectedTokenPosition = ProxyRequest.positionsOfTokensToMakeOptionalAfterReorder[i];
                RntbdToken originalToken = RntbdRequest.tokens[expectedTokenPosition];
                RntbdRequest.tokens[expectedTokenPosition] = new RntbdToken(false, originalToken.GetTokenType(), originalToken.GetTokenIdentifier());
                ProxyRequest.settersForFieldsToMakeOptional[i](RntbdRequest.tokens[expectedTokenPosition], RntbdRequest);
            }
        }

        public void Reset()
        {
            RntbdRequest.Reset();
        }

        private static void SwapTokens(RntbdToken[] tokens, int i, int j)
        {
            if (i != j)
            {
                (tokens[j], tokens[i]) = (tokens[i], tokens[j]);
            }
        }

        internal sealed class ObjectPool
        {
            public static readonly ObjectPool Instance = new ObjectPool();

            private readonly ConcurrentQueue<ProxyRequest> entities = new ConcurrentQueue<ProxyRequest>();

            private ObjectPool()
            {
            }

            public EntityOwner Get()
            {
                if (this.entities.TryDequeue(out ProxyRequest entity))
                {
                    return new EntityOwner(entity);
                }

                return new EntityOwner(new ProxyRequest());
            }

            private void Return(ProxyRequest entity)
            {
                entity.Reset();
                this.entities.Enqueue(entity);
            }

            public readonly struct EntityOwner : IDisposable
            {
                public EntityOwner(ProxyRequest entity)
                {
                    this.Entity = entity;
                }

                public ProxyRequest Entity { get; }

                public void Dispose()
                {
                    if (this.Entity != null)
                    {
                        ObjectPool.Instance.Return(this.Entity);
                    }
                }
            }
        }
    }
}