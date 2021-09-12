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
        private readonly INameValueCollection nameValueCollection;

        public NameValueResponseHeaders(INameValueCollection nameValueCollection)
        {
            this.nameValueCollection = nameValueCollection ?? throw new ArgumentNullException(nameof(nameValueCollection));
        }

        public override void Add(string headerName, string value)
        {
            this.nameValueCollection.Add(headerName, value);
        }

        public override void Add(string headerName, IEnumerable<string> values)
        {
            this.nameValueCollection.Add(headerName, values);
        }

        public override void Set(string headerName, string value)
        {
            this.nameValueCollection.Set(headerName, value);
        }

        public override string Get(string headerName)
        {
            return this.nameValueCollection.Get(headerName);
        }

        public override bool TryGetValue(string headerName, out string value)
        {
            value = this.nameValueCollection.Get(headerName);
            return value != null;
        }

        public override void Remove(string headerName)
        {
            this.nameValueCollection.Remove(headerName);
        }

        public override string[] AllKeys()
        {
            return this.nameValueCollection.AllKeys();
        }

        public override IEnumerator<string> GetEnumerator()
        {
            return this.nameValueCollection.Keys().GetEnumerator();
        }

        public override void Clear()
        {
            this.nameValueCollection.Clear();
        }

        public override int Count()
        {
            return this.nameValueCollection.Count();
        }

        public override INameValueCollection Clone()
        {
            return this.nameValueCollection.Clone();
        }

        public override string[] GetValues(string key)
        {
            return this.nameValueCollection.GetValues(key);
        }

        public override IEnumerable<string> Keys()
        {
            return this.nameValueCollection.Keys();
        }

        public override NameValueCollection ToNameValueCollection()
        {
            return this.nameValueCollection.ToNameValueCollection();
        }
    }
}