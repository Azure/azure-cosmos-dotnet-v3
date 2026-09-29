//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Globalization;

    /// <summary>
    /// Implements session token with Global LSN
    ///
    /// We make assumption that instances of this interface are immutable (read only after they are constructed), so if you want to change
    /// this behaviour please review all of its uses and make sure that mutability doesn't break anything.
    /// </summary>
    internal sealed class SimpleSessionToken : ISessionToken
    {
        private readonly long globalLsn;

        public SimpleSessionToken(long globalLsn)
        {
            this.globalLsn = globalLsn;
        }

        public static bool TryCreate(string globalLsn, out ISessionToken parsedSessionToken)
        {
            parsedSessionToken = null;
            long parsedGlobalLsn = -1;

            if (long.TryParse(globalLsn, out parsedGlobalLsn))
            {
                parsedSessionToken = new SimpleSessionToken(parsedGlobalLsn);
                return true;
            }
            else
            {
                return false;
            }
        }

        public long LSN
        {
            get
            {
                return this.globalLsn;
            }
        }

        public bool Equals(ISessionToken obj)
        {
            SimpleSessionToken other = obj as SimpleSessionToken;

            if (other == null)
            {
                return false;
            }

            return this.globalLsn.Equals(other.globalLsn);
        }

        // Merge is commutative operation, so a.Merge(b).Equals(b.Merge(a))
        public ISessionToken Merge(ISessionToken obj)
        {
            SimpleSessionToken other = obj as SimpleSessionToken;

            if (other == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            return new SimpleSessionToken(Math.Max(this.globalLsn, other.globalLsn));
        }

        public bool IsValid(ISessionToken otherSessionToken)
        {
            SimpleSessionToken other = otherSessionToken as SimpleSessionToken;

            if (other == null)
            {
                throw new ArgumentNullException(nameof(otherSessionToken));
            }

            return other.globalLsn >= this.globalLsn;
        }

        string ISessionToken.ConvertToString()
        {
            return this.globalLsn.ToString(CultureInfo.InvariantCulture);
        }
    }
}
