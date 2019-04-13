namespace Microsoft.Azure.Cosmos.CosmosElements.Patchable
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Json;

    internal sealed class PatchableCosmosArray : PatchableCosmosElement
    {
        private readonly List<PatchableUnion> items;
        private readonly List<ArrayPatchOperation> patchOperations;

        private PatchableCosmosArray(CosmosArray cosmosArray)
            : base(PatchableCosmosElementType.Array)
        {
            if (cosmosArray == null)
            {
                throw new ArgumentNullException($"{nameof(cosmosArray)}");
            }

            this.items = new List<PatchableUnion>();
            foreach (CosmosElement arrayItem in cosmosArray)
            {
                items.Add(PatchableUnion.Create(arrayItem));
            }

            this.patchOperations = new List<ArrayPatchOperation>();
        }

        public PatchableCosmosElement this[int index]
        {
            get
            {
                return this.items[index].PatchableCosmosElement;
            }
        }

        public int Count => this.items.Count;

        public static PatchableCosmosArray Create(CosmosArray cosmosArray)
        {
            return new PatchableCosmosArray(cosmosArray);
        }

        public void Add(int index, CosmosElement item)
        {
            if (item == null)
            {
                throw new ArgumentNullException($"Can not {nameof(Add)} a null {nameof(CosmosElement)} into a {nameof(PatchableCosmosArray)}");
            }

            this.items.Insert(index, PatchableUnion.Create(item));
            this.patchOperations.Add(ArrayPatchOperation.CreateAddOperation(index, item));
        }

        public void Remove(int index)
        {
            this.items.RemoveAt(index);
            this.patchOperations.Add(ArrayPatchOperation.CreateRemoveOperation(index));
        }

        public void Replace(int index, CosmosElement item)
        {
            if (item == null)
            {
                throw new ArgumentNullException($"Can not {nameof(Replace)} an element with a null {nameof(CosmosElement)} in a {nameof(PatchableCosmosArray)}");
            }

            this.items[index] = PatchableUnion.Create(item);
            this.patchOperations.Add(ArrayPatchOperation.CreateReplaceOperation(index, item));
        }

        public override CosmosElement ToCosmosElement()
        {
            return new PatchableCosmosArrayWrapper(this);
        }

        private sealed class PatchableCosmosArrayWrapper : CosmosArray
        {
            private readonly PatchableCosmosArray patchableCosmosArray;
            public PatchableCosmosArrayWrapper(PatchableCosmosArray patchableCosmosArray)
            {
                if (patchableCosmosArray == null)
                {
                    throw new ArgumentNullException($"{nameof(patchableCosmosArray)}");
                }

                this.patchableCosmosArray = patchableCosmosArray;
            }

            public override CosmosElement this[int index]
            {
                get
                {
                    PatchableUnion patchableUnion = this.patchableCosmosArray.items[index];
                    return patchableUnion.CosmosElement;
                }
            }

            public override int Count => this.patchableCosmosArray.items.Count;

            public override IEnumerator<CosmosElement> GetEnumerator()
            {
                return this.patchableCosmosArray
                    .items
                    .Select((patchableUnion) => patchableUnion.CosmosElement)
                    .GetEnumerator();
            }

            public override PatchableCosmosElement ToPatchable()
            {
                return this.patchableCosmosArray;
            }

            public override void WriteTo(IJsonWriter jsonWriter)
            {
                jsonWriter.WriteArrayStart();

                foreach (CosmosElement cosmosElement in this)
                {
                    cosmosElement.WriteTo(jsonWriter);
                }

                jsonWriter.WriteArrayEnd();
            }
        }

        private struct ArrayPatchOperation
        {
            private ArrayPatchOperation(
                PatchOperation patchOperation,
                int index,
                CosmosElement item)
            {
                this.OperationType = patchOperation;
                this.Index = index;
                this.Item = item;
            }

            public PatchOperation OperationType
            {
                get;
            }

            public int Index
            {
                get;
            }

            public CosmosElement Item
            {
                get;
            }

            public static ArrayPatchOperation CreateAddOperation(int index, CosmosElement item)
            {
                return new ArrayPatchOperation(PatchOperation.Add, index, item);
            }

            public static ArrayPatchOperation CreateRemoveOperation(int index)
            {
                return new ArrayPatchOperation(PatchOperation.Remove, index, item: null);
            }

            public static ArrayPatchOperation CreateReplaceOperation(int index, CosmosElement item)
            {
                return new ArrayPatchOperation(PatchOperation.Replace, index, item);
            }
        }
    }
}
