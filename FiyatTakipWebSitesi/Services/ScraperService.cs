// =====================================================
// ScraperService.cs
// Bu servis, verilen bir ürün URL'sine giderek sayfa
// kaynağından ürünün fiyat bilgisini otomatik olarak
// çeken bir web scraper'dır. Selenium ve Chrome tarayıcısını
// arka planda (headless) çalıştırarak JavaScript ile
// yüklenen dinamik sayfalardaki fiyat verisine ulaşır.
// =====================================================

using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using HtmlAgilityPack;

namespace FiyatTakipWebSitesi.Services;

public class ScraperService
{
    // Verilen URL'den fiyatı asenkron olarak çeker
    public Task<string> GetPriceAsync(string url)
    {
        // Selenium işlemleri senkron çalıştığı için Task.Run ile arka plana alıyoruz
        return Task.Run(() =>
        {
            var options = new ChromeOptions();
            options.AddArgument("--headless=new");                              // Tarayıcıyı görünmez (arka planda) çalıştır
            options.AddArgument("--no-sandbox");                                // Linux/Docker ortamlarında gerekli güvenlik bypass'ı
            options.AddArgument("--disable-gpu");                               // Headless modda GPU kullanımını devre dışı bırak
            options.AddArgument("--window-size=1920,1080");                     // Sayfa düzeni için pencere boyutunu ayarla
            options.AddArgument("--disable-blink-features=AutomationControlled"); // Selenium tespitini engelle
            options.AddExcludedArgument("enable-automation");                   // "Chrome otomasyonla kontrol ediliyor" bildirimini gizle
            options.AddArgument("user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"); // Bot değil normal kullanıcı gibi görün

            using var driver = new ChromeDriver(options);
            driver.Navigate().GoToUrl(url);   // Verilen URL'e git
            Thread.Sleep(5000);               // Sayfanın JavaScript ile tam yüklenmesini bekle

            // Yüklenen sayfanın tüm HTML kaynak kodunu al
            var source = driver.PageSource;

            // Sayfa kaynağında "price":"SAYI" veya "price":SAYI formatındaki
            // tüm fiyat verilerini Regex ile bul
            var matches = System.Text.RegularExpressions.Regex
                .Matches(source, @"""price""\s*:\s*""?(\d+)""?");

            // Eşleşen değerler arasından en büyüğünü bul
            // (Gerçek ürün fiyatı genellikle en büyük anlamlı sayıdır)
            long bestPrice = 0;
            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                if (long.TryParse(m.Groups[1].Value, out long val))
                {
                    // 10'dan küçükler (ID olabilir) ve 10 milyondan büyükler geçersiz sayılır
                    if (val > 10 && val < 10_000_000 && val > bestPrice)
                        bestPrice = val;
                }
            }

            // Geçerli bir fiyat bulunduysa Türkçe formatında döndür (örn: 1.299 TL)
            if (bestPrice > 0)
            {
                return bestPrice.ToString("N0",
                    new System.Globalization.CultureInfo("tr-TR")) + " TL";
            }

            // Hiçbir geçerli fiyat bulunamazsa
            return "Fiyat bulunamadı";
        });
    }

    // Hepsiburada kategori sayfasından ürün listesi çeker
    public Task<List<UrunKart>> GetKategoriUrunleriAsync(string kategoriUrl)
    {
        return Task.Run(() =>
        {
            var options = new ChromeOptions();
            options.AddArgument("--headless=new");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--window-size=1920,1080");
            options.AddArgument("--disable-blink-features=AutomationControlled");
            options.AddExcludedArgument("enable-automation");
            options.AddArgument("user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            using var driver = new ChromeDriver(options);
            driver.Navigate().GoToUrl(kategoriUrl);
            Thread.Sleep(4000);

            var source = driver.PageSource;
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(source);

            var urunler = new List<UrunKart>();

            // Hepsiburada ürün kartları
            var kartlar = doc.DocumentNode.SelectNodes("//li[contains(@class,'productListContent-')]");

            if (kartlar == null)
            {
                // Alternatif selector
                kartlar = doc.DocumentNode.SelectNodes("//div[contains(@data-test-id,'product-card')]");
            }

            if (kartlar != null)
            {
                foreach (var kart in kartlar.Take(20))
                {
                    var adNode = kart.SelectSingleNode(".//*[contains(@class,'product-title') or contains(@data-test-id,'product-card-name') or contains(@class,'productName')]");
                    var fiyatNode = kart.SelectSingleNode(".//*[contains(@class,'price-value') or contains(@data-test-id,'product-card-price') or contains(@class,'currentPrice')]");
                    var resimNode = kart.SelectSingleNode(".//img");
                    var linkNode = kart.SelectSingleNode(".//a[@href]");

                    var ad = adNode?.InnerText?.Trim();
                    var fiyat = fiyatNode?.InnerText?.Trim();
                    var resim = resimNode?.GetAttributeValue("src", "") ?? resimNode?.GetAttributeValue("data-src", "");
                    var link = linkNode?.GetAttributeValue("href", "");

                    if (!string.IsNullOrWhiteSpace(ad) && !string.IsNullOrWhiteSpace(fiyat))
                    {
                        if (link != null && !link.StartsWith("http"))
                            link = "https://www.hepsiburada.com" + link;

                        urunler.Add(new UrunKart
                        {
                            Ad = ad,
                            Fiyat = fiyat,
                            ResimUrl = resim ?? "",
                            UrunUrl = link ?? ""
                        });
                    }
                }
            }

            return urunler;
        });
    }
}

public class UrunKart
{
    public string Ad { get; set; } = "";
    public string Fiyat { get; set; } = "";
    public string ResimUrl { get; set; } = "";
    public string UrunUrl { get; set; } = "";
}