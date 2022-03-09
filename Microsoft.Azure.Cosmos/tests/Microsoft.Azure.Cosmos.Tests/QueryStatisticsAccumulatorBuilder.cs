using System.Collections.Generic;

internal class ImmutableQueryStatisticsAccumulator
{
    public List<double> RetrievedDocumentCountList = new();
    public List<double> RetrievedDocumentSizeList = new();
    public List<double> OutputDocumentCountList = new();
    public List<double> OutputDocumentSizeList = new();
    public List<double> TotalQueryExecutionTimeList = new();
    public List<double> DocumentLoadTimeList = new();
    public List<double> DocumentWriteTimeList = new();
    public List<double> CreatedList = new();
    public List<double> ChannelAcquisitionStartedList = new();
    public List<double> PipelinedList = new();
    public List<double> TransitTimeList = new();
    public List<double> ReceivedList = new();
    public List<double> CompletedList = new();
    public List<double> BadRequestCreatedList = new();
    public List<double> BadRequestChannelAcquisitionStartedList = new();
    public List<double> BadRequestPipelinedList = new();
    public List<double> BadRequestTransitTimeList = new();
    public List<double> BadRequestReceivedList = new();
    public List<double> BadRequestCompletedList = new();

    private ImmutableQueryStatisticsAccumulator(QueryStatisticsAccumulatorBuilder builder)
    {
        this.RetrievedDocumentCountList = builder.RetrievedDocumentCountList;
        this.RetrievedDocumentSizeList = builder.RetrievedDocumentSizeList;
        this.OutputDocumentCountList = builder.OutputDocumentCountList;
        this.OutputDocumentSizeList = builder.OutputDocumentSizeList;
        this.TotalQueryExecutionTimeList = builder.TotalQueryExecutionTimeList;
        this.DocumentLoadTimeList = builder.DocumentLoadTimeList;
        this.DocumentWriteTimeList = builder.DocumentWriteTimeList;
        this.CreatedList = builder.CreatedList;
        this.ChannelAcquisitionStartedList = builder.ChannelAcquisitionStartedList;
        this.PipelinedList = builder.PipelinedList;
        this.TransitTimeList = builder.TransitTimeList;
        this.ReceivedList = builder.ReceivedList;
        this.CompletedList = builder.CompletedList;
        this.BadRequestCreatedList = builder.BadRequestCreatedList;
        this.BadRequestChannelAcquisitionStartedList = builder.BadRequestChannelAcquisitionStartedList;
        this.BadRequestPipelinedList = builder.BadRequestPipelinedList;
        this.BadRequestTransitTimeList = builder.BadRequestTransitTimeList;
        this.BadRequestReceivedList = builder.BadRequestReceivedList;
        this.BadRequestCompletedList = builder.BadRequestCompletedList;
    }

    public class QueryStatisticsAccumulatorBuilder
    {
        public List<double> RetrievedDocumentCountList { get; private set; } = new();
        public List<double> RetrievedDocumentSizeList { get; private set; } = new();
        public List<double> OutputDocumentCountList { get; private set; } = new();
        public List<double> OutputDocumentSizeList { get; private set; } = new();
        public List<double> TotalQueryExecutionTimeList { get; private set; } = new();
        public List<double> DocumentLoadTimeList { get; private set; } = new();
        public List<double> DocumentWriteTimeList { get; private set; } = new();
        public List<double> CreatedList { get; private set; } = new();
        public List<double> ChannelAcquisitionStartedList { get; private set; } = new();
        public List<double> PipelinedList { get; private set; } = new();
        public List<double> TransitTimeList { get; private set; } = new();
        public List<double> ReceivedList { get; private set; } = new();
        public List<double> CompletedList { get; private set; } = new();
        public List<double> BadRequestCreatedList { get; private set; } = new();
        public List<double> BadRequestChannelAcquisitionStartedList { get; private set; } = new();
        public List<double> BadRequestPipelinedList { get; private set; } = new();
        public List<double> BadRequestTransitTimeList { get; private set; } = new();
        public List<double> BadRequestReceivedList { get; private set; } = new();
        public List<double> BadRequestCompletedList { get; private set; } = new();

        public QueryStatisticsAccumulatorBuilder AddRetrievedDocumentCount(long retrievedDocumentCount)
        {
            this.RetrievedDocumentCountList.Add(retrievedDocumentCount);
            return this;
        }
        public QueryStatisticsAccumulatorBuilder AddRetrievedDocumentSize(long retrievedDocumentSize)
        {
            this.RetrievedDocumentSizeList.Add(retrievedDocumentSize);
            return this;
        }
        public QueryStatisticsAccumulatorBuilder AddOutputDocumentCount(long outputDocumentCount)
        {
            this.OutputDocumentCountList.Add(outputDocumentCount);
            return this;
        }
        public QueryStatisticsAccumulatorBuilder AddOutputDocumentSize(long outputDocumentSize)
        {
            this.OutputDocumentSizeList.Add(outputDocumentSize);
            return this;
        }
        public QueryStatisticsAccumulatorBuilder AddTotalQueryExecutionTime(double totalQueryExecutionTime)
        {
            this.TotalQueryExecutionTimeList.Add(totalQueryExecutionTime);
            return this;
        }
        public QueryStatisticsAccumulatorBuilder AddDocumentLoadTime(double documentLoadTime)
        {
            this.DocumentLoadTimeList.Add(documentLoadTime);
            return this;
        }
        public QueryStatisticsAccumulatorBuilder AddDocumentWriteTime(double documentWriteTime)
        {
            this.DocumentWriteTimeList.Add(documentWriteTime);
            return this;
        }
        public QueryStatisticsAccumulatorBuilder AddCreated(double created)
        {
            this.CreatedList.Add(created);
            return this;
        }
        public QueryStatisticsAccumulatorBuilder AddChannelAcquisitionStarted(double channelAcquisitionStarted)
        {
            this.ChannelAcquisitionStartedList.Add(channelAcquisitionStarted);
            return this;
        }
        public QueryStatisticsAccumulatorBuilder AddPipelined(double Pipelined)
        {
            this.PipelinedList.Add(Pipelined);
            return this;
        }
        public QueryStatisticsAccumulatorBuilder AddTransitTime(double transitTime)
        {
            this.TransitTimeList.Add(transitTime);
            return this;
        }
        public QueryStatisticsAccumulatorBuilder AddReceived(double received)
        {
            this.ReceivedList.Add(received);
            return this;
        }
        public QueryStatisticsAccumulatorBuilder AddCompleted(double completed)
        {
            this.CompletedList.Add(completed);
            return this;
        }
        public QueryStatisticsAccumulatorBuilder AddBadRequestCreated(double BadRequestCreated)
        {
            this.BadRequestCreatedList.Add(BadRequestCreated);
            return this;
        }
        public QueryStatisticsAccumulatorBuilder AddBadRequestChannelAcquisitionStarted(double BadRequestChannelAcquisitionStarted)
        {
            this.BadRequestChannelAcquisitionStartedList.Add(BadRequestChannelAcquisitionStarted);
            return this;
        }
        public QueryStatisticsAccumulatorBuilder AddBadRequestPipelined(double BadRequestPipelined)
        {
            this.BadRequestPipelinedList.Add(BadRequestPipelined);
            return this;
        }
        public QueryStatisticsAccumulatorBuilder AddBadRequestTransitTime(double BadRequestTransitTime)
        {
            this.BadRequestTransitTimeList.Add(BadRequestTransitTime);
            return this;
        }
        public QueryStatisticsAccumulatorBuilder AddBadRequestReceived(double BadRequestReceived)
        {
            this.BadRequestReceivedList.Add(BadRequestReceived);
            return this;
        }
        public QueryStatisticsAccumulatorBuilder AddBadRequestCompleted(double BadRequestCompleted)
        {
            this.BadRequestCompletedList.Add(BadRequestCompleted);
            return this;
        }

        public ImmutableQueryStatisticsAccumulator Build()
        {
            return new ImmutableQueryStatisticsAccumulator(this);
        }
    }
}
