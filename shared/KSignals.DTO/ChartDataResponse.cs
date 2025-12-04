namespace KSignals.DTO;

public class ChartDataResponse
{
    public string Ticker { get; set; } = string.Empty;
    public List<ChartDataPoint> DataPoints { get; set; } = new();
}

public class ChartDataPoint
{
    public DateTime Timestamp { get; set; }
    public decimal Value { get; set; }
}
