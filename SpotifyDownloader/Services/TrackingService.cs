using Microsoft.Extensions.Logging;

interface ITrackingService
{
    public void Test();
}

public class TrackingService(ILogger<TrackingService> logger) : ITrackingService
{
    public void Test()
    {
        logger.LogInformation("Test!");
    }
}