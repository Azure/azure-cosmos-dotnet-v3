//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    /// <summary>
    /// Base class for Header handling.
    /// </summary>
    public abstract class CosmosMessageHeadersBase : IEnumerable
    {
        private readonly Lazy<CosmosMessageHeadersInternal> messageHeaders;

        /// <summary>
        /// Creates a new instance of <see cref="CosmosMessageHeadersBase"/>.
        /// </summary>
        public CosmosMessageHeadersBase()
        {
            this.messageHeaders = new Lazy<CosmosMessageHeadersInternal>(this.CreateCosmosMessageHeaders);
        }

        /// <summary>
        /// Gets the value of a particular header.
        /// </summary>
        /// <param name="headerName">Header name to look for.</param>
        /// <returns></returns>
        public virtual string this[string headerName]
        {
            get => this.messageHeaders.Value[headerName];
            set => this.messageHeaders.Value[headerName] = value;
        }

        /// <summary>
        /// Enumerates all the HTTP headers names in the <see cref="CosmosMessageHeadersBase"/>.
        /// </summary>
        /// <returns></returns>
        public virtual IEnumerator<string> GetEnumerator()
        {
            return this.messageHeaders.Value.GetEnumerator();
        }

        /// <summary>
        /// Adds a header to the Header collection.
        /// </summary>
        /// <param name="headerName">Header name.</param>
        /// <param name="value">Header value.</param>
        public virtual void Add(string headerName, string value)
        {
            this.messageHeaders.Value.Add(headerName, value);
        }

        /// <summary>
        /// Adds a header to the Header collection.
        /// </summary>
        /// <param name="headerName">Header name.</param>
        /// <param name="values">List of values to be added as a comma-separated list.</param>
        public virtual void Add(string headerName, IEnumerable<string> values)
        {
            this.messageHeaders.Value.Add(headerName, values);
        }

        /// <summary>
        /// Adds or updates a header in the Header collection.
        /// </summary>
        /// <param name="headerName">Header name.</param>
        /// <param name="value">Header value.</param>
        public virtual void Set(string headerName, string value)
        {
            this.messageHeaders.Value.Set(headerName, value);
        }

        /// <summary>
        /// Gets the value of a particular header.
        /// </summary>
        /// <param name="headerName">Header name.</param>
        /// <returns></returns>
        public virtual string Get(string headerName)
        {
            return this.messageHeaders.Value.Get(headerName);
        }

        /// <summary>
        /// Tries to get the value for a particular header.
        /// </summary>
        /// <param name="headerName">Header name.</param>
        /// <param name="value">Header value.</param>
        /// <returns>True or false if the header name existed in the header collection.</returns>
        public virtual bool TryGetValue(string headerName, out string value)
        {
            return this.messageHeaders.Value.TryGetValue(headerName, out value);
        }

        /// <summary>
        /// Returns the header value or the default(string)
        /// </summary>
        /// <param name="headerName">Header Name</param>
        /// <returns>Returns the header value or the default(string)</returns>
        public virtual string GetValueOrDefault(string headerName)
        {
            if (this.TryGetValue(headerName, out string value))
            {
                return value;
            }

            return default(string);
        }

        /// <summary>
        /// Removes a header from the header collection.
        /// </summary>
        /// <param name="headerName">Header name.</param>
        public virtual void Remove(string headerName)
        {
            this.messageHeaders.Value.Remove(headerName);
        }

        /// <summary>
        /// Obtains a list of all header names.
        /// </summary>
        /// <returns>An array with all the header names.</returns>
        public virtual string[] AllKeys()
        {
            return this.messageHeaders.Value.AllKeys();
        }

        /// <summary>
        /// Gets a header value with a particular type.
        /// </summary>
        /// <typeparam name="T">Type of the header value.</typeparam>
        /// <param name="headerName">Header name.</param>
        /// <returns></returns>
        public virtual T GetHeaderValue<T>(string headerName)
        {
            return this.messageHeaders.Value.GetHeaderValue<T>(headerName);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        internal string[] GetValues(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            string value = this[key];
            if (value == null)
            {
                return null;
            }

            return new string[1] { this[key] };
        }

        internal CosmosMessageHeadersInternal CosmosMessageHeaders => this.messageHeaders.Value;

        private CosmosMessageHeadersInternal CreateCosmosMessageHeaders()
        {
            return new CosmosMessageHeadersInternal(this.CreateKnownDictionary());
        }

        internal virtual Dictionary<string, CosmosCustomHeader> CreateKnownDictionary()
        {
            return new Dictionary<string, CosmosCustomHeader>(StringComparer.OrdinalIgnoreCase);
        }
    }
}