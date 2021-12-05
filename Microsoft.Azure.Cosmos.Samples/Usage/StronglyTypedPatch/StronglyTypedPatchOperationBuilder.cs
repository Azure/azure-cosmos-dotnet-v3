namespace StronglyTypedPatch
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using Microsoft.Azure.Cosmos;

    public class StronglyTypedPatchOperationBuilder<TObject>
    {
        private readonly List<StronglyTypedPatchOperation<TObject>> stronglyTypedPatchOperations = new();

        public IReadOnlyList<StronglyTypedPatchOperation<TObject>> StronglyTypedPatchOperations => this.stronglyTypedPatchOperations;

        public StronglyTypedPatchOperationBuilder<TObject> WithAdd<TValue>(Expression<Func<TObject, TValue>> path, TValue value)
        {
            this.stronglyTypedPatchOperations.Add(new StronglyTypedPatchOperation<TObject, TValue>(value, PatchOperationType.Add, path));
            return this;
        }

        public StronglyTypedPatchOperationBuilder<TObject> WithRemove<TValue>(Expression<Func<TObject, TValue>> path)
        {
            this.stronglyTypedPatchOperations.Add(new StronglyTypedPatchOperation<TObject, TValue>(default, PatchOperationType.Remove, path));
            return this;
        }

        public StronglyTypedPatchOperationBuilder<TObject> WithReplace<TValue>(Expression<Func<TObject, TValue>> path, TValue value)
        {
            this.stronglyTypedPatchOperations.Add(new StronglyTypedPatchOperation<TObject, TValue>(value, PatchOperationType.Replace, path));
            return this;
        }

        public StronglyTypedPatchOperationBuilder<TObject> WithSet<TValue>(Expression<Func<TObject, TValue>> path, TValue value)
        {
            this.stronglyTypedPatchOperations.Add(new StronglyTypedPatchOperation<TObject, TValue>(value, PatchOperationType.Set, path));
            return this;
        }

        public StronglyTypedPatchOperationBuilder<TObject> WithIncrement(Expression<Func<TObject, byte>> path, byte value)
        {
            return this.WithIncrement<byte>(path, value);
        }

        public StronglyTypedPatchOperationBuilder<TObject> WithIncrement(Expression<Func<TObject, short>> path, short value)
        {
            return this.WithIncrement<short>(path, value);
        }

        public StronglyTypedPatchOperationBuilder<TObject> WithIncrement(Expression<Func<TObject, int>> path, int value)
        {
            return this.WithIncrement<int>(path, value);
        }

        public StronglyTypedPatchOperationBuilder<TObject> WithIncrement(Expression<Func<TObject, long>> path, long value)
        {
            return this.WithIncrement<long>(path, value);
        }

        public StronglyTypedPatchOperationBuilder<TObject> WithIncrement(Expression<Func<TObject, sbyte>> path, sbyte value)
        {
            return this.WithIncrement<sbyte>(path, value);
        }

        public StronglyTypedPatchOperationBuilder<TObject> WithIncrement(Expression<Func<TObject, ushort>> path, ushort value)
        {
            return this.WithIncrement<ushort>(path, value);
        }

        public StronglyTypedPatchOperationBuilder<TObject> WithIncrement(Expression<Func<TObject, uint>> path, uint value)
        {
            return this.WithIncrement<uint>(path, value);
        }

        public StronglyTypedPatchOperationBuilder<TObject> WithIncrement(Expression<Func<TObject, ulong>> path, ulong value)
        {
            return this.WithIncrement<ulong>(path, value);
        }

        public StronglyTypedPatchOperationBuilder<TObject> WithIncrement(Expression<Func<TObject, float>> path, float value)
        {
            return this.WithIncrement<float>(path, value);
        }

        public StronglyTypedPatchOperationBuilder<TObject> WithIncrement(Expression<Func<TObject, double>> path, double value)
        {
            return this.WithIncrement<double>(path, value);
        }

        public StronglyTypedPatchOperationBuilder<TObject> WithIncrement(Expression<Func<TObject, decimal>> path, decimal value)
        {
            return this.WithIncrement<decimal>(path, value);
        }

        private StronglyTypedPatchOperationBuilder<TObject> WithIncrement<TValue>(Expression<Func<TObject, TValue>> path, TValue value)
        {
            this.stronglyTypedPatchOperations.Add(new StronglyTypedPatchOperation<TObject, TValue>(value, PatchOperationType.Increment, path));
            return this;
        }

        public List<PatchOperation> ToUntyped(CosmosClient client)
        {
            throw new NotImplementedException();
        }
    }
}
