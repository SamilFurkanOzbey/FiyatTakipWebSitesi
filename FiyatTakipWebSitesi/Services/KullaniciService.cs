// =====================================================
// KullaniciService.cs
// Kullanıcı kaydı, girişi, profil yönetimi ve JWT token
// üretimini sağlar. Şifreler HMACSHA512 ile hash'lenir
// (SifreHash + SifreTuzu). Başarılı giriş/kayıt sonrası
// AuthResponse içinde JWT token döner.
// =====================================================

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FiyatTakipWebSitesi.Data;
using FiyatTakipWebSitesi.DTOs;
using FiyatTakipWebSitesi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace FiyatTakipWebSitesi.Services;

public class KullaniciService(
    ApplicationDbContext context,
    IConfiguration config,
    ILogger<KullaniciService> logger)
{
    private readonly ApplicationDbContext _context = context;
    private readonly IConfiguration _config = config;
    private readonly ILogger<KullaniciService> _logger = logger;

    // ── Kayıt ────────────────────────────────────────────────

    /// <summary>
    /// Yeni kullanıcı kaydı oluşturur. E-posta zaten kayıtlıysa hata fırlatır.
    /// </summary>
    public async Task<AuthResponse> KayitAsync(KayitRequest istek)
    {
        if (await _context.Kullanicilar.AnyAsync(k => k.Email == istek.Email))
            throw new InvalidOperationException("Bu e-posta adresi zaten kullanılıyor.");

        SifreHashle(istek.Sifre, out byte[] hash, out byte[] tuz);

        var kullanici = new Kullanici
        {
            Ad               = istek.Ad.Trim(),
            Soyad            = istek.Soyad.Trim(),
            Email            = istek.Email.Trim().ToLowerInvariant(),
            TelefonNumarasi  = istek.TelefonNumarasi?.Trim(),
            SifreHash        = hash,
            SifreTuzu        = tuz,
            OlusturulmaTarihi = DateTime.UtcNow,
            Aktif            = true
        };

        _context.Kullanicilar.Add(kullanici);
        await _context.SaveChangesAsync();

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Yeni kullanıcı kaydoldu: {Email}", kullanici.Email);
        }
        return JwtUret(kullanici);
    }

    // ── Giriş ────────────────────────────────────────────────

    /// <summary>
    /// E-posta ve şifre doğrulayarak JWT token döner.
    /// </summary>
    public async Task<AuthResponse> GirisAsync(GirisRequest istek)
    {
        var email = istek.Email.Trim();
        var kullanici = await _context.Kullanicilar
            .FirstOrDefaultAsync(k => string.Equals(k.Email, email, StringComparison.OrdinalIgnoreCase));

        if (kullanici is null || kullanici.SifreHash is null || kullanici.SifreTuzu is null)
            throw new UnauthorizedAccessException("E-posta veya şifre hatalı.");

        if (!SifreDogrula(istek.Sifre, kullanici.SifreHash, kullanici.SifreTuzu))
            throw new UnauthorizedAccessException("E-posta veya şifre hatalı.");

        if (!kullanici.Aktif)
            throw new UnauthorizedAccessException("Hesabınız askıya alınmış. Lütfen destek ile iletişime geçin.");

        kullanici.SonGirisTarihi = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Kullanıcı giriş yaptı: {Email}", kullanici.Email);
        }
        return JwtUret(kullanici);
    }

    // ── Profil ───────────────────────────────────────────────

    /// <summary>
    /// Kimliği doğrulanmış kullanıcının profil bilgisini döner.
    /// </summary>
    public async Task<ProfilResponse?> ProfilGetirAsync(int kullaniciId)
    {
        var kullanici = await _context.Kullanicilar
            .AsNoTracking()
            .Include(k => k.Urunler)
            .Include(k => k.Uyarilar.Where(u => !u.Okundu))
            .FirstOrDefaultAsync(k => k.Id == kullaniciId);

        if (kullanici is null) return null;

        return new ProfilResponse
        {
            Id                     = kullanici.Id,
            Ad                     = kullanici.Ad,
            Soyad                  = kullanici.Soyad,
            Email                  = kullanici.Email,
            TelefonNumarasi        = kullanici.TelefonNumarasi,
            OlusturulmaTarihi      = kullanici.OlusturulmaTarihi,
            SonGirisTarihi         = kullanici.SonGirisTarihi,
            EmailBildirimleriniAc  = kullanici.EmailBildirimleriniAc,
            PushBildirimleriniAc   = kullanici.PushBildirimleriniAc,
            TakipEdilenUrunSayisi  = kullanici.Urunler.Count,
            OkunmamisUyariSayisi   = kullanici.Uyarilar.Count
        };
    }

    // ── Şifre Değiştir ───────────────────────────────────────

    /// <summary>
    /// Mevcut şifreyi doğrulayarak yeni şifre belirler.
    /// </summary>
    public async Task<bool> SifreDegistirAsync(int kullaniciId, SifreDegistirRequest istek)
    {
        var kullanici = await _context.Kullanicilar.FindAsync(kullaniciId);
        if (kullanici is null) return false;

        if (kullanici.SifreHash is null || kullanici.SifreTuzu is null)
            return false;

        if (!SifreDogrula(istek.MevcutSifre, kullanici.SifreHash, kullanici.SifreTuzu))
            throw new UnauthorizedAccessException("Mevcut şifre hatalı.");

        SifreHashle(istek.YeniSifre, out byte[] yeniHash, out byte[] yeniTuz);
        kullanici.SifreHash  = yeniHash;
        kullanici.SifreTuzu  = yeniTuz;

        await _context.SaveChangesAsync();
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Kullanıcı {Id} şifresini değiştirdi.", kullaniciId);
        }
        return true;
    }

    // ── JWT Üretimi ──────────────────────────────────────────

    private AuthResponse JwtUret(Kullanici kullanici)
    {
        var jwtKey    = _config["Jwt:Key"]      ?? throw new InvalidOperationException("JWT Key yapılandırılmamış.");
        var issuer    = _config["Jwt:Issuer"]   ?? "FiyatTakipWebSitesi";
        var audience  = _config["Jwt:Audience"] ?? "FiyatTakipKullanicilari";
        var expMinutes = int.TryParse(_config["Jwt:ExpireMinutes"], out var m) ? m : 1440;

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);
        var bitis = DateTime.UtcNow.AddMinutes(expMinutes);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, kullanici.Id.ToString()),
            new(ClaimTypes.Name,           kullanici.Ad),
            new(ClaimTypes.Email,          kullanici.Email),
            new("soyad",                   kullanici.Soyad)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject            = new ClaimsIdentity(claims),
            Expires            = bitis,
            Issuer             = issuer,
            Audience           = audience,
            SigningCredentials = creds
        };

        var handler = new JwtSecurityTokenHandler();
        var token   = handler.CreateToken(tokenDescriptor);

        return new AuthResponse
        {
            Token        = handler.WriteToken(token),
            KullaniciId  = kullanici.Id,
            Ad           = kullanici.Ad,
            Soyad        = kullanici.Soyad,
            Email        = kullanici.Email,
            BitisTarihi  = bitis
        };
    }

    // ── Şifre Yardımcıları ───────────────────────────────────

    private static void SifreHashle(string sifre, out byte[] hash, out byte[] tuz)
    {
        using var hmac = new HMACSHA512();
        tuz  = hmac.Key;
        hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(sifre));
    }

    private static bool SifreDogrula(string sifre, byte[] kayitliHash, byte[] kayitliTuz)
    {
        using var hmac = new HMACSHA512(kayitliTuz);
        var hesaplananHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(sifre));
        return hesaplananHash.SequenceEqual(kayitliHash);
    }
}
