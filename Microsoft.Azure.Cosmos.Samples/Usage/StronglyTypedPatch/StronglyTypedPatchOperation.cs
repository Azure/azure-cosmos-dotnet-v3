namespace StronglyTypedPatch
{
    using System;
    using System.Linq.Expressions;
    using Microsoft.Azure.Cosmos;

    public abstract class StronglyTypedPatchOperation<TObject>
    {
        public abstract PatchOperationType OperationType { get; }

        internal abstract object UntypedValue { get; }

        internal abstract LambdaExpression UntypedPath { get; }
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

        public override PatchOperationType OperationType { get; }

        public Expression<Func<TObject, TValue>> Path { get; }

        internal override object UntypedValue => this.Value;

        internal override LambdaExpression UntypedPath => this.Path;
    }
}
