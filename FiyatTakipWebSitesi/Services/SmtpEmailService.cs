// =====================================================
// SmtpEmailService.cs
// IEmailService'in gerçek SMTP üzerinden gönderim yapan
// implementasyonu. Gmail için kullanım:
//   1. Google Hesabı → Güvenlik → 2 Adımlı Doğrulama AKTİF olmalı
//   2. 'Uygulama şifreleri' sayfasından 16 karakterli App Password al
//   3. appsettings.Development.json'a Smtp:Password olarak yapıştır
// SMTP başarısız olursa exception fırlatmaz, logla devam eder —
// e-posta gönderemediğimiz için tüm uyarı akışını çökertmeyelim.
// =====================================================

using System.Net;
using System.Net.Mail;

namespace FiyatTakipWebSitesi.Services;

public sealed class SmtpEmailService : IEmailService
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger)
    {
        _logger = logger;
        _settings = configuration.GetSection("Smtp").Get<SmtpSettings>()
                    ?? throw new InvalidOperationException(
                        "appsettings.json içinde 'Smtp' bölümü bulunamadı.");

        if (string.IsNullOrWhiteSpace(_settings.Host) ||
            string.IsNullOrWhiteSpace(_settings.User) ||
            string.IsNullOrWhiteSpace(_settings.Password))
        {
            _logger.LogWarning(
                "[SmtpEmailService] Smtp ayarları eksik (Host/User/Password). E-postalar log'a düşecek.");
        }
    }

    public async Task SendEmailAsync(string to, string subject, string body)
    {
        // Konfigürasyon eksikse log'a düşür ve sessizce dön (uyarı akışını kırmayalım)
        if (string.IsNullOrWhiteSpace(_settings.Host) ||
            string.IsNullOrWhiteSpace(_settings.User) ||
            string.IsNullOrWhiteSpace(_settings.Password))
        {
            _logger.LogInformation(
                "==== SMTP YAPILANDIRILMAMIŞ — E-POSTA LOG'A DÜŞÜRÜLDÜ ====\nAlıcı: {To}\nKonu: {Subject}\n{Body}",
                to, subject, body);
            return;
        }

        try
        {
            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = _settings.EnableSsl,
                Credentials = new NetworkCredential(_settings.User, _settings.Password),
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            using var message = new MailMessage
            {
                From = new MailAddress(_settings.From, _settings.FromName),
                Subject = subject,
                Body = HtmlGovdeOlustur(subject, body),
                IsBodyHtml = true,
                SubjectEncoding = System.Text.Encoding.UTF8,
                BodyEncoding = System.Text.Encoding.UTF8
            };
            message.To.Add(to);

            await client.SendMailAsync(message);

            _logger.LogInformation(
                "[SmtpEmailService] ✓ E-posta gönderildi — Alıcı: {To}, Konu: {Subject}",
                to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[SmtpEmailService] ✗ E-posta gönderilemedi — Alıcı: {To}, Konu: {Subject}, Hata: {Mesaj}",
                to, subject, ex.Message);
            // Uyarı akışını kırmamak için fırlatmıyoruz
        }
    }

    /// <summary>
    /// Düz metin mesajı basit bir HTML şablona sarar — sunum görseli için.
    /// </summary>
    private static string HtmlGovdeOlustur(string baslik, string mesaj)
    {
        var safeMesaj = System.Net.WebUtility.HtmlEncode(mesaj).Replace("\n", "<br/>");
        return $@"
<!DOCTYPE html>
<html><body style='font-family:Segoe UI,Arial,sans-serif;background:#f1f5f9;padding:24px;'>
  <div style='max-width:560px;margin:0 auto;background:white;border-radius:12px;overflow:hidden;box-shadow:0 4px 12px rgba(0,0,0,0.08);'>
    <div style='background:linear-gradient(135deg,#0284c7,#2563eb);color:white;padding:20px 24px;'>
      <h2 style='margin:0;font-size:1.25rem;'>📊 FiyatTakip Bildirimi</h2>
    </div>
    <div style='padding:24px;color:#1e293b;'>
      <h3 style='margin:0 0 12px;color:#0f172a;'>{System.Net.WebUtility.HtmlEncode(baslik)}</h3>
      <p style='line-height:1.6;color:#475569;'>{safeMesaj}</p>
    </div>
    <div style='background:#f8fafc;padding:14px 24px;color:#94a3b8;font-size:0.8rem;text-align:center;border-top:1px solid #e2e8f0;'>
      Bu e-posta FiyatTakipWebSitesi otomatik bildirim sistemi tarafından gönderildi.
    </div>
  </div>
</body></html>";
    }

    private sealed class SmtpSettings
    {
        public string Host { get; set; } = "";
        public int Port { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;
        public string User { get; set; } = "";
        public string Password { get; set; } = "";
        public string From { get; set; } = "";
        public string FromName { get; set; } = "FiyatTakip Bildirim";
    }
}
