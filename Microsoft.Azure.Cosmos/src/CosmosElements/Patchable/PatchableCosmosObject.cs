namespace Microsoft.Azure.Cosmos.CosmosElements.Patchable
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Json;

    internal sealed class PatchableCosmosObject : PatchableCosmosElement
    {
        private readonly Dictionary<string, PatchableUnion> properties;
        private readonly List<ObjectPatchOperation> patchOperations;

        private PatchableCosmosObject(CosmosObject cosmosObject)
            : base(PatchableCosmosElementType.Object)
        {
            if (cosmosObject == null)
            {
                throw new ArgumentNullException($"{nameof(cosmosObject)}");
            }

            this.properties = new Dictionary<string, PatchableUnion>();
            foreach (KeyValuePair<string, CosmosElement> kvp in cosmosObject)
            {
                string key = kvp.Key;
                CosmosElement value = kvp.Value;

                this.properties[key] = PatchableUnion.Create(value);
            }

            this.patchOperations = new List<ObjectPatchOperation>();
        }

        public static PatchableCosmosObject Create(CosmosObject cosmosObject)
        {
            return new PatchableCosmosObject(cosmosObject);
        }

        public PatchableCosmosElement this[string key]
        {
            get
            {
                return this.properties[key].PatchableCosmosElement;
            }
        }

        public void Add(string key, CosmosElement value)
        {

            this.properties.Add(key, PatchableUnion.Create(value));
            this.patchOperations.Add(ObjectPatchOperation.CreateAddOperation(key, value));
        }

        public void Remove(string key)
        {
            bool found = this.properties.Remove(key);
            if (!found)
            {
                throw new KeyNotFoundException($"Could not {nameof(Remove)} key: {key} in {nameof(PatchableCosmosObject)}");
            }

            this.patchOperations.Add(ObjectPatchOperation.CreateRemoveOperation(key));
        }

        public void Replace(string key, CosmosElement value)
        {
            if (!this.properties.ContainsKey(key))
            {
                throw new KeyNotFoundException($"Could not {nameof(Replace)} key: {key} in {nameof(PatchableCosmosObject)}");
            }

            this.properties[key] = PatchableUnion.Create(value);
            this.patchOperations.Add(ObjectPatchOperation.CreateReplaceOperation(key, value));
        }

        public override CosmosElement ToCosmosElement()
        {
            return new PatchableCosmosObjectWrapper(this);
        }

        public bool ContainsKey(string key)
        {
            return this.properties.ContainsKey(key);
        }

        private sealed class PatchableCosmosObjectWrapper : CosmosObject
        {
            private readonly PatchableCosmosObject patchableCosmosObject;

            public PatchableCosmosObjectWrapper(PatchableCosmosObject patchableCosmosObject)
            {
                if (patchableCosmosObject == null)
                {
                    throw new ArgumentNullException($"{nameof(patchableCosmosObject)}");
                }

                this.patchableCosmosObject = patchableCosmosObject;
            }

            public override CosmosElement this[string key] => this.patchableCosmosObject.properties[key].CosmosElement;

            public override IEnumerable<string> Keys => this.patchableCosmosObject.properties.Keys;

            public override IEnumerable<CosmosElement> Values => this.patchableCosmosObject
                .properties
                .Values
                .Select((patchableUnion) => patchableUnion.CosmosElement);

            public override int Count => this.patchableCosmosObject.properties.Count;

            public override bool ContainsKey(string key)
            {
                return this.patchableCosmosObject.properties.ContainsKey(key);
            }

            public override IEnumerator<KeyValuePair<string, CosmosElement>> GetEnumerator()
            {
                return this.patchableCosmosObject
                    .properties
                    .Select((kvp) => new KeyValuePair<string, CosmosElement>(kvp.Key, kvp.Value.CosmosElement))
                    .GetEnumerator();
            }

            public override bool TryGetValue(string key, out CosmosElement value)
            {
                value = null;
                if (this.patchableCosmosObject.properties.TryGetValue(key, out PatchableUnion patchableUnion))
                {
                    value = patchableUnion.CosmosElement;
                }

                return value != null;
            }

            public override PatchableCosmosElement ToPatchable()
            {
                return this.patchableCosmosObject;
            }

            public override void WriteTo(IJsonWriter jsonWriter)
            {
                jsonWriter.WriteObjectStart();

                foreach (KeyValuePair<string, CosmosElement> kvp in this)
                {
                    jsonWriter.WriteFieldName(kvp.Key);
                    kvp.Value.WriteTo(jsonWriter);
                }

                jsonWriter.WriteObjectEnd();
            }
        }

        private struct ObjectPatchOperation
        {
            private ObjectPatchOperation(
                PatchOperation patchOperation,
                string key,
                CosmosElement value)
            {
                this.OperationType = patchOperation;
                this.Key = key;
                this.Value = value;
            }

            public PatchOperation OperationType
            {
                get;
            }

            public string Key
            {
                get;
            }

            public CosmosElement Value
            {
                get;
            }

            public static ObjectPatchOperation CreateAddOperation(string key, CosmosElement value)
            {
                return new ObjectPatchOperation(PatchOperation.Add, key, value);
            }

            public static ObjectPatchOperation CreateRemoveOperation(string key)
            {
                return new ObjectPatchOperation(PatchOperation.Remove, key, value: null);
            }

            public static ObjectPatchOperation CreateReplaceOperation(string key, CosmosElement value)
            {
                return new ObjectPatchOperation(PatchOperation.Replace, key, value);
            }
        }
    }
}
