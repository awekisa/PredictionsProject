namespace PredictionsAPI.FootballApi;

public class FootballApiStatusStore
{
    private readonly object _lock = new();
    private int? _requestsLimit;
    private int? _requestsRemaining;

    public void Update(int? limit, int? remaining)
    {
        lock (_lock)
        {
            _requestsLimit = limit;
            _requestsRemaining = remaining;
        }
    }

    public (int? Limit, int? Remaining) Get()
    {
        lock (_lock)
        {
            return (_requestsLimit, _requestsRemaining);
        }
    }
}
