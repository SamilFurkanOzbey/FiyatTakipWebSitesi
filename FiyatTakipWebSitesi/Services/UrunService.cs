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

    public async Task<List<Urun>> GetAllAsync()
        => await _context.Urunler
            .AsNoTracking()
            .Include(u => u.Kategori)
            .Include(u => u.Kullanici)
            .Where(u => u.Aktif)
            .OrderByDescending(u => u.EklendigiTarih)
            .ToListAsync();

    public async Task<Urun?> GetByIdAsync(int id)
        => await _context.Urunler
            .AsNoTracking()
            .Include(u => u.Kategori)
            .Include(u => u.FiyatGecmisleri.OrderByDescending(fg => fg.Tarih).Take(30))
            .Include(u => u.Uyarilar)
            .FirstOrDefaultAsync(u => u.Id == id);

    public async Task<List<Urun>> GetByKategoriAsync(int kategoriId)
        => await _context.Urunler
            .AsNoTracking()
            .Include(u => u.Kategori)
            .Where(u => u.KategoriId == kategoriId && u.Aktif)
            .OrderByDescending(u => u.EklendigiTarih)
            .ToListAsync();

    public async Task<List<Urun>> GetByKullaniciAsync(int kullaniciId)
        => await _context.Urunler
            .AsNoTracking()
            .Include(u => u.Kategori)
            .Where(u => u.UserId == kullaniciId && u.Aktif)
            .OrderByDescending(u => u.EklendigiTarih)
            .ToListAsync();

    /// <summary>
    /// "Takip Ettiklerim" sayfası için: sadece kullanıcının elle eklediği ürünler
    /// (UrunModeliId == NULL) — sistem kataloğundaki ürünler (UrunModeliId != NULL)
    /// burada GÖSTERİLMEZ.
    /// </summary>
    public async Task<List<Urun>> GetKullaniciTakipleriAsync()
        => await _context.Urunler
            .AsNoTracking()
            .Include(u => u.Kategori)
            .Where(u => u.Aktif && u.UrunModeliId == null)
            .OrderByDescending(u => u.EklendigiTarih)
            .ToListAsync();

    /// <summary>
    /// "Kategoriler" sayfası için: bir kategorideki tüm MODELLERİ (UrunModeli) getirir.
    /// Her model kendi sitedeki listelemeleri (Listeler) ile beraber gelir — kartta
    /// "en düşük fiyat" ve "kaç sitede" göstermek için lazım.
    /// </summary>
    public async Task<List<UrunModeli>> GetModellerByKategoriAsync(int kategoriId)
        => await _context.UrunModelleri
            .AsNoTracking()
            .Include(m => m.Listeler.Where(l => l.Aktif))
            .Where(m => m.Aktif && m.KategoriId == kategoriId)
            .OrderBy(m => m.Ad)
            .ToListAsync();

    /// <summary>
    /// "Model Detay" sayfası için: tek bir modeli, sitelerdeki tüm aktif
    /// listelemeleri (Listeler) ve kategori bilgisi ile birlikte getirir.
    /// </summary>
    public async Task<UrunModeli?> GetModelByIdAsync(int id)
        => await _context.UrunModelleri
            .AsNoTracking()
            .Include(m => m.Kategori)
            .Include(m => m.Listeler.Where(l => l.Aktif))
            .FirstOrDefaultAsync(m => m.Id == id);

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

    // --- Scraper sonucunu ürüne uygula ---

    /// <summary>
    /// Scraper'dan dönen <see cref="UrunDetay"/>'i alır: fiyatı günceller
    /// (mevcut <see cref="FiyatGuncelleAsync"/> mantığı — geçmiş, uyarılar),
    /// ek olarak Ad ve Resim boşsa onları da scrape verisiyle doldurur.
    /// Hem Hangfire periyodik job'ı hem kullanıcı "Yenile" butonu bunu kullanır.
    /// </summary>
    public async Task<bool> OtomatikGuncelleAsync(int urunId, UrunDetay detay)
    {
        if (detay.FiyatSayi <= 0)
        {
            // Scrape başarısız oldu. Ürün DAHA ÖNCE çalışıyorduysa (SonFiyati > 0)
            // bu büyük ihtimalle "stok kalktı / listeleme kapandı" demektir →
            // pasifleştir. Yeni seed edilen (henüz hiç fiyatı olmamış) ürünleri
            // ilk denemede pasifleştirmeyiz, çünkü geçici anti-bot/network
            // sorunu olabilir.
            var pasif = await _context.Urunler.FirstOrDefaultAsync(u => u.Id == urunId);
            if (pasif is not null && pasif.Aktif && pasif.SonFiyati > 0)
            {
                pasif.Aktif = false;
                await _context.SaveChangesAsync();
                _logger.LogWarning(
                    "[UrunService] Ürün #{Id} otomatik pasifleştirildi (Satıcı: {Satici}, son fiyat: {Fiyat:N2}) — scrape fiyat döndüremedi, muhtemelen stok yok.",
                    urunId, pasif.Satici, pasif.SonFiyati);
            }
            return false;
        }

        // Önce fiyat akışı çalışsın (FiyatGecmisi + Uyari + SonGuncellemeTarihi)
        var basarili = await FiyatGuncelleAsync(urunId, detay.FiyatSayi, stokVar: true);
        if (!basarili) return false;

        // Sonra Ad/Resim'i tamamla (sadece boşsa — seed'lediğimiz model adlarını
        // veya kullanıcının elle girdiği isimleri ezmeyiz)
        var urun = await _context.Urunler.FirstOrDefaultAsync(u => u.Id == urunId);
        if (urun is null) return true; // fiyat zaten kaydedildi

        bool degisti = false;

        if ((string.IsNullOrWhiteSpace(urun.Ad) || urun.Ad == "İsimsiz Ürün") &&
            !string.IsNullOrWhiteSpace(detay.Ad) && detay.Ad != "İsimsiz Ürün")
        {
            urun.Ad = detay.Ad;
            degisti = true;
        }

        if (string.IsNullOrWhiteSpace(urun.Resim) && !string.IsNullOrWhiteSpace(detay.ResimUrl))
        {
            urun.Resim = await _resimCacheService.ResimOnbellegeAlAsync(detay.ResimUrl);
            degisti = true;
        }

        if (degisti) await _context.SaveChangesAsync();
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
            await OtomatikGuncelleAsync(id, detay);

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
    /// Per-model idempotent: aynı isimli model zaten varsa atlar, yoksa ekler.
    /// Per-URL idempotent: aynı URL zaten kayıtlıysa o listelemeyi atlar.
    /// Bu sayede seed listesine yeni modeller eklendiğinde tekrar çalışır.
    /// </summary>
    public async Task SeedKatalogAsync()
    {
        // Kategorileri tek seferde çek (her satıra ayrı query atmamak için)
        var kategoriler = await _context.Kategoriler.ToDictionaryAsync(k => k.Ad, k => k.Id);
        if (kategoriler.Count == 0)
        {
            _logger.LogWarning("[Seed] Hiç kategori bulunamadı, katalog seed iptal edildi. Önce SeedVarsayilanKategorilerAsync çalışmalı.");
            return;
        }

        var katalog = new (string Kategori, string Ad, (string Satici, string URL)[] Linkler)[]
        {
            // ── Elektronik (telefonlar) ────────────────────────────────────
            ("Elektronik", "iPhone 15 128GB", new[]
            {
                // Çiçeksepeti listelemesi (kcs475224659) stoktan kalktı → çıkarıldı (2026-05-15).
                ("Hepsiburada", "https://www.hepsiburada.com/apple-iphone-15-128-gb-siyah-p-HBCV00004X9ZCH"),
                ("PttAVM",      "https://www.pttavm.com/apple-iphone-15-128-6-gb-ram-5g-apple-turkiye-garantili-p-658112268"),
                ("Teknosa",     "https://www.teknosa.com/apple-iphone-15-128gb-siyah-p-125079197"),
            }),
            ("Elektronik", "Samsung Galaxy S24 Ultra 256GB", new[]
            {
                // Çiçeksepeti listelemesi (kcm11436817) stoktan kalktı → çıkarıldı (2026-05-15).
                ("Hepsiburada", "https://www.hepsiburada.com/samsung-galaxy-s24-ultra-256-gb-12-gb-ram-samsung-turkiye-garantili-siyah-p-HBCV00005MLL3N"),
                ("PttAVM",      "https://www.pttavm.com/samsung-galaxy-s24-ultra-256-gb-12-gb-ram-samsung-turkiye-garantili-titanyum-siyah-p-1246216559"),
                ("Teknosa",     "https://www.teknosa.com/samsung-galaxy-s24-ultra-12gb256gb-titanyum-black-akilli-telefon-p-125079454"),
                ("n11",         "https://www.n11.com/urun/samsung-galaxy-s24-ultra-12-gb-256-gb-samsung-turkiye-garantili-47977817"),
            }),
            ("Elektronik", "Xiaomi Redmi Note 13 Pro 256GB", new[]
            {
                // Çiçeksepeti listelemesi (kcm88276666) stoktan kalktı → çıkarıldı (2026-05-13).
                ("PttAVM",      "https://www.pttavm.com/xiaomi-redmi-note-13-pro-256-8-gb-ram-xiaomi-turkiye-garantili-p-794300467"),
                ("Teknosa",     "https://www.teknosa.com/xiaomi-redmi-note-13-pro-8-gb-256-gb-siyah-cep-telefonu-xiaomi-turkiye-garantili-p-780010574"),
                ("n11",         "https://www.n11.com/urun/xiaomi-redmi-note-13-pro-8-gb-256-gb-xiaomi-turkiye-garantili-48308143"),
            }),
            ("Elektronik", "Samsung Galaxy A36 5G", new[]
            {
                // Çiçeksepeti (kcm75582696) ve PttAVM (p-1216895525) listelemeleri
                // stoktan kalktı → çıkarıldı (2026-05-15).
                ("Hepsiburada", "https://www.hepsiburada.com/samsung-galaxy-a36-5g-128-gb-8-gb-ram-samsung-turkiye-garantili-antrasit-p-HBCV000088BW16"),
                ("Teknosa",     "https://www.teknosa.com/samsung-galaxy-a36-5g-8256gb-siyah-akilli-telefon-p-125070176"),
                ("n11",         "https://www.n11.com/urun/samsung-galaxy-a36-5g-128-gb-samsung-turkiye-garantili-74948031"),
            }),
            ("Elektronik", "Samsung Galaxy S25", new[]
            {
                // Çiçeksepeti'nde standart S25 yok (sadece Plus/Ultra var) → atlandı.
                ("Hepsiburada", "https://www.hepsiburada.com/samsung-galaxy-s25-128-gb-12-gb-ram-samsung-turkiye-garantili-lacivert-p-HBCV00007JDVRH"),
                ("PttAVM",      "https://www.pttavm.com/samsung-galaxy-s25-12-256gb-akilli-telefon-mint-yesili-p-860327137"),
                ("Teknosa",     "https://www.teknosa.com/samsung-galaxy-s25-5g-256gb-lacivert-akilli-telefon-p-125079837"),
                ("n11",         "https://www.n11.com/urun/samsung-galaxy-s25-12-gb-256-gb-samsung-turkiye-garantili-66614871"),
            }),
            ("Elektronik", "iPhone 16 128GB", new[]
            {
                // Çiçeksepeti'nde iPhone 16 yok (sadece eski iPhone'lar) → atlandı.
                // PttAVM listelemesi (p-1238107279) stoktan kalktı → çıkarıldı (2026-05-15).
                ("Hepsiburada", "https://www.hepsiburada.com/apple-iphone-16-128gb-siyah-p-HBCV00006Y4HFJ"),
                ("Teknosa",     "https://www.teknosa.com/apple-iphone-16-128gb-siyah-akilli-telefon-p-125079709"),
                ("n11",         "https://www.n11.com/urun/apple-iphone-16-128-gb-apple-turkiye-garantili-59257801"),
            }),
            ("Elektronik", "Xiaomi Redmi 13 256GB", new[]
            {
                // Çiçeksepeti'nde Redmi 13 telefon listelemesi bulunamadı (sadece aksesuar) → atlandı.
                ("Hepsiburada", "https://www.hepsiburada.com/redmi-13-256-gb-8-gb-ram-xiaomi-turkiye-garantili-siyah-p-HBCV00006HSGP8"),
                ("PttAVM",      "https://www.pttavm.com/xiaomi-redmi-13-8-256-gb-sifir-p-1098809564"),
                ("Teknosa",     "https://www.teknosa.com/xiaomi-redmi-13-8gb256gb-midnight-black-akilli-telefon-p-125079616"),
                ("n11",         "https://www.n11.com/urun/xiaomi-redmi-13-8-gb-256-gb-xiaomi-turkiye-garantili-54862857"),
            }),

            // ── Ev & Yaşam ─────────────────────────────────────────────────
            // NOT: Bu kategoride Teknosa kullanılmıyor (sadece elektronik satıyor).
            // Hepsiburada URL'leri çoğunlukla "pm-" prefixli (model kartı) — scraper
            // genelde p- yapıyı bekliyor. Test sırasında bu modellerin Hepsiburada
            // listelemeleri fiyat döndüremezse stok-yok handling devreye girer.
            ("Ev & Yaşam", "Philips Airfryer XL HD9270", new[]
            {
                // Çiçeksepeti'nde HD9270 tam karşılığı yok (HD9870/HD9255 var) → atlandı.
                ("Hepsiburada", "https://www.hepsiburada.com/philips-hd9270-90-airfryer-xl-essential-sicak-hava-fritozu-2000-w-1200-g-kapasite-dijital-ekran-ithalatci-garantili-p-HBCV0000373VJY"),
                ("PttAVM",      "https://www.pttavm.com/philips-airfryer-xl-hd9270-96-essential-62-lt-yagsiz-fritoumlz-p-332691548"),
                ("n11",         "https://www.n11.com/urun/philips-hd927090-airfryer-xl-essential-2000-w-sicak-hava-fritozu-27954665"),
            }),
            ("Ev & Yaşam", "Tefal Secure 5 Neo V2 6L Düdüklü Tencere", new[]
            {
                ("Hepsiburada", "https://www.hepsiburada.com/tefal-p2530739-secure-5-neo-v2-6-l-duduklu-tencere-7114000484-pm-mt7114000484"),
                ("Çiçeksepeti", "https://www.ciceksepeti.com/tefal-secure-5-neo-v2-6-lt-duduklu-tencere-kc8027299"),
                ("PttAVM",      "https://www.pttavm.com/tefal-secure-5-neo-v2-6-lt-duduklu-tencere-p-917417971"),
                ("n11",         "https://www.n11.com/urun/tefal-secure-5-neo-v2-6-l-duduklu-tencere-4772450"),
            }),
            ("Ev & Yaşam", "Roborock Q5 Pro Robot Süpürge", new[]
            {
                ("Hepsiburada", "https://www.hepsiburada.com/roborock-q5-pro-siyah-robot-supurge-pm-HBC000055QCUK"),
                ("Çiçeksepeti", "https://www.ciceksepeti.com/roborock-q5-pro-akilli-robot-supurge-siyah-roborock-turkiye-garantili-kcm94792482"),
                ("PttAVM",      "https://www.pttavm.com/roborock-q5-pro-siyah-robot-supurge-p-1226218285"),
                ("n11",         "https://www.n11.com/urun/roborock-q5-pro-robot-supurge-siyah-44751640"),
            }),
            ("Ev & Yaşam", "Karaca Hatır Mod Türk Kahve Makinesi", new[]
            {
                // PttAVM'de "Hatır Mod" yok (Köz, Hüps, Barista var) → atlandı.
                ("Hepsiburada", "https://www.hepsiburada.com/karaca-hatir-mod-sutlu-turk-kahve-makinesi-red-pm-HB00000ZKOY7"),
                ("Çiçeksepeti", "https://www.ciceksepeti.com/karaca-hatir-mod-sutlu-turk-kahve-makinesi-rosegold-kc6080998"),
                ("n11",         "https://www.n11.com/urun/karaca-hatir-moduna-gore-sutlu-turk-kahve-makinesi-1585295"),
            }),
            ("Ev & Yaşam", "Arzum AR3061 Çaycı Çay Makinesi", new[]
            {
                ("Hepsiburada", "https://www.hepsiburada.com/arzum-cam-demlik-cay-makinesi-1700-watt-siyah-ar3061-pm-HB00000E7BD3"),
                ("Çiçeksepeti", "https://www.ciceksepeti.com/arzum-ar3061-cayci-1700-w-cam-demlikli-cay-makinesi-kcm72227191"),
                ("PttAVM",      "https://www.pttavm.com/arzum-ar3061-cayci-cay-makinesi-orijinal-cam-demlik-p-122936227"),
                ("n11",         "https://www.n11.com/urun/arzum-ar3061-cayci-18-l-cay-makinesi-780794"),
            }),
            // Atlanan modeller (yeterli kapsam yok, 2026-05-13):
            //   • Samsung Galaxy A57 — sadece 2 sitede
            //   • Xiaomi Redmi 14    — pazarda yok (Note 14 / 14C var)
            //   • Xiaomi Redmi 15    — sadece Teknosa'da net
            //   • Karaca Mokka       — model yok, "Hatır Mod" ile değiştirildi
            //   • Arzum Çaykolik     — Korkmaz markası, "AR3061 Çaycı" ile değiştirildi
        };

        // Tüm seed URL'lerini set'e topla — sonradan "bu sette olmayan sistem
        // ürünlerini pasifleştir" için kullanılacak.
        var seedUrlSet = katalog
            .SelectMany(k => k.Linkler.Select(l => l.URL))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        int eklenenModel = 0, eklenenListe = 0, atlananModel = 0, atlananListe = 0;

        foreach (var (kategoriAdi, modelAdi, linkler) in katalog)
        {
            // Kategori sözlükte var mı?
            if (!kategoriler.TryGetValue(kategoriAdi, out var kategoriId))
            {
                _logger.LogWarning("[Seed] '{Kategori}' kategorisi DB'de yok, '{Model}' modeli atlandı.",
                    kategoriAdi, modelAdi);
                continue;
            }

            // Aynı isimle model varsa → modeli yeniden ekleme, ama listelerini gözden geçir.
            var model = await _context.UrunModelleri.FirstOrDefaultAsync(m => m.Ad == modelAdi);
            if (model is null)
            {
                model = new UrunModeli
                {
                    Ad = modelAdi,
                    KategoriId = kategoriId,
                    OlusturulmaTarihi = DateTime.UtcNow,
                    Aktif = true,
                };
                _context.UrunModelleri.Add(model);
                await _context.SaveChangesAsync();
                eklenenModel++;
            }
            else
            {
                atlananModel++;
            }

            foreach (var (satici, url) in linkler)
            {
                // Aynı URL zaten kayıtlıysa atla (idempotent — seed tekrar çalışsa da çift kayıt oluşmaz)
                if (await _context.Urunler.AnyAsync(u => u.URL == url))
                {
                    atlananListe++;
                    continue;
                }

                var liste = new Urun
                {
                    Ad = modelAdi,
                    URL = url,
                    Satici = satici,
                    ParaBirimi = "TRY",
                    KategoriId = kategoriId,
                    UrunModeliId = model.Id,
                    UserId = null,                       // Sistem ürünü
                    BaslangicFiyati = 0,
                    SonFiyati = 0,
                    EklendigiTarih = DateTime.UtcNow,
                    SonGuncellemeTarihi = DateTime.MinValue,  // İlk scrape'i tetikle
                    Aktif = true,
                };
                _context.Urunler.Add(liste);
                eklenenListe++;
            }
            await _context.SaveChangesAsync();
        }

        // ── Seed listesinden ÇIKARILMIŞ sistem URL'lerini pasifleştir ──
        // Kod sahibi (Veri Avcısı) bir URL'yi seed listesinden silerse, DB'de zaten
        // var olan kaydı da pasifleştirmek lazım — yoksa Hangfire job'ı çalışmaya
        // devam eder ve "kategoriler" sayfasında ölü listingler gözükür.
        // Sadece SİSTEM ürünleri (UrunModeliId != null) etkilenir; kullanıcının
        // elle eklediği URL'lere (UrunModeliId == null) DOKUNULMAZ.
        var sistemUrunleri = await _context.Urunler
            .Where(u => u.UrunModeliId != null && u.Aktif)
            .ToListAsync();

        int pasiflestirilen = 0;
        foreach (var u in sistemUrunleri)
        {
            if (!seedUrlSet.Contains(u.URL))
            {
                u.Aktif = false;
                pasiflestirilen++;
            }
        }
        if (pasiflestirilen > 0)
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation(
                "[Seed] Seed listesinden çıkarılmış {Sayi} sistem URL'si pasifleştirildi.",
                pasiflestirilen);
        }

        _logger.LogInformation(
            "[Seed] Katalog seed tamamlandı — Eklenen: {EkM} model, {EkL} listeleme | Atlanan: {AtM} model, {AtL} listeleme | Pasifleştirilen: {Pasif}",
            eklenenModel, eklenenListe, atlananModel, atlananListe, pasiflestirilen);
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
