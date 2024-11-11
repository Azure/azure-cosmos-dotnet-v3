//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using Newtonsoft.Json;

    public class ToDoActivity
    {
        [JsonProperty(propertyName: "id")]
        public string Id { get; set; }
        [JsonProperty(propertyName: "pk")]
        public string Pk { get; set; }

        public override bool Equals(object obj)
        {
            if (!(obj is ToDoActivity input))
            {
                return false;
            }

            return string.Equals(this.Id, input.Id)
                && string.Equals(this.Pk, input.Pk);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}