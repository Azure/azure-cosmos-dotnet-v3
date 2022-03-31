using System.Collections.Generic;

internal class ImmutableQueryStatisticsAccumulator
{
    public List<double> retrievedDocumentCountList = new();
    public List<double> retrievedDocumentSizeList = new();
    public List<double> outputDocumentCountList = new();
    public List<double> outputDocumentSizeList = new();
    public List<double> totalQueryExecutionTimeList = new();
    public List<double> documentLoadTimeList = new();
    public List<double> documentWriteTimeList = new();
    public List<double> createdList = new();
    public List<double> channelAcquisitionStartedList = new();
    public List<double> pipelinedList = new();
    public List<double> transitTimeList = new();
    public List<double> receivedList = new();
    public List<double> completedList = new();
    public List<double> badRequestCreatedList = new();
    public List<double> badRequestChannelAcquisitionStartedList = new();
    public List<double> badRequestPipelinedList = new();
    public List<double> badRequestTransitTimeList = new();
    public List<double> badRequestReceivedList = new();
    public List<double> badRequestCompletedList = new();

    public ImmutableQueryStatisticsAccumulator(QueryStatisticsAccumulatorBuilder builder)
    {
        this.retrievedDocumentCountList = builder.RetrievedDocumentCountList;
        this.retrievedDocumentSizeList = builder.RetrievedDocumentSizeList;
        this.outputDocumentCountList = builder.OutputDocumentCountList;
        this.outputDocumentSizeList = builder.OutputDocumentSizeList;
        this.totalQueryExecutionTimeList = builder.TotalQueryExecutionTimeList;
        this.documentLoadTimeList = builder.DocumentLoadTimeList;
        this.documentWriteTimeList = builder.DocumentWriteTimeList;
        this.createdList = builder.CreatedList;
        this.channelAcquisitionStartedList = builder.ChannelAcquisitionStartedList;
        this.pipelinedList = builder.PipelinedList;
        this.transitTimeList = builder.TransitTimeList;
        this.receivedList = builder.ReceivedList;
        this.completedList = builder.CompletedList;
        this.badRequestCreatedList = builder.BadRequestCreatedList;
        this.badRequestChannelAcquisitionStartedList = builder.BadRequestChannelAcquisitionStartedList;
        this.badRequestPipelinedList = builder.BadRequestPipelinedList;
        this.badRequestTransitTimeList = builder.BadRequestTransitTimeList;
        this.badRequestReceivedList = builder.BadRequestReceivedList;
        this.badRequestCompletedList = builder.BadRequestCompletedList;
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
