// =====================================================
// UyariService.cs
// Fiyat değişimlerine bağlı uyarı kayıtlarını yönetir.
// FiyatDususu, HedefFiyataBasti, RekorDusuk gibi durumlarda
// uyarı oluşturur; kullanıcıya göre listeleme, tekil ve
// toplu okundu işaretleme işlemlerini sağlar.
// =====================================================

using FiyatTakipWebSitesi.Data;
using FiyatTakipWebSitesi.Models;
using Microsoft.EntityFrameworkCore;

namespace FiyatTakipWebSitesi.Services;

public class UyariService(ApplicationDbContext context, ILogger<UyariService> logger, IEmailService emailService)
{
    private readonly ApplicationDbContext _context = context;
    private readonly ILogger<UyariService> _logger = logger;
    private readonly IEmailService _emailService = emailService;

    // ── Uyarı Oluşturma ──────────────────────────────────────

    /// <summary>
    /// Fiyat değişimi sonrası uyarı kaydı oluşturur.
    /// </summary>
    public async Task<Uyari> OlusturAsync(
        int urunId,
        int userId,
        UyariTipi tip,
        string baslik,
        string mesaj)
    {
        var uyari = new Uyari
        {
            UrunId            = urunId,
            UserId            = userId,
            Tip               = tip,
            Baslik            = baslik,
            Mesaj             = mesaj,
            OlusturulmaTarihi = DateTime.UtcNow,
            Okundu            = false
        };

        _context.Uyarilar.Add(uyari);
        await _context.SaveChangesAsync();

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "[UyariService] Uyarı oluşturuldu — Kullanıcı: {UserId}, Ürün: {UrunId}, Tip: {Tip}",
                userId, urunId, tip);
        }

        // E-posta gönderimi — kullanıcının gerçek e-postasına gönder.
        // Kullanıcı EmailBildirimleriniAc=false ise atla.
        var kullanici = await _context.Kullanicilar
            .Where(k => k.Id == userId)
            .Select(k => new { k.Email, k.EmailBildirimleriniAc })
            .FirstOrDefaultAsync();

        if (kullanici is null || string.IsNullOrWhiteSpace(kullanici.Email))
        {
            _logger.LogWarning(
                "[UyariService] Kullanıcı {UserId} için e-posta bulunamadı — mail atlandı.", userId);
        }
        else if (!kullanici.EmailBildirimleriniAc)
        {
            _logger.LogInformation(
                "[UyariService] Kullanıcı {UserId} e-posta bildirimlerini kapatmış — mail atlandı.", userId);
        }
        else
        {
            await _emailService.SendEmailAsync(kullanici.Email, baslik, mesaj);
        }

        return uyari;
    }

    // ── Fiyat Değişimi Uyarı Kontrolü ────────────────────────

    /// <summary>
    /// Fiyat güncellendiğinde uygun uyarıları otomatik oluşturur.
    /// UrunService.FiyatGuncelleAsync tarafından çağrılır.
    /// </summary>
    public async Task FiyatDegisimUyariKontrolAsync(
        Urun urun,
        decimal eskiFiyat,
        decimal yeniFiyat,
        bool isRekorDusuk)
    {
        // Kullanıcı yoksa uyarı oluşturmaya gerek yok
        if (!urun.UserId.HasValue) return;

        var userId   = urun.UserId.Value;
        var degisim  = yeniFiyat - eskiFiyat;

        // 1. Fiyat Düşüşü
        if (degisim < 0 && urun.FiyatDususuBildir)
        {
            var yuzde = Math.Abs(Math.Round(degisim / eskiFiyat * 100, 1));
            await OlusturAsync(
                urun.Id, userId, UyariTipi.FiyatDususu,
                baslik: $"Fiyat Düştü! 🎉 — {urun.Ad}",
                mesaj:  $"Ürün fiyatı %{yuzde} düşerek {yeniFiyat:N2} ₺ oldu. (Önceki: {eskiFiyat:N2} ₺)");
        }

        // 2. Fiyat Artışı
        if (degisim > 0 && urun.FiyatArtisiiBildir)
        {
            var yuzde = Math.Round(degisim / eskiFiyat * 100, 1);
            await OlusturAsync(
                urun.Id, userId, UyariTipi.FiyatArtisi,
                baslik: $"Fiyat Arttı ⬆ — {urun.Ad}",
                mesaj:  $"Ürün fiyatı %{yuzde} artarak {yeniFiyat:N2} ₺ oldu. (Önceki: {eskiFiyat:N2} ₺)");
        }

        // 3. Hedef Fiyata Ulaşıldı
        if (urun.HedefFiyati.HasValue && yeniFiyat <= urun.HedefFiyati.Value)
        {
            // Bugün için aynı uyarı tekrar oluşturulmasın
            var bugunVarMi = await _context.Uyarilar.AnyAsync(u =>
                u.UrunId == urun.Id &&
                u.UserId == userId &&
                u.Tip    == UyariTipi.HedefFiyataBasti &&
                u.OlusturulmaTarihi >= DateTime.UtcNow.Date);

            if (!bugunVarMi)
                await OlusturAsync(
                    urun.Id, userId, UyariTipi.HedefFiyataBasti,
                    baslik: $"Hedef Fiyata Ulaşıldı! 🎯 — {urun.Ad}",
                    mesaj:  $"Ürün hedef fiyatınız olan {urun.HedefFiyati:N2} ₺ değerine ulaştı! Güncel fiyat: {yeniFiyat:N2} ₺");
        }

        // 4. Rekor Düşük Fiyat
        if (isRekorDusuk && degisim < 0)
        {
            var bugunVarMi = await _context.Uyarilar.AnyAsync(u =>
                u.UrunId == urun.Id &&
                u.UserId == userId &&
                u.Tip    == UyariTipi.RekorDusuk &&
                u.OlusturulmaTarihi >= DateTime.UtcNow.Date);

            if (!bugunVarMi)
                await OlusturAsync(
                    urun.Id, userId, UyariTipi.RekorDusuk,
                    baslik: $"Rekor Düşük Fiyat! 🏆 — {urun.Ad}",
                    mesaj:  $"Ürün takip edildiğinden bu yana en düşük fiyatına ulaştı: {yeniFiyat:N2} ₺");
        }
    }

    // ── Okuma ────────────────────────────────────────────────

    /// <summary>Kullanıcıya ait uyarıları listeler</summary>
    public async Task<List<Uyari>> GetByKullaniciAsync(int kullaniciId, bool? sadeceokunmamis = null)
    {
        var query = _context.Uyarilar
            .AsNoTracking()
            .Include(u => u.Urun)
            .Where(u => u.UserId == kullaniciId);

        if (sadeceokunmamis == true)
            query = query.Where(u => !u.Okundu);

        return await query
            .OrderByDescending(u => u.OlusturulmaTarihi)
            .ToListAsync();
    }

    // ── Okundu İşaretleme ────────────────────────────────────

    /// <summary>Belirtilen uyarıyı okundu olarak işaretler</summary>
    public async Task<bool> OkunduIsaretle(int uyariId, int kullaniciId)
    {
        var uyari = await _context.Uyarilar
            .FirstOrDefaultAsync(u => u.Id == uyariId && u.UserId == kullaniciId);

        if (uyari is null) return false;

        uyari.Okundu       = true;
        uyari.OkunduğuTarih = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>Kullanıcının tüm okunmamış uyarılarını okundu yapar</summary>
    public async Task<int> TumunuOkunduIsaretle(int kullaniciId)
    {
        var okunmamislar = await _context.Uyarilar
            .Where(u => u.UserId == kullaniciId && !u.Okundu)
            .ToListAsync();

        var tarih = DateTime.UtcNow;
        foreach (var uyari in okunmamislar)
        {
            uyari.Okundu       = true;
            uyari.OkunduğuTarih = tarih;
        }

        await _context.SaveChangesAsync();
        return okunmamislar.Count;
    }
}
