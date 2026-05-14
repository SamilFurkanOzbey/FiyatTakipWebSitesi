// =====================================================
// UrunService.cs
// Bu servis, takip edilen ürünlerin veritabanı işlemlerini
// yönetir. Tüm ürünleri, belirli bir ürünü, kategoriye
// veya kullanıcıya göre ürünleri listeleme; yeni ürün ekleme
// (başlangıç fiyat kaydı ile birlikte), fiyat güncelleme
// (fiyat geçmişine otomatik kayıt atar), ürün bilgilerini
// düzenleme ve soft-delete ile silme işlemlerini sağlar.
// =====================================================

using FiyatTakipWebSitesi.Data;
using FiyatTakipWebSitesi.Models;
using Microsoft.EntityFrameworkCore;

namespace FiyatTakipWebSitesi.Services;

public class UrunService(
    ApplicationDbContext context,
    UyariService uyariService,
    FiyatGecmisiService fiyatGecmisiService,
    ResimCacheService resimCacheService,
    ScraperService scraperService,
    ILogger<UrunService> logger)
{
    private readonly ApplicationDbContext _context = context;
    private readonly UyariService _uyariService = uyariService;
    private readonly FiyatGecmisiService _fiyatGecmisiService = fiyatGecmisiService;
    private readonly ResimCacheService _resimCacheService = resimCacheService;
    private readonly ScraperService _scraperService = scraperService;
    private readonly ILogger<UrunService> _logger = logger;

    // Aynı ürün için iki yenileme denemesi arasındaki minimum süre.
    // Anti-bot tetiklenmesini ve kullanıcıların butonu spam'lemesini önler.
    private static readonly TimeSpan _yenilemeBekleme = TimeSpan.FromMinutes(5);

    // --- Okuma ---

    public async Task<List<Urun>> GetAllAsync(int skip = 0, int take = 50)
        => await _context.Urunler
            .AsNoTracking()
            .Include(u => u.Kategori)
            .Include(u => u.Kullanici)
            .Where(u => u.Aktif)
            .OrderByDescending(u => u.EklendigiTarih)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

    public async Task<Urun?> GetByIdAsync(int id)
        => await _context.Urunler
            .AsNoTracking()
            .Include(u => u.Kategori)
            .Include(u => u.FiyatGecmisleri.OrderByDescending(fg => fg.Tarih).Take(30))
            .Include(u => u.Uyarilar)
            .FirstOrDefaultAsync(u => u.Id == id);

    public async Task<List<Urun>> GetByKategoriAsync(int kategoriId, int skip = 0, int take = 50)
        => await _context.Urunler
            .AsNoTracking()
            .Include(u => u.Kategori)
            .Where(u => u.KategoriId == kategoriId && u.Aktif)
            .OrderByDescending(u => u.EklendigiTarih)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

    public async Task<List<Urun>> GetByKullaniciAsync(int kullaniciId)
        => await _context.Urunler
            .AsNoTracking()
            .Include(u => u.Kategori)
            .Where(u => u.UserId == kullaniciId && u.Aktif)
            .OrderByDescending(u => u.EklendigiTarih)
            .ToListAsync();

    public async Task<List<Urun>> AraAsync(string aramaMetni, int limit = 8)
    {
        if (string.IsNullOrWhiteSpace(aramaMetni))
            return [];

        var metin = aramaMetni.Trim();

        return await _context.Urunler
            .AsNoTracking()
            .Include(u => u.Kategori)
            .Where(u => u.Aktif && EF.Functions.Like(u.Ad, $"%{metin}%"))
            .OrderByDescending(u => u.EklendigiTarih)
            .Take(limit)
            .ToListAsync();
    }

    // --- Yazma ---

    public async Task<Urun> EkleAsync(Urun urun)
    {
        urun.EklendigiTarih = DateTime.UtcNow;
        urun.SonGuncellemeTarihi = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(urun.Resim))
            urun.Resim = await _resimCacheService.ResimOnbellegeAlAsync(urun.Resim);

        _context.Urunler.Add(urun);
        await _context.SaveChangesAsync();

        // Başlangıç fiyat kaydı
        var ilkFiyat = new FiyatGecmisi
        {
            UrunId = urun.Id,
            Fiyat = urun.BaslangicFiyati,
            Tarih = DateTime.UtcNow,
            StokDurumuMevcutMu = true,
            Durum = "Başlangıç"
        };
        _context.FiyatGecmisleri.Add(ilkFiyat);
        await _context.SaveChangesAsync();

        return urun;
    }

    public async Task<bool> FiyatGuncelleAsync(int urunId, decimal yeniFiyat, bool stokVar = true)
    {
        var urun = await _context.Urunler
            .Include(u => u.Kullanici)
            .FirstOrDefaultAsync(u => u.Id == urunId);
        if (urun is null) return false;

        var eskiFiyat = urun.SonFiyati;
        var degisim = yeniFiyat - eskiFiyat;
        var degisimYuzdesi = eskiFiyat != 0 ? (degisim / eskiFiyat) * 100 : 0;

        urun.SonFiyati = yeniFiyat;
        urun.SonGuncellemeTarihi = DateTime.UtcNow;

        string durum = degisim switch
        {
            < 0 => "Düşüş",
            > 0 => "Artış",
            _ => "Değişmedi"
        };

        var fiyatGecmisi = new FiyatGecmisi
        {
            UrunId = urunId,
            Fiyat = yeniFiyat,
            OncekiFiyat = eskiFiyat,
            FiyatDegisimi = degisim,
            FiyatDegisimYüzdesi = Math.Round(degisimYuzdesi, 2),
            Tarih = DateTime.UtcNow,
            StokDurumuMevcutMu = stokVar,
            Durum = durum
        };

        _context.FiyatGecmisleri.Add(fiyatGecmisi);
        await _context.SaveChangesAsync();

        // Uyarı kontrolü (sadece fiyat değiştiyse)
        if (degisim != 0)
        {
            var isRekorDusuk = await _fiyatGecmisiService.IsRekorDusukAsync(urunId, yeniFiyat);
            await _uyariService.FiyatDegisimUyariKontrolAsync(urun, eskiFiyat, yeniFiyat, isRekorDusuk);
        }

        return true;
    }

    public async Task<bool> GuncelleAsync(Urun urun)
    {
        var mevcut = await _context.Urunler.FindAsync(urun.Id);
        if (mevcut is null) return false;

        if (!string.IsNullOrWhiteSpace(urun.Resim) && urun.Resim != mevcut.Resim)
            mevcut.Resim = await _resimCacheService.ResimOnbellegeAlAsync(urun.Resim);
        else if (!string.IsNullOrWhiteSpace(urun.Resim))
            mevcut.Resim = urun.Resim;

        mevcut.Ad = urun.Ad;
        mevcut.URL = urun.URL;
        mevcut.KategoriId = urun.KategoriId;
        mevcut.HedefFiyati = urun.HedefFiyati;
        mevcut.FiyatDususuBildir = urun.FiyatDususuBildir;
        mevcut.FiyatArtisiiBildir = urun.FiyatArtisiiBildir;
        mevcut.SonGuncellemeTarihi = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    // --- Tek ürün yenileme (kullanıcı butonu) ---

    /// <summary>
    /// Tek bir ürünün fiyatını anında scrape eder ve günceller.
    /// Son 5 dakika içinde zaten güncellenmişse scrape etmez,
    /// mevcut veriyi "güncel" olarak geri döner.
    /// </summary>
    public async Task<YenilemeSonucu> TekUrunYenileAsync(int id)
    {
        var urun = await _context.Urunler.FindAsync(id);
        if (urun is null)
            return YenilemeSonucu.Hata("Ürün bulunamadı.");

        if (!urun.Aktif || string.IsNullOrWhiteSpace(urun.URL))
            return YenilemeSonucu.Hata("Bu ürün takip için uygun değil.");

        var gecenSure = DateTime.UtcNow - urun.SonGuncellemeTarihi;
        if (gecenSure < _yenilemeBekleme)
        {
            return new YenilemeSonucu(
                Yenilendi: false,
                ZatenGuncel: true,
                Mesaj: "Bu ürün güncel.",
                EskiFiyat: urun.SonFiyati,
                YeniFiyat: urun.SonFiyati,
                SonGuncellemeTarihi: urun.SonGuncellemeTarihi);
        }

        try
        {
            var detay = await _scraperService.GetUrunDetayAsync(urun.URL);

            if (detay.FiyatSayi <= 0)
            {
                _logger.LogWarning(
                    "[UrunService] Ürün #{Id} için scrape başarılı ama fiyat alınamadı: '{Ham}'",
                    id, detay.Fiyat);
                return YenilemeSonucu.Hata("Şu anda fiyat alınamadı, biraz sonra dene.");
            }

            var eskiFiyat = urun.SonFiyati;
            await FiyatGuncelleAsync(id, detay.FiyatSayi, stokVar: true);

            return new YenilemeSonucu(
                Yenilendi: true,
                ZatenGuncel: false,
                Mesaj: eskiFiyat == detay.FiyatSayi
                    ? "Fiyat değişmedi."
                    : "Fiyat güncellendi.",
                EskiFiyat: eskiFiyat,
                YeniFiyat: detay.FiyatSayi,
                SonGuncellemeTarihi: DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[UrunService] Ürün #{Id} yenilenirken hata: {Mesaj}", id, ex.Message);
            return YenilemeSonucu.Hata("Yenileme başarısız oldu, lütfen tekrar dene.");
        }
    }

    public async Task<bool> SilAsync(int id)
    {
        var urun = await _context.Urunler.FindAsync(id);
        if (urun is null) return false;

        urun.Aktif = false; // Soft delete
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task SeedOrnekUrunlerAsync()
    {
        if (await _context.Urunler.AnyAsync()) return;

        var kategori = await _context.Kategoriler.FirstOrDefaultAsync(k => k.Ad == "Elektronik")
                       ?? await _context.Kategoriler.FirstOrDefaultAsync();

        if (kategori is null) return;

        var urunler = new List<Urun>
        {
            new()
            {
                Ad = "Apple iPhone 15 (128 GB) - Siyah",
                URL = "https://www.hepsiburada.com/iphone-15-128-gb-p-HBCV00004ZEWB8",
                Resim = "https://productimages.hepsiburada.net/s/448/550/110000483863414.jpg",
                Satici = "Hepsiburada",
                BaslangicFiyati = 52999,
                SonFiyati = 51499,
                HedefFiyati = 49000,
                KategoriId = kategori.Id,
                ParaBirimi = "TRY",
                Aktif = true
            },
            new()
            {
                Ad = "Samsung Galaxy S24 Ultra 512 GB",
                URL = "https://www.hepsiburada.com/samsung-galaxy-s24-ultra-512-gb-p-HBCV00005OPYYR",
                Resim = "https://productimages.hepsiburada.net/s/525/550/110000582239665.jpg",
                Satici = "Samsung",
                BaslangicFiyati = 69999,
                SonFiyati = 69999,
                HedefFiyati = 65000,
                KategoriId = kategori.Id,
                ParaBirimi = "TRY",
                Aktif = true
            }
        };

        foreach (var u in urunler)
        {
            await EkleAsync(u);
        }
    }

    public async Task SeedSifirlaAsync()
    {
        // Tüm ürünleri ve ilişkili fiyat/uyarı kayıtlarını kalıcı olarak siler
        var hepsi = await _context.Urunler.ToListAsync();
        _context.Urunler.RemoveRange(hepsi);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Sistem katalogunu seed eder: UrunModeli + her modelin N adet site
    /// listelemesini (Urun) ekler. Fiyatlar boş (0) bırakılır — Hangfire'ın
    /// FiyatGuncellemeJob'ı 06:00/18:00'de bunları scrape edip doldurur.
    /// Kullanıcı manuel "Yenile" butonu ile de tetikleyebilir.
    /// Idempotent: UrunModelleri tablosunda kayıt varsa hiçbir şey yapmaz.
    /// </summary>
    public async Task SeedKatalogAsync()
    {
        if (await _context.UrunModelleri.AnyAsync()) return;

        var elektronik = await _context.Kategoriler
            .FirstOrDefaultAsync(k => k.Ad == "Elektronik");
        if (elektronik is null)
        {
            _logger.LogWarning("[Seed] Elektronik kategorisi bulunamadı, katalog seed iptal edildi.");
            return;
        }

        // 3 telefon modeli × 4-5 site = 14 listeleme
        var katalog = new (string Ad, (string Satici, string URL)[] Linkler)[]
        {
            ("iPhone 15 128GB", new[]
            {
                ("Hepsiburada", "https://www.hepsiburada.com/apple-iphone-15-128-gb-siyah-p-HBCV00004X9ZCH"),
                ("Çiçeksepeti", "https://www.ciceksepeti.com/apple-iphone-15-128-gb-apple-turkiye-garantili-kcs475224659"),
                ("PttAVM",      "https://www.pttavm.com/apple-iphone-15-128-6-gb-ram-5g-apple-turkiye-garantili-p-658112268"),
                ("Teknosa",     "https://www.teknosa.com/apple-iphone-15-128gb-siyah-p-125079197"),
            }),
            ("Samsung Galaxy S24 Ultra 256GB", new[]
            {
                ("Hepsiburada", "https://www.hepsiburada.com/samsung-galaxy-s24-ultra-256-gb-12-gb-ram-samsung-turkiye-garantili-siyah-p-HBCV00005MLL3N"),
                ("Çiçeksepeti", "https://www.ciceksepeti.com/samsung-galaxy-s24-ultra-256-gb-12-gb-ram-samsung-turkiye-garantili-kcm11436817"),
                ("PttAVM",      "https://www.pttavm.com/samsung-galaxy-s24-ultra-256-gb-12-gb-ram-samsung-turkiye-garantili-titanyum-siyah-p-1246216559"),
                ("Teknosa",     "https://www.teknosa.com/samsung-galaxy-s24-ultra-12gb256gb-titanyum-black-akilli-telefon-p-125079454"),
                ("n11",         "https://www.n11.com/urun/samsung-galaxy-s24-ultra-12-gb-256-gb-samsung-turkiye-garantili-47977817"),
            }),
            ("Xiaomi Redmi Note 13 Pro 256GB", new[]
            {
                ("Çiçeksepeti", "https://www.ciceksepeti.com/xiaomi-redmi-note-13-pro-8-gb-256-gb-xiaomi-turkiye-garantili-kcm88276666"),
                ("PttAVM",      "https://www.pttavm.com/xiaomi-redmi-note-13-pro-256-8-gb-ram-xiaomi-turkiye-garantili-p-794300467"),
                ("Teknosa",     "https://www.teknosa.com/xiaomi-redmi-note-13-pro-8-gb-256-gb-siyah-cep-telefonu-xiaomi-turkiye-garantili-p-780010574"),
                ("n11",         "https://www.n11.com/urun/xiaomi-redmi-note-13-pro-8-gb-256-gb-xiaomi-turkiye-garantili-48308143"),
            }),
        };

        int eklenenModel = 0, eklenenListe = 0;

        foreach (var (modelAdi, linkler) in katalog)
        {
            var model = new UrunModeli
            {
                Ad = modelAdi,
                KategoriId = elektronik.Id,
                OlusturulmaTarihi = DateTime.UtcNow,
                Aktif = true,
            };
            _context.UrunModelleri.Add(model);
            await _context.SaveChangesAsync(); // model.Id üretilsin

            foreach (var (satici, url) in linkler)
            {
                var liste = new Urun
                {
                    Ad = modelAdi,                       // Scraper sonradan günceller
                    URL = url,
                    Satici = satici,
                    ParaBirimi = "TRY",
                    KategoriId = elektronik.Id,
                    UrunModeliId = model.Id,
                    UserId = null,                       // Sistem ürünü (kullanıcı eklemesi değil)
                    BaslangicFiyati = 0,                 // Scraper ilk run'da dolduracak
                    SonFiyati = 0,
                    EklendigiTarih = DateTime.UtcNow,
                    SonGuncellemeTarihi = DateTime.MinValue,  // İlk scrape'i tetikle
                    Aktif = true,
                };
                _context.Urunler.Add(liste);
                eklenenListe++;
            }
            await _context.SaveChangesAsync();
            eklenenModel++;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "[Seed] Katalog seed tamamlandı — {Model} model, {Liste} listeleme eklendi.",
                eklenenModel, eklenenListe);
        }
    }
}

/// <summary>
/// "Yenile" butonu sonucu — frontend'in göstereceği mesaj ve değerleri taşır.
/// </summary>
public sealed record YenilemeSonucu(
    bool Yenilendi,
    bool ZatenGuncel,
    string Mesaj,
    decimal? EskiFiyat,
    decimal? YeniFiyat,
    DateTime SonGuncellemeTarihi)
{
    public static YenilemeSonucu Hata(string mesaj) =>
        new(Yenilendi: false, ZatenGuncel: false, Mesaj: mesaj,
            EskiFiyat: null, YeniFiyat: null, SonGuncellemeTarihi: DateTime.MinValue);
}
