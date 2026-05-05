using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using HtmlAgilityPack;

namespace FiyatTakipWebSitesi.Services;

public class ScraperService
{
    private static ChromeOptions ChromeAyarlari()
    {
        var options = new ChromeOptions();
        options.AddArgument("--headless=new");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-gpu");
        options.AddArgument("--window-size=1920,1080");
        options.AddArgument("--disable-blink-features=AutomationControlled");
        options.AddExcludedArgument("enable-automation");
        options.AddArgument("user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        return options;
    }

    // ─────────────────────────────────────────────────────────
    // Ana fiyatı çeken yardımcı metot
    // ─────────────────────────────────────────────────────────
    private static string FiyatCek(IWebDriver driver)
    {
        // 1. DENEME: data-test-id="default-price"
        var defaultPrice = driver.FindElements(By.CssSelector("[data-test-id='default-price']"));
        if (defaultPrice.Count > 0)
        {
            // "Kazancımı gör" kutusunu DOM'dan kaldır — fiyat metnine karışmasın
            var jsExecutor = (IJavaScriptExecutor)driver;
            jsExecutor.ExecuteScript(@"
                var el = document.querySelector('[data-test-id=""see-earnings""]');
                if (el) el.remove();
            ");

            // Temizlendikten sonra tekrar al
            defaultPrice = driver.FindElements(By.CssSelector("[data-test-id='default-price']"));
            if (defaultPrice.Count > 0)
            {
                var text = defaultPrice[0].Text?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                    return text.Replace("\n", " ").Trim();
            }
        }

        // 2. DENEME: price-current-price — fallback
        var elements = driver.FindElements(By.CssSelector("[data-test-id='price-current-price']"));
        if (elements.Count > 0)
        {
            var fiyatlar = new List<decimal>();
            foreach (var el in elements)
            {
                var t = el.Text?.Trim();
                if (string.IsNullOrWhiteSpace(t)) continue;
                var temiz = t.Replace("TL", "").Replace(".", "").Replace(",", ".").Trim();
                if (decimal.TryParse(temiz, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out decimal f))
                    fiyatlar.Add(f);
            }
            if (fiyatlar.Count > 0)
                return fiyatlar.Min().ToString("N2", new System.Globalization.CultureInfo("tr-TR")) + " TL";
        }

        return "Fiyat bulunamadı";
    }

    // ─────────────────────────────────────────────────────────
    // 1) Sadece fiyat çeker — FiyatSorgula.razor tarafından kullanılıyor
    // ─────────────────────────────────────────────────────────
    public Task<string> GetPriceAsync(string url)
    {
        return Task.Run(() =>
        {
            var logPath = @"C:\Users\furko\OneDrive\Masaüstü\DebugLog.txt";

            using var driver = new ChromeDriver(ChromeAyarlari());
            driver.Navigate().GoToUrl(url);
            Thread.Sleep(5000);

            var fiyat = FiyatCek(driver);
            System.IO.File.WriteAllText(logPath, $"URL: {url}\nFiyat: {fiyat}\n");
            return fiyat;
        });
    }

    // ─────────────────────────────────────────────────────────
    // 2) Kategori sayfasından ürün listesi çeker — KategoriUrunler.razor
    // ─────────────────────────────────────────────────────────
    public Task<List<UrunKart>> GetKategoriUrunleriAsync(string kategoriUrl)
    {
        return Task.Run(() =>
        {
            using var driver = new ChromeDriver(ChromeAyarlari());
            driver.Navigate().GoToUrl(kategoriUrl);
            Thread.Sleep(4000);

            string source;
            try { source = driver.PageSource; }
            catch { Thread.Sleep(2000); source = driver.PageSource; }

            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(source);

            var urunler = new List<UrunKart>();

            var kartlar = doc.DocumentNode.SelectNodes("//li[contains(@class,'productListContent-')]");
            if (kartlar == null)
                kartlar = doc.DocumentNode.SelectNodes("//div[contains(@data-test-id,'product-card')]");

            if (kartlar != null)
            {
                foreach (var kart in kartlar.Take(20))
                {
                    var adNode = kart.SelectSingleNode(".//*[starts-with(@data-test-id,'title-')]");
                    var fiyatNode = kart.SelectSingleNode(".//*[starts-with(@data-test-id,'final-price-')]");
                    var resimNode = kart.SelectSingleNode(".//img");
                    var linkNode = kart.SelectSingleNode(".//a[@href]");

                    var ad = adNode?.InnerText?.Trim() ?? linkNode?.GetAttributeValue("title", "")?.Trim();
                    var fiyat = fiyatNode?.InnerText?.Trim();
                    var resim = resimNode?.GetAttributeValue("src", "");
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

    // ─────────────────────────────────────────────────────────
    // 3) Tek ürün URL'sinden fiyat + ad + resim + satıcı çeker
    // ─────────────────────────────────────────────────────────
    public Task<UrunDetay> GetUrunDetayAsync(string url)
    {
        return Task.Run(() =>
        {
            using var driver = new ChromeDriver(ChromeAyarlari());
            driver.Navigate().GoToUrl(url);
            Thread.Sleep(5000);

            var detay = new UrunDetay { Url = url };

            // ── Fiyat ──────────────────────────────────────────────
            detay.Fiyat = FiyatCek(driver);

            // ── Ürün Adı ───────────────────────────────────────────
            var adElemanlari = driver.FindElements(By.CssSelector("[data-test-id='title'] h1"));
            if (adElemanlari.Count == 0)
                adElemanlari = driver.FindElements(By.CssSelector("[data-test-id='title']"));
            if (adElemanlari.Count > 0)
            {
                var ad = adElemanlari[0].Text?.Trim();
                if (!string.IsNullOrWhiteSpace(ad))
                    detay.Ad = ad;
            }

            // ── Ürün Resmi ─────────────────────────────────────────
            var resimElemanlari = driver.FindElements(By.CssSelector("picture img"));
            foreach (var img in resimElemanlari)
            {
                var src = img.GetAttribute("src")?.Trim();
                if (!string.IsNullOrWhiteSpace(src) && src.StartsWith("https://productimages.hepsiburada"))
                {
                    detay.ResimUrl = src;
                    break;
                }
            }

            // Bulamazsa srcset'ten dene
            if (string.IsNullOrEmpty(detay.ResimUrl))
            {
                var sourceElemanlari = driver.FindElements(By.CssSelector("picture source"));
                foreach (var source in sourceElemanlari)
                {
                    var srcset = source.GetAttribute("srcset")?.Trim();
                    if (!string.IsNullOrWhiteSpace(srcset) && srcset.StartsWith("https://productimages.hepsiburada"))
                    {
                        detay.ResimUrl = srcset.Split(' ')[0];
                        break;
                    }
                }
            }

            // ── Satıcı Adı ─────────────────────────────────────────
            var saticiElemanlari = driver.FindElements(By.CssSelector("[data-test-id='buyBox-seller'] a"));
            if (saticiElemanlari.Count > 0)
            {
                var satici = saticiElemanlari[0].GetAttribute("title")?.Trim();
                if (string.IsNullOrWhiteSpace(satici))
                    satici = saticiElemanlari[0].Text?.Trim();
                if (!string.IsNullOrWhiteSpace(satici))
                    detay.Satici = satici;
            }

            return detay;
        });
    }
}

// ─────────────────────────────────────────────────────────────
// Veri modelleri
// ─────────────────────────────────────────────────────────────

public class UrunKart
{
    public string Ad { get; set; } = "";
    public string Fiyat { get; set; } = "";
    public string ResimUrl { get; set; } = "";
    public string UrunUrl { get; set; } = "";
}

public class UrunDetay
{
    public string Url { get; set; } = "";
    public string Ad { get; set; } = "İsimsiz Ürün";
    public string Fiyat { get; set; } = "";
    public string ResimUrl { get; set; } = "";
    public string Satici { get; set; } = "Bilinmiyor";
}
