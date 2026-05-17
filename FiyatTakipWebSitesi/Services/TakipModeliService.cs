// =====================================================
// TakipModeliService.cs
// Hibrit takip sisteminin CRUD katmanı. Kullanıcının
// takip ettiği UrunModeli kayıtlarını yönetir. Bir model
// takip edilince mevcut "en ucuz" fiyatı baseline olarak
// kaydedilir (SonBildirilenEnUcuz). UyariService bu değeri
// kullanarak "yeni en ucuz daha mı düştü?" kontrolü yapar.
// =====================================================

using FiyatTakipWebSitesi.Data;
using FiyatTakipWebSitesi.Models;
using Microsoft.EntityFrameworkCore;

namespace FiyatTakipWebSitesi.Services;

public class TakipModeliService(ApplicationDbContext context, ILogger<TakipModeliService> logger)
{
    private readonly ApplicationDbContext _context = context;
    private readonly ILogger<TakipModeliService> _logger = logger;

    // ── Takip Ekle/Çıkar ──────────────────────────────────────

    /// <summary>
    /// Kullanıcıyı bu modele takipçi yapar. Zaten takipteyse (Aktif=true) hiçbir
    /// şey yapmaz. Önceden silinmiş (Aktif=false) bir kayıt varsa onu yeniden
    /// aktifleştirir. Yeni kayıtta mevcut en ucuz fiyatı baseline olarak yazar.
    /// </summary>
    public async Task<bool> TakipEkleAsync(int userId, int modelId)
    {
        var mevcut = await _context.TakipModelleri
            .FirstOrDefaultAsync(t => t.UserId == userId && t.UrunModeliId == modelId);

        // Mevcut en ucuz fiyatı çek (baseline için)
        var enUcuz = await _context.Urunler
            .Where(u => u.UrunModeliId == modelId && u.Aktif && u.SonFiyati > 0)
            .Select(u => (decimal?)u.SonFiyati)
            .OrderBy(f => f)
            .FirstOrDefaultAsync();

        if (mevcut is not null)
        {
            // Zaten varsa: pasifse aktifleştir, baseline'ı güncelle
            if (!mevcut.Aktif)
            {
                mevcut.Aktif = true;
                mevcut.SonBildirilenEnUcuz = enUcuz;
                mevcut.EklendigiTarih = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            return mevcut.Aktif;
        }

        var yeni = new TakipModeli
        {
            UserId = userId,
            UrunModeliId = modelId,
            SonBildirilenEnUcuz = enUcuz,
            EklendigiTarih = DateTime.UtcNow,
            Aktif = true,
            FiyatDususuBildir = true,
            FiyatArtisiBildir = false,
        };

        _context.TakipModelleri.Add(yeni);
        await _context.SaveChangesAsync();

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "[TakipModeli] Takip eklendi — Kullanıcı: {UserId}, Model: {ModelId}, Baseline: {Baseline}",
                userId, modelId, enUcuz);
        }
        return true;
    }

    /// <summary>Soft delete — Aktif=false. Geri istendiğinde TakipEkleAsync onu reaktive eder.</summary>
    public async Task<bool> TakiptenCikAsync(int userId, int modelId)
    {
        var kayit = await _context.TakipModelleri
            .FirstOrDefaultAsync(t => t.UserId == userId && t.UrunModeliId == modelId && t.Aktif);

        if (kayit is null) return false;

        kayit.Aktif = false;
        await _context.SaveChangesAsync();
        return true;
    }

    // ── Sorgular ──────────────────────────────────────────────

    /// <summary>Kullanıcının bu modeli takip edip etmediği.</summary>
    public async Task<bool> TakipteMiAsync(int userId, int modelId) =>
        await _context.TakipModelleri
            .AnyAsync(t => t.UserId == userId && t.UrunModeliId == modelId && t.Aktif);

    /// <summary>Kullanıcının takip ettiği tüm ModelId'leri (UI'da lookup için).</summary>
    public async Task<HashSet<int>> KullaniciTakipModelIdleriAsync(int userId) =>
        (await _context.TakipModelleri
            .Where(t => t.UserId == userId && t.Aktif)
            .Select(t => t.UrunModeliId)
            .ToListAsync())
        .ToHashSet();

    /// <summary>
    /// Kullanıcının takip ettiği tüm modeller + her birinin canlı en ucuz fiyatı
    /// ve site listemeleri. /takip sayfası için.
    /// </summary>
    public async Task<List<TakipModelKart>> GetKullaniciKartlariAsync(int userId)
    {
        // Önce takip ettiği model ID'lerini al
        var takipKayitlari = await _context.TakipModelleri
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.Aktif)
            .OrderByDescending(t => t.EklendigiTarih)
            .ToListAsync();

        if (takipKayitlari.Count == 0) return new();

        var modelIds = takipKayitlari.Select(t => t.UrunModeliId).ToList();

        // Modelleri ve aktif listelemelerini çek
        var modeller = await _context.UrunModelleri
            .AsNoTracking()
            .Include(m => m.Kategori)
            .Include(m => m.Listeler.Where(l => l.Aktif))
            .Where(m => modelIds.Contains(m.Id))
            .ToListAsync();

        return takipKayitlari.Select(t =>
        {
            var model = modeller.FirstOrDefault(m => m.Id == t.UrunModeliId);
            if (model is null) return null;

            var fiyatliListeler = model.Listeler.Where(l => l.SonFiyati > 0).OrderBy(l => l.SonFiyati).ToList();
            var enUcuzListe = fiyatliListeler.FirstOrDefault();
            var saticiSiralamasi = model.Listeler.OrderBy(l => l.SonFiyati > 0 ? 0 : 1)
                .ThenBy(l => l.SonFiyati > 0 ? l.SonFiyati : decimal.MaxValue)
                .ToList();

            return new TakipModelKart
            {
                TakipId = t.Id,
                ModelId = model.Id,
                ModelAd = model.Ad,
                Resim = !string.IsNullOrEmpty(model.Resim)
                    ? model.Resim
                    : model.Listeler.Select(l => l.Resim).FirstOrDefault(r => !string.IsNullOrEmpty(r)) ?? "",
                KategoriAd = model.Kategori?.Ad ?? "",
                EnUcuzFiyat = enUcuzListe?.SonFiyati,
                EnUcuzSatici = enUcuzListe?.Satici ?? "",
                EnUcuzUrunId = enUcuzListe?.Id ?? 0,
                ToplamSite = model.Listeler.Count,
                HedefFiyat = t.HedefFiyat,
                EklendigiTarih = t.EklendigiTarih,
                Satkalemleri = saticiSiralamasi.Select(l => new SaticiSatir
                {
                    UrunId = l.Id,
                    Satici = l.Satici,
                    Fiyat = l.SonFiyati,
                    URL = l.URL,
                    EnUcuzMu = enUcuzListe != null && l.Id == enUcuzListe.Id,
                }).ToList(),
            };
        })
        .Where(k => k is not null)!
        .ToList()!;
    }

    /// <summary>
    /// Bir modelin fiyatı güncellendiğinde çağrılır. O modeli takip eden TÜM
    /// kullanıcılara bakar; en ucuz fiyatları SonBildirilenEnUcuz değerinin
    /// altına düştüyse uyarı tetikler. Spam mail engelleme: baseline güncellenir.
    /// </summary>
    public async Task<List<TakipUyariBilgisi>> FiyatDegisimiKontrolEtAsync(int modelId)
    {
        // Modelin şu anki en ucuz fiyatı
        var enUcuz = await _context.Urunler
            .AsNoTracking()
            .Where(u => u.UrunModeliId == modelId && u.Aktif && u.SonFiyati > 0)
            .OrderBy(u => u.SonFiyati)
            .Select(u => new { u.SonFiyati, u.Satici, u.Id })
            .FirstOrDefaultAsync();

        if (enUcuz is null) return new(); // Hiç fiyatlı listing yok

        var takipler = await _context.TakipModelleri
            .Where(t => t.UrunModeliId == modelId && t.Aktif)
            .Include(t => t.UrunModeli)
            .ToListAsync();

        var uyarilar = new List<TakipUyariBilgisi>();

        foreach (var t in takipler)
        {
            var oncekiEnUcuz = t.SonBildirilenEnUcuz;
            var yeniEnUcuz = enUcuz.SonFiyati;

            // İlk fiyat — baseline kaydet, uyarı atma
            if (!oncekiEnUcuz.HasValue || oncekiEnUcuz.Value == 0)
            {
                t.SonBildirilenEnUcuz = yeniEnUcuz;
                continue;
            }

            // Aynı seviyede — uyarı yok
            if (oncekiEnUcuz.Value == yeniEnUcuz) continue;

            var isDusus = yeniEnUcuz < oncekiEnUcuz.Value;
            var isArtis = yeniEnUcuz > oncekiEnUcuz.Value;

            if ((isDusus && t.FiyatDususuBildir) || (isArtis && t.FiyatArtisiBildir))
            {
                uyarilar.Add(new TakipUyariBilgisi
                {
                    UserId = t.UserId,
                    ModelId = modelId,
                    ModelAd = t.UrunModeli?.Ad ?? "",
                    EskiEnUcuz = oncekiEnUcuz.Value,
                    YeniEnUcuz = yeniEnUcuz,
                    EnUcuzSatici = enUcuz.Satici,
                    HedefFiyat = t.HedefFiyat,
                    HedefBasti = t.HedefFiyat.HasValue && yeniEnUcuz <= t.HedefFiyat.Value
                                 && oncekiEnUcuz.Value > t.HedefFiyat.Value,
                });
            }

            // Baseline güncelle — spam engel
            t.SonBildirilenEnUcuz = yeniEnUcuz;
        }

        if (uyarilar.Count > 0 || takipler.Any(t => t.SonBildirilenEnUcuz != null))
        {
            await _context.SaveChangesAsync();
        }

        return uyarilar;
    }
}

// ── DTOs ─────────────────────────────────────────────────────

public class TakipModelKart
{
    public int TakipId { get; set; }
    public int ModelId { get; set; }
    public string ModelAd { get; set; } = "";
    public string Resim { get; set; } = "";
    public string KategoriAd { get; set; } = "";
    public decimal? EnUcuzFiyat { get; set; }
    public string EnUcuzSatici { get; set; } = "";
    public int EnUcuzUrunId { get; set; }
    public int ToplamSite { get; set; }
    public decimal? HedefFiyat { get; set; }
    public DateTime EklendigiTarih { get; set; }
    public List<SaticiSatir> Satkalemleri { get; set; } = new();
}

public class SaticiSatir
{
    public int UrunId { get; set; }
    public string Satici { get; set; } = "";
    public decimal Fiyat { get; set; }
    public string URL { get; set; } = "";
    public bool EnUcuzMu { get; set; }
}

public class TakipUyariBilgisi
{
    public int UserId { get; set; }
    public int ModelId { get; set; }
    public string ModelAd { get; set; } = "";
    public decimal EskiEnUcuz { get; set; }
    public decimal YeniEnUcuz { get; set; }
    public string EnUcuzSatici { get; set; } = "";
    public decimal? HedefFiyat { get; set; }
    public bool HedefBasti { get; set; }
}
