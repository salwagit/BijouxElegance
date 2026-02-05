namespace BijouxElegance.Services
{
    public class IndexingOptions
    {
        public int BatchSize { get; set; } = 3;
        public int DelayBetweenBatches { get; set; } = 10000; // ms
        public int DelayBetweenRequests { get; set; } = 2000; // ms
    }
}
