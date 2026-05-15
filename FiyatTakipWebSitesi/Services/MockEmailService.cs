namespace FiyatTakipWebSitesi.Services;

/// <summary>
/// SMTP sunucusu olmadığı için e-posta gönderimlerini sadece loga yazan mock servis.
/// </summary>
public class MockEmailService(ILogger<MockEmailService> logger) : IEmailService
{
    private readonly ILogger<MockEmailService> _logger = logger;

    public Task SendEmailAsync(string to, string subject, string body)
    {
        _logger.LogInformation(
            "==== MOCK E-POSTA GÖNDERİLDİ ====\nAlıcı: {To}\nKonu: {Subject}\nMesaj:\n{Body}\n=================================", 
            to, subject, body);
            
        return Task.CompletedTask;
    }
}
