namespace PowerPilot.Infrastructure.Weather;

public class WeatherOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string City { get; set; } = "Brussels";
    public string Units { get; set; } = "metric";
}
