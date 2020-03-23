//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System.Collections.Generic;

    /// <summary>
    /// Provides utility to store and compare request charges for tests.
    /// </summary>
    internal sealed class RequestChargeHelper
    {
        private readonly Dictionary<DocumentClientType, double> chargeStore;

        internal RequestChargeHelper()
        {
            this.chargeStore = new Dictionary<DocumentClientType, double>();
        }

        /// <summary>
        /// Sets the value for charge for a clientType.
        /// Overrides the earlier set value if it is set.
        /// </summary>
        /// <param name="type">DocumentClientType</param>
        /// <param name="charge">charge for the operation</param>
        internal void SetValue(DocumentClientType type, double charge)
        {
            this.chargeStore[type] = charge;
        }

        internal void SetOrAddValue(DocumentClientType type, double charge)
        {
            double curr;
            if (this.chargeStore.TryGetValue(type, out curr))
            {
                this.chargeStore[type] = curr + charge;
            }
            else
            {
                this.chargeStore[type] = charge;
            }
        }

        internal void CompareRequestCharge(string testName, double epsilon = 0.1)
        {
            // Bug : 5509497. As we are enabling the sync replication and new quorum read in the gateway only,
            // the charge comparison will fail because of different logic between g/w and client. Even in client
            // it will be different because of swich between read from secondaries vs primary based upon old quorum read logic.
            // Hence disabling this validation until we can start using the read quorum based strong read everywhere
            //if (chargeStore.Count == 0)
            //{
            //    return;
            //}

            //var it = this.chargeStore.Keys.GetEnumerator();
            //do
            //{
            //    var temptr = it;
            //    if (!temptr.MoveNext())
            //    {
            //        continue;
            //    }

            //    do
            //    {
            //        Assert.AreEqual(chargeStore[it.Current], chargeStore[temptr.Current], epsilon);
            //    } while (temptr.MoveNext());

            //} while (it.MoveNext());
        }
    }
}
