namespace Infrastructure.Identity;

public sealed class JwtOptions
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "MESPlatForm";
    public string Audience { get; set; } = "MESPlatFormClient";
    public int DurationMinutes { get; set; } = 120;
    public int RefreshTokenDays { get; set; } = 7;
}
