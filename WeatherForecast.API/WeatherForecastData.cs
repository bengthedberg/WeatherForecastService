namespace WeatherForecast.API;

public class WeatherForecastData
{
    public string City { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.Today;
    public int TemperatureC { get; set; }
    public string? Summary { get; set; }
    
}
