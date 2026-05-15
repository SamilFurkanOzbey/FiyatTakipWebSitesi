namespace FiyatTakipWebSitesi.Models;

public class JwtSettings
{
    public string Key { get; set; } = "FiyatTakipGizliAnahtar2026!XyZ#AbC$DeF%GhI&JkL";
    public string Issuer { get; set; } = "FiyatTakipWebSitesi";
    public string Audience { get; set; } = "FiyatTakipKullanicilari";
    public int ExpireMinutes { get; set; } = 1440;
}
