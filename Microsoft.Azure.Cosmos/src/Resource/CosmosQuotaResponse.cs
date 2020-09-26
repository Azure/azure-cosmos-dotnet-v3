//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// The Cosmos resource response. 
    /// This contains a list of shared properties accross the different response types.
    /// </summary>
    internal class CosmosQuotaResponse
    {
        protected IDictionary<string, double> Quotas = null;
        private readonly string source = null;
        protected const string FunctionsKey = "functions";
        protected const string StoredProceduresKey = "storedProcedures";
        protected const string TriggersKey = "triggers";
        protected const string DatabasesKey = "databases";
        protected const string DocumentSizeKey = "documentSize";
        protected const string DocumentsSizeKey = "documentsSize";
        protected const string DocumentsCountKey = "documentsCount";
        protected const string ContainerSizeKey = "collectionSize";

        /// <summary>
        /// A class to parse all the quota response.
        /// </summary>
        /// <param name="quotaInfo">Example string: functions=0;storedProcedures=0;triggers=0;documentSize=0;documentsSize=0;documentsCount=0;collectionSize=0;</param>
        internal CosmosQuotaResponse(string quotaInfo)
        {
            this.source = quotaInfo;
            this.ParseQuotaString(quotaInfo);
        }

        /// <summary>
        /// Parse the string into the dictionary
        /// </summary>
        /// <param name="quotaInfo">Example string: functions=0;storedProcedures=0;triggers=0;documentSize=0;documentsSize=0;documentsCount=0;collectionSize=0;</param>
        private void ParseQuotaString(string quotaInfo)
        {
            this.Quotas = new Dictionary<string, double>();
            string[] quotaKeyValues = quotaInfo.Split(';');
            foreach (string quotaKeyValue in quotaKeyValues)
            {
                if (!string.IsNullOrEmpty(quotaKeyValue))
                {
                    string[] keyValue = quotaKeyValue.Split('=');
                    if (keyValue.Length == 2)
                    {
                        string key = keyValue[0];
                        double value = double.Parse(keyValue[1], CultureInfo.InvariantCulture);
                        switch (key)
                        {
                            case DatabasesKey:
                                this.Databases = value;
                                break;
                            case FunctionsKey:
                                this.UserDefinedFunctions = value;
                                break;
                            case StoredProceduresKey:
                                this.StoredProcedures = value;
                                break;
                            case TriggersKey:
                                this.Triggers = value;
                                break;
                            case DocumentSizeKey:
                                this.DocumentSize = value;
                                break;
                            case DocumentsSizeKey:
                                this.DocumentsSize = value;
                                break;
                            case DocumentsCountKey:
                                this.DocumentsCount = value;
                                break;
                            case ContainerSizeKey:
                                this.ContainerSize = value;
                                break;
                            default:
                                //Ignore unkown values
                                break;
                        }
                    }
                }
            }
        }

        internal double? Databases { get; private set; }
        internal double? UserDefinedFunctions { get; private set; }
        internal double? StoredProcedures { get; private set; }
        internal double? Triggers { get; private set; }
        internal double? DocumentSize { get; private set; }
        internal double? DocumentsSize { get; private set; }
        internal double? DocumentsCount { get; private set; }
        internal double? ContainerSize { get; private set; }

        /// <summary>
        /// Override the to string method to return the original string from the header
        /// </summary>
        public override string ToString()
        {
            return this.source;
        }
    }
}