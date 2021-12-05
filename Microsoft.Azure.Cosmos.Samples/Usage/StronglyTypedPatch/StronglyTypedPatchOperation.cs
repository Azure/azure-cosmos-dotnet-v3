namespace StronglyTypedPatch
{
    using System;
    using System.Linq.Expressions;
    using Microsoft.Azure.Cosmos;

    public abstract class StronglyTypedPatchOperation<TObject>
    {

    }

    public class StronglyTypedPatchOperation<TObject, TValue> : StronglyTypedPatchOperation<TObject>
    {
        internal StronglyTypedPatchOperation(
            TValue value,
            PatchOperationType operationType,
            Expression<Func<TObject, TValue>> path)
        {
            this.Value = value;
            this.OperationType = operationType;
            this.Path = path;
        }

        public TValue Value { get; }

        public PatchOperationType OperationType { get; }

        public Expression<Func<TObject, TValue>> Path { get; }
    }
}
