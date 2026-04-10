//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Fluent
{
    using System;
    using System.Collections.ObjectModel;

    /// <summary>
    /// Computed Properties fluent definition.
    /// </summary>
    /// <seealso cref="ComputedProperty"/>
    public class ComputedPropertiesDefinition<T>
    {
        private readonly Collection<ComputedProperty> computedProperties = new Collection<ComputedProperty>();
        private readonly T parent;
        private readonly Action<Collection<ComputedProperty>> attachCallback;

        internal ComputedPropertiesDefinition(
            T parent,
            Action<Collection<ComputedProperty>> attachCallback)
        {
            this.parent = parent;
            this.attachCallback = attachCallback;
        }

        /// <summary>
        /// Adds a computed property to the current <see cref="ComputedPropertiesDefinition{T}"/>
        /// </summary>
        /// <param name="name">Name of the computed property</param>
        /// <param name="query">Query for the computed property values</param>
        /// <returns>An instance of the current <see cref="ComputedPropertiesDefinition{T}"/></returns>
        public ComputedPropertiesDefinition<T> WithComputedProperty(string name, string query)
        {
            this.computedProperties.Add(
                new ComputedProperty
                {
                    Name = name,
                    Query = query
                });

            return this;
        }

        /// <summary>
        /// Applies the current definition to the parent.
        /// </summary>
        /// <returns>An instance of the parent.</returns>
        public T Attach()
        {
            this.attachCallback(this.computedProperties);
            return this.parent;
        }
    }
}
