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

    public class StronglyTypedPatchOperationFactory<TObject>
    {
        private readonly IContractResolver contractResolver;

        internal StronglyTypedPatchOperationFactory(IContractResolver contractResolver)
        {
            this.contractResolver = contractResolver;
        }

        internal StronglyTypedPatchOperationFactory(CosmosClient client) : this(GetResolver(client))
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

        public PatchOperation Add<TValue>(Expression<Func<TObject, TValue>> path, TValue value)
        {
            return PatchOperation.Add(this.GetJsonPointer(path), value);
        }

        public PatchOperation Remove<TValue>(Expression<Func<TObject, TValue>> path)
        {
            return PatchOperation.Remove(this.GetJsonPointer(path));
        }

        public PatchOperation Replace<TValue>(Expression<Func<TObject, TValue>> path, TValue value)
        {
            return PatchOperation.Replace(this.GetJsonPointer(path), value);
        }

        public PatchOperation Set<TValue>(Expression<Func<TObject, TValue>> path, TValue value)
        {
            return PatchOperation.Set(this.GetJsonPointer(path), value);
        }

        public PatchOperation Increment(Expression<Func<TObject, byte>> path, byte value)
        {
            return PatchOperation.Increment(this.GetJsonPointer(path), value);
        }

        public PatchOperation Increment(Expression<Func<TObject, short>> path, short value)
        {
            return PatchOperation.Increment(this.GetJsonPointer(path), value);
        }

        public PatchOperation Increment(Expression<Func<TObject, int>> path, int value)
        {
            return PatchOperation.Increment(this.GetJsonPointer(path), value);
        }

        public PatchOperation Increment(Expression<Func<TObject, long>> path, long value)
        {
            return PatchOperation.Increment(this.GetJsonPointer(path), value);
        }

        public PatchOperation Increment(Expression<Func<TObject, sbyte>> path, sbyte value)
        {
            return PatchOperation.Increment(this.GetJsonPointer(path), value);
        }

        public PatchOperation Increment(Expression<Func<TObject, ushort>> path, ushort value)
        {
            return PatchOperation.Increment(this.GetJsonPointer(path), value);
        }

        public PatchOperation Increment(Expression<Func<TObject, uint>> path, uint value)
        {
            return PatchOperation.Increment(this.GetJsonPointer(path), value);
        }

        public PatchOperation Increment(Expression<Func<TObject, ulong>> path, ulong value)
        {
            return PatchOperation.Increment(this.GetJsonPointer(path), value);
        }

        public PatchOperation Increment(Expression<Func<TObject, float>> path, float value)
        {
            return PatchOperation.Increment(this.GetJsonPointer(path), value);
        }

        public PatchOperation Increment(Expression<Func<TObject, double>> path, double value)
        {
            return PatchOperation.Increment(this.GetJsonPointer(path), value);
        }

        public PatchOperation Increment(Expression<Func<TObject, decimal>> path, decimal value)
        {
            return PatchOperation.Increment(this.GetJsonPointer(path), (double)value);
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
