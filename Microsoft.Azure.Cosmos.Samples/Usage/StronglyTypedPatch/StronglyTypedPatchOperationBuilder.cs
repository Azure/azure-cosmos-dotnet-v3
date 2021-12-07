namespace StronglyTypedPatch
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using Microsoft.Azure.Cosmos;
    using Newtonsoft.Json.Serialization;

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

        internal List<PatchOperation> ToUntyped(CosmosClient client)
        {
            //TODO: get resolver from client
            throw new NotImplementedException();
        }

        internal List<PatchOperation> ToUntyped(IContractResolver resolver)
        {
            //TODO: create patch operations of tpye PatchOperation<T>, not PatchOperation<object>
            return this.stronglyTypedPatchOperations.Select(operation =>
            {
                string path = GetJsonPointer(resolver, operation.UntypedPath);
                return operation.OperationType switch
                {
                    PatchOperationType.Add => PatchOperation.Add(path, operation.UntypedValue),
                    PatchOperationType.Remove => PatchOperation.Remove(path),
                    PatchOperationType.Replace => PatchOperation.Replace(path, operation.UntypedValue),
                    PatchOperationType.Set => PatchOperation.Set(path, operation.UntypedValue),
                    PatchOperationType.Increment => operation.UntypedValue is float or double or decimal
                        ? PatchOperation.Increment(path, (double)operation.UntypedValue)
                        : PatchOperation.Increment(path, (long)operation.UntypedValue),
                    _ => throw new ArgumentOutOfRangeException(),
                };
            }).ToList();
        }

        private static string GetJsonPointer(IContractResolver resolver, LambdaExpression expression)
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
                JsonObjectContract contract = (JsonObjectContract)resolver.ResolveContract(member.DeclaringType);
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
