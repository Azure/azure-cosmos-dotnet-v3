//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;

    internal class PatchOperationBuilder<T>
    {
        private readonly T instance;
        private readonly List<Func<CosmosSerializer, PatchOperation>> deferredOperations = new List<Func<CosmosSerializer, PatchOperation>>();

        public PatchOperationBuilder(T instance, params Action<PatchOperationContext<T>>[] patches)
        {
            this.instance = instance;

            foreach (Action<PatchOperationContext<T>> patchAction in patches)
            {
                PatchOperationContext<T> context = new PatchOperationContext<T>(this.instance);
                patchAction(context);

                this.deferredOperations.Add(serializer => context.Build(serializer));
            }
        }

        public List<PatchOperation> Build(CosmosSerializer serializer)
        {
            List<PatchOperation> patchOperations = new List<PatchOperation>();

            foreach (Func<CosmosSerializer, PatchOperation> deferredOperation in this.deferredOperations)
            {
                patchOperations.Add(deferredOperation(serializer));
            }
            return patchOperations;
        }
    }
}
