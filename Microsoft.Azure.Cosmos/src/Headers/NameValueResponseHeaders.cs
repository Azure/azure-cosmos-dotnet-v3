//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Globalization;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    /// <summary>
    /// Header implementation used for Responses
    /// </summary>
    internal sealed class NameValueResponseHeaders : CosmosMessageHeadersInternal
    {
        public override INameValueCollection INameValueCollection { get; }

        public NameValueResponseHeaders(INameValueCollection nameValueCollection)
        {
            this.INameValueCollection = nameValueCollection ?? throw new ArgumentNullException(nameof(nameValueCollection));
        }

        public override void Add(string headerName, string value)
        {
            this.INameValueCollection.Add(headerName, value);
        }

        public override void Add(string headerName, IEnumerable<string> values)
        {
            this.INameValueCollection.Add(headerName, values);
        }

        public override void Set(string headerName, string value)
        {
            this.INameValueCollection.Set(headerName, value);
        }

        public override string Get(string headerName)
        {
            return this.INameValueCollection.Get(headerName);
        }

        public override bool TryGetValue(string headerName, out string value)
        {
            value = this.INameValueCollection.Get(headerName);
            return value != null;
        }

        public override void Remove(string headerName)
        {
            this.INameValueCollection.Remove(headerName);
        }

        public override string[] AllKeys()
        {
            return this.INameValueCollection.AllKeys();
        }

        public override IEnumerator<string> GetEnumerator()
        {
            return this.INameValueCollection.Keys().GetEnumerator();
        }

        public override string[] GetValues(string key)
        {
            return this.INameValueCollection.GetValues(key);
        }
    }
}