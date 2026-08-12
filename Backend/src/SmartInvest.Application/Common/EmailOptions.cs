namespace SmartInvest.Application.Common;

public class EmailOptions
{
    public const string SectionName = "Email";

    public string SmtpHost { get; set; } = string.Empty;

    public int SmtpPort { get; set; } = 587;

    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "SmartInvest";

    public bool EnableSsl { get; set; } = true;

    public string FrontendBaseUrl { get; set; } = "http://localhost:4200";
}
