using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using HtmlAgilityPack;

namespace FiyatTakipWebSitesi.Services;

public class ScraperService
{
    public Task<string> GetPriceAsync(string url)
    {
        return Task.Run(() =>
        {
            var logPath = @"C:\Users\furko\OneDrive\Masaüstü\DebugLog.txt";
            var options = new ChromeOptions();
            options.AddArgument("--headless=new");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--window-size=1920,1080");
            options.AddArgument("--disable-blink-features=AutomationControlled");
            options.AddExcludedArgument("enable-automation");
            options.AddArgument("user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");

            using var driver = new ChromeDriver(options);
            driver.Navigate().GoToUrl(url);
            Thread.Sleep(5000);

            var selectors = new[]
            {
                "[data-test-id='price-current-price']",
                "[data-test-id='final-price']",
                "span[class*='currentPriceContainer']",
                "span[class*='price-value']",
                "div[data-bind*='currentPrice'] span",
                "span[itemprop='price']",
                ".product-price span"
            };

            var log = new System.Text.StringBuilder();
            System.IO.File.WriteAllText(logPath, "Henüz başlamadı");
            // Tek FindElement yerine FindElements kullan
            var elements = driver.FindElements(By.CssSelector("[data-test-id='price-current-price']"));

            if (elements.Count > 0)
            {
                var fiyatlar = new List<decimal>();

                foreach (var el in elements)
                {
                    var text = el.Text?.Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        // "25.649,05 TL" → "25649.05" formatına çevir
                        var temiz = text.Replace("TL", "").Replace(".", "").Replace(",", ".").Trim();
                        if (decimal.TryParse(temiz, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out decimal fiyat))
                        {
                            fiyatlar.Add(fiyat);
                        }
                    }
                }

                if (fiyatlar.Count > 0)
                {
                    var enDusuk = fiyatlar.Min();
                    // ÖNCE log yaz
                    System.IO.File.WriteAllText(logPath,
                        $"Bulunan element sayısı: {elements.Count}\n" +
                        $"Tüm fiyatlar: {string.Join(", ", fiyatlar)}\n" +
                        $"Seçilen (en düşük): {enDusuk}\n");
                    return enDusuk.ToString("N2", new System.Globalization.CultureInfo("tr-TR")) + " TL";
                }
            }

            System.IO.File.WriteAllText(logPath, $"Bulunan element sayısı: {elements.Count}\n");
            System.IO.File.WriteAllText(logPath, "Henüz başlamadı");
            return "Fiyat bulunamadı";
        });
    }

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
}

public class UrunKart
{
    public string Ad { get; set; } = "";
    public string Fiyat { get; set; } = "";
    public string ResimUrl { get; set; } = "";
    public string UrunUrl { get; set; } = "";
}