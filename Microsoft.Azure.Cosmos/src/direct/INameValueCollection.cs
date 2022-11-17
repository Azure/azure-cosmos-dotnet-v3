//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Collections
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;

    internal interface INameValueCollection : IEnumerable
    {
        void Add(string key, string value);

        void Set(string key, string value);

        string Get(string key);

        string this[string key] { get;  set; }

        void Remove(string key);

        void Clear();

        int Count();

        INameValueCollection Clone();

        void Add(INameValueCollection collection);

        string[] GetValues(string key);

        string[] AllKeys();

        IEnumerable<string> Keys();

        NameValueCollection ToNameValueCollection();
    }
}
