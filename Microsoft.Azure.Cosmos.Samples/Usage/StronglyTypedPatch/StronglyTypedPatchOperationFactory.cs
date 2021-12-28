namespace StronglyTypedPatch
{
    using System;
    using System.Linq.Expressions;
    using Microsoft.Azure.Cosmos;

    public class StronglyTypedPatchOperationFactory<TObject>
    {
        private readonly JsonPointerBuilder pointerBuilder;

        internal StronglyTypedPatchOperationFactory(CosmosLinqSerializerOptions serializerOptions)
        {
            this.pointerBuilder = new JsonPointerBuilder(serializerOptions);
        }

        public StronglyTypedPatchOperationFactory(CosmosClient client) : this(GetOptions(client))
        {
        }

        private static CosmosLinqSerializerOptions GetOptions(CosmosClient client)
        {
            // see ContainerCore.Items.GetItemLinqQueryable for source of this
            return client.ClientOptions.SerializerOptions is { } options
                ? new() { PropertyNamingPolicy = options.PropertyNamingPolicy }
                : null;
        }

        public PatchOperation Add<TValue>(Expression<Func<TObject, TValue>> path, TValue value)
        {
            return PatchOperation.Add(this.pointerBuilder.Build(path), value);
        }

        public PatchOperation Remove<TValue>(Expression<Func<TObject, TValue>> path)
        {
            return PatchOperation.Remove(this.pointerBuilder.Build(path));
        }

        public PatchOperation Replace<TValue>(Expression<Func<TObject, TValue>> path, TValue value)
        {
            return PatchOperation.Replace(this.pointerBuilder.Build(path), value);
        }

        public PatchOperation Set<TValue>(Expression<Func<TObject, TValue>> path, TValue value)
        {
            return PatchOperation.Set(this.pointerBuilder.Build(path), value);
        }

        public PatchOperation Increment(Expression<Func<TObject, byte>> path, byte value)
        {
            return PatchOperation.Increment(this.pointerBuilder.Build(path), value);
        }

        public PatchOperation Increment(Expression<Func<TObject, short>> path, short value)
        {
            return PatchOperation.Increment(this.pointerBuilder.Build(path), value);
        }

        public PatchOperation Increment(Expression<Func<TObject, int>> path, int value)
        {
            return PatchOperation.Increment(this.pointerBuilder.Build(path), value);
        }

        public PatchOperation Increment(Expression<Func<TObject, long>> path, long value)
        {
            return PatchOperation.Increment(this.pointerBuilder.Build(path), value);
        }

        public PatchOperation Increment(Expression<Func<TObject, sbyte>> path, sbyte value)
        {
            return PatchOperation.Increment(this.pointerBuilder.Build(path), value);
        }

        public PatchOperation Increment(Expression<Func<TObject, ushort>> path, ushort value)
        {
            return PatchOperation.Increment(this.pointerBuilder.Build(path), value);
        }

        public PatchOperation Increment(Expression<Func<TObject, uint>> path, uint value)
        {
            return PatchOperation.Increment(this.pointerBuilder.Build(path), value);
        }

        public PatchOperation Increment(Expression<Func<TObject, ulong>> path, ulong value)
        {
            return PatchOperation.Increment(this.pointerBuilder.Build(path), value);
        }

        public PatchOperation Increment(Expression<Func<TObject, float>> path, float value)
        {
            return PatchOperation.Increment(this.pointerBuilder.Build(path), value);
        }

        public PatchOperation Increment(Expression<Func<TObject, double>> path, double value)
        {
            return PatchOperation.Increment(this.pointerBuilder.Build(path), value);
        }

        public PatchOperation Increment(Expression<Func<TObject, decimal>> path, decimal value)
        {
            return PatchOperation.Increment(this.pointerBuilder.Build(path), (double)value);
        }
    }
}
