using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace FiyatTakipWebSitesi.Services;

public class ScraperService
{
    public Task<string> GetPriceAsync(string url)
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
            options.AddArgument("user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            using var driver = new ChromeDriver(options);
            driver.Navigate().GoToUrl(url);
            Thread.Sleep(5000);

            var source = driver.PageSource;

            // Tüm "price":"SAYI" ve "price":SAYI formatlarını bul
            var matches = System.Text.RegularExpressions.Regex
                .Matches(source, @"""price""\s*:\s*""?(\d+)""?");

            // En büyük sayıyı al (genellikle gerçek fiyat en büyük olandır)
            long bestPrice = 0;
            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                if (long.TryParse(m.Groups[1].Value, out long val))
                {
                    // Çok küçük (ID olabilir) veya çok büyük değerleri atla
                    if (val > 10 && val < 10_000_000 && val > bestPrice)
                        bestPrice = val;
                }
            }

            if (bestPrice > 0)
            {
                return bestPrice.ToString("N0",
                    new System.Globalization.CultureInfo("tr-TR")) + " TL";
            }

            return "Fiyat bulunamadı";
        });
    }
}