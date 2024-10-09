//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Linq;
    using System.Linq.Expressions;
    using Newtonsoft.Json.Linq;

    internal class PatchOperationContext<T>
    {
        private readonly T instance;
        private PatchOperationType operationType;
        private string path;
        private object value;
        private string property;

        public PatchOperationContext(T instance)
        {
            this.instance = instance;
        }

        public PatchOperationContext<T> SetOperationType(PatchOperationType operationType)
        {
            this.operationType = operationType;
            return this;
        }

        public PatchOperationContext<T> Property<TProperty>(Expression<Func<T, TProperty>> propertyExpression)
        {
            this.path = this.BuildPathFromExpression(propertyExpression);
            this.value = this.GetValueFromExpression(propertyExpression);
            return this;
        }

        // Add the AddIndex method to allow for adding an index to the path
        public PatchOperationContext<T> AddIndex(int index)
        {
            this.path += $"/{index}";
            return this;
        }

        private string BuildPathFromExpression<TProperty>(Expression<Func<T, TProperty>> expression)
        {
            if (expression.Body is MemberExpression memberExpression)
            {
                this.property = memberExpression.Member.Name;
                return "/" + memberExpression.Member.Name;
            }

            if (expression.Body is UnaryExpression unaryExpression && unaryExpression.Operand is MemberExpression operand)
            {
                this.property = operand.Member.Name;
                return "/" + operand.Member.Name;
            }

            throw new ArgumentException("Invalid expression");
        }

        private object GetValueFromExpression<TProperty>(Expression<Func<T, TProperty>> expression)
        {
            return expression.Compile().Invoke(this.instance);
        }

        public PatchOperation Build(CosmosSerializer serializer)
        {
            // Serialize the entire instance, then extract the value for the specific property
            System.IO.Stream stream = serializer.ToStream(this.instance);
            JObject serializedInstance = serializer.FromStream<JObject>(stream);

            // Extract the value from the serialized instance using the path
            // JProperty jProperty = serializedInstance.Properties().FirstOrDefault(p => string.Equals(p.Name, this.property, StringComparison.OrdinalIgnoreCase));
            JProperty jProperty = FindProperty(serializedInstance, this.property);
            
            this.path = this.path.Replace(this.path.Trim('/'), jProperty.Name);
            this.value = jProperty.Value;

            return PatchOperation.Add(this.path, this.value);
        }

        private JObject SerializeValue(CosmosSerializer serializer, object value)
        {
            System.IO.Stream asStream = serializer.ToStream(value);

            return serializer.FromStream<JObject>(asStream);
        }

        public static JProperty FindProperty(JObject jsonObject, string propertyName)
        {
            return FindPropertyRecursive(jsonObject, propertyName, StringComparison.OrdinalIgnoreCase);
        }

        private static JProperty FindPropertyRecursive(JToken token, string propertyName, StringComparison comparison)
        {
            if (token is JObject obj)
            {
                foreach (JProperty property in obj.Properties())
                {
                    if (string.Equals(property.Name, propertyName, comparison))
                    {
                        return property;
                    }

                    JProperty found = FindPropertyRecursive(property.Value, propertyName, comparison);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }
            else if (token is JArray array)
            {
                foreach (JToken item in array)
                {
                    JProperty found = FindPropertyRecursive(item, propertyName, comparison);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }
            return null;
        }
    }
}
