//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Diagnostics;

    internal sealed class ServerKey
    {
        public ServerKey(Uri uri)
        {
            Debug.Assert(uri != null);
            this.Server = uri.DnsSafeHost;
            this.Port = uri.Port;
        }

        public string Server { get; private set; }

        public int Port { get; private set; }

        public override string ToString()
        {
            return string.Format("{0}:{1}", this.Server, this.Port);
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }
            return this.Equals(obj as ServerKey);
        }

        public bool Equals(ServerKey key)
        {
            return key != null && this.Server.Equals(key.Server) &&
                this.Port == key.Port;
        }

        public override int GetHashCode()
        {
            // FNV hash.
            unchecked
            {
                const int factor = 0x120D6C21;  // Prime number.
                int hash = 0x22970BE1;  // Prime number.
                hash ^= this.Server.GetHashCode();
                hash *= factor;
                // int.GetHashCode() returns the value itself, which is a bad hash.
                hash ^= ServerKey.HashInt32(this.Port);
                hash *= factor;
                return hash;
            }
        }

        private static int HashInt32(int key)
        {
            // FNV hash.
            unchecked
            {
                const int factor = 0x268E114D;  // Prime number.
                int hash = 0x0FE669CB;  // Prime number.
                for (int i = 0; i < 4; i++)
                {
                    hash ^= (key & 0xFF);
                    hash *= factor;
                    key >>= 8;
                }
                return hash;
            }
        }
    }
}
