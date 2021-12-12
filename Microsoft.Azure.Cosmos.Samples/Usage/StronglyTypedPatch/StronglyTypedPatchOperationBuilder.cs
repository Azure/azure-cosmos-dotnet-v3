namespace StronglyTypedPatch
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using Fasterflect;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    // TODO: convert to factory
    public class StronglyTypedPatchOperationBuilder<TObject>
    {
        private readonly List<PatchOperation> patchOperations = new();
        private readonly IContractResolver contractResolver;

        internal StronglyTypedPatchOperationBuilder(IContractResolver contractResolver)
        {
            this.contractResolver = contractResolver;
        }

        internal StronglyTypedPatchOperationBuilder(CosmosClient client) : this(GetResolver(client))
        {
        }

        private static IContractResolver GetResolver(CosmosClient client)
        {
            //TODO: remove Fasterflect
            JsonSerializerSettings serializerSettings = (JsonSerializerSettings)typeof(CosmosClient).Assembly
                .GetType("Microsoft.Azure.Cosmos.CosmosJsonDotNetSerializer")
                .CreateInstance(client.ClientOptions.SerializerOptions ?? new()) //TODO: use this new() behavior for custom serializers?
                .GetFieldValue("SerializerSettings");

            return serializerSettings.ContractResolver;
        }

        public IReadOnlyList<PatchOperation> PatchOperations => this.patchOperations;

        public StronglyTypedPatchOperationBuilder<TObject> WithAdd<TValue>(Expression<Func<TObject, TValue>> path, TValue value)
        {
            this.patchOperations.Add(PatchOperation.Add(this.GetJsonPointer(path), value));
            return this;
        }

        public StronglyTypedPatchOperationBuilder<TObject> WithRemove<TValue>(Expression<Func<TObject, TValue>> path)
        {
            this.patchOperations.Add(PatchOperation.Remove(this.GetJsonPointer(path)));
            return this;
        }

        public StronglyTypedPatchOperationBuilder<TObject> WithReplace<TValue>(Expression<Func<TObject, TValue>> path, TValue value)
        {
            this.patchOperations.Add(PatchOperation.Replace(this.GetJsonPointer(path), value));
            return this;
        }

        public StronglyTypedPatchOperationBuilder<TObject> WithSet<TValue>(Expression<Func<TObject, TValue>> path, TValue value)
        {
            this.patchOperations.Add(PatchOperation.Set(this.GetJsonPointer(path), value));
            return this;
        }

        public StronglyTypedPatchOperationBuilder<TObject> WithIncrement(Expression<Func<TObject, byte>> path, byte value)
        {
            this.patchOperations.Add(PatchOperation.Increment(this.GetJsonPointer(path), value));
            return this;
        }

        public StronglyTypedPatchOperationBuilder<TObject> WithIncrement(Expression<Func<TObject, short>> path, short value)
        {
            this.patchOperations.Add(PatchOperation.Increment(this.GetJsonPointer(path), value));
            return this;
        }

        public StronglyTypedPatchOperationBuilder<TObject> WithIncrement(Expression<Func<TObject, int>> path, int value)
        {
            this.patchOperations.Add(PatchOperation.Increment(this.GetJsonPointer(path), value));
            return this;
        }

        public StronglyTypedPatchOperationBuilder<TObject> WithIncrement(Expression<Func<TObject, long>> path, long value)
        {
            this.patchOperations.Add(PatchOperation.Increment(this.GetJsonPointer(path), value));
            return this;
        }

        public StronglyTypedPatchOperationBuilder<TObject> WithIncrement(Expression<Func<TObject, sbyte>> path, sbyte value)
        {
            this.patchOperations.Add(PatchOperation.Increment(this.GetJsonPointer(path), value));
            return this;
        }

        public StronglyTypedPatchOperationBuilder<TObject> WithIncrement(Expression<Func<TObject, ushort>> path, ushort value)
        {
            this.patchOperations.Add(PatchOperation.Increment(this.GetJsonPointer(path), value));
            return this;
        }

        public StronglyTypedPatchOperationBuilder<TObject> WithIncrement(Expression<Func<TObject, uint>> path, uint value)
        {
            this.patchOperations.Add(PatchOperation.Increment(this.GetJsonPointer(path), value));
            return this;
        }

        public StronglyTypedPatchOperationBuilder<TObject> WithIncrement(Expression<Func<TObject, ulong>> path, ulong value)
        {
            this.patchOperations.Add(PatchOperation.Increment(this.GetJsonPointer(path), value));
            return this;
        }

        public StronglyTypedPatchOperationBuilder<TObject> WithIncrement(Expression<Func<TObject, float>> path, float value)
        {
            this.patchOperations.Add(PatchOperation.Increment(this.GetJsonPointer(path), value));
            return this;
        }

        public StronglyTypedPatchOperationBuilder<TObject> WithIncrement(Expression<Func<TObject, double>> path, double value)
        {
            this.patchOperations.Add(PatchOperation.Increment(this.GetJsonPointer(path), value));
            return this;
        }

        public StronglyTypedPatchOperationBuilder<TObject> WithIncrement(Expression<Func<TObject, decimal>> path, decimal value)
        {
            this.patchOperations.Add(PatchOperation.Increment(this.GetJsonPointer(path), (double)value));
            return this;
        }

        private string GetJsonPointer(LambdaExpression expression)
        {
            //TODO: use expression visitor
            //TODO: handle indices assigned by variables, etc - what does Cosmos LINQ support?

            Stack<string> pathParts = new();

            Expression currentExpression = expression.Body;
            while (currentExpression is not ParameterExpression)
            {
                if (currentExpression is MemberExpression memberExpression)
                {
                    // Member access: fetch serialized name and pop
                    pathParts.Push(GetNameUnderContract(memberExpression.Member));
                    currentExpression = memberExpression.Expression;
                }
                else if (
                    currentExpression is BinaryExpression binaryExpression and { NodeType: ExpressionType.ArrayIndex }
                    && binaryExpression.Right is ConstantExpression arrayIndexConstantExpression
                )
                {
                    // Array index
                    pathParts.Push(GetIndex((int)arrayIndexConstantExpression.Value));
                    currentExpression = binaryExpression.Left;
                }
                else if (
                    currentExpression is MethodCallExpression callExpression and { Arguments: { Count: 1 }, Method: { Name: "get_Item" } }
                    && callExpression.Arguments[0] is ConstantExpression listIndexConstantExpression and { Type: { Name: nameof(Int32) } }
                )
                {
                    // IReadOnlyList index of other type
                    pathParts.Push(GetIndex((int)listIndexConstantExpression.Value));
                    currentExpression = callExpression.Object;
                }
                else
                {
                    throw new InvalidOperationException($"{currentExpression.GetType().Name} (at {currentExpression}) not supported");
                }
            }

            return "/" + string.Join("/", pathParts);

            //TODO: add cache for a single pass of ToUntyped
            string GetNameUnderContract(MemberInfo member)
            {
                JsonObjectContract contract = (JsonObjectContract)this.contractResolver.ResolveContract(member.DeclaringType);
                JsonProperty property = contract.Properties.Single(x => x.UnderlyingName == member.Name);
                return property.PropertyName;
            }

            string GetIndex(int index)
            {
                return index switch
                {
                    >= 0 => index.ToString(),
                    -1 => "-", //array append
                    _ => throw new ArgumentOutOfRangeException(nameof(index))
                };
            }
        }
    }
}
