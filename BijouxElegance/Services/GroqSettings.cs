namespace BijouxElegance.Services;

public class GroqSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int MaxTokens { get; set; } = 500;
    public double Temperature { get; set; } = 0.7;
    public int CacheDurationMinutes { get; set; } = 5;
}
