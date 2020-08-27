//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Globalization;

    internal enum RntbdResponseStateEnum
    {
        NotStarted = 0,
        Called = 1,
        StartHeader = 2,
        BufferingHeader = 3,
        DoneBufferingHeader = 4,
        BufferingMetadata = 5,
        DoneBufferingMetadata = 6,
        BufferingBodySize = 7,
        DoneBufferingBodySize = 8,
        BufferingBody = 9,
        DoneBufferingBody = 10,
        Done = 11,
    }

    internal class RntbdResponseState
    {
        private static readonly string[] StateNames = Enum.GetNames(typeof(RntbdResponseStateEnum));

        private RntbdResponseStateEnum state;
        private int headerAndMetadataRead;
        private int bodyRead;
        private DateTimeOffset lastReadTime;

        public RntbdResponseState()
        {
            this.state = RntbdResponseStateEnum.NotStarted;
            this.headerAndMetadataRead = 0;
            this.bodyRead = 0;
            this.lastReadTime = DateTimeOffset.MinValue;
        }

        public void SetState(RntbdResponseStateEnum newState)
        {
            if (newState < this.state || newState > RntbdResponseStateEnum.Done)
            {
                throw new InternalServerErrorException();
            }
            else
            {
                this.state = newState;
            }
        }

        public void AddHeaderMetadataRead(int amountRead)
        {
            this.headerAndMetadataRead += amountRead;
            this.lastReadTime = DateTimeOffset.Now;
        }

        public void AddBodyRead(int amountRead)
        {
            this.bodyRead += amountRead;
            this.lastReadTime = DateTimeOffset.Now;
        }

        public override string ToString()
        {
            return String.Format(CultureInfo.InvariantCulture, "State: {0}. Meta bytes read: {1}. Body bytes read: {2}. Last read completion: {3}", 
                RntbdResponseState.StateNames[(int)(this.state)], this.headerAndMetadataRead, this.bodyRead, this.lastReadTime);
        }
    }
}
