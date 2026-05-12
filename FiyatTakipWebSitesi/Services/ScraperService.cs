using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using HtmlAgilityPack;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FiyatTakipWebSitesi.Services;

public class ScraperService
{
    private readonly ILogger<ScraperService> _logger;
    private static readonly SemaphoreSlim _slot = new(2, 2);
    private static readonly System.Globalization.CultureInfo _tr = new("tr-TR");
    private static readonly System.Globalization.CultureInfo _inv = System.Globalization.CultureInfo.InvariantCulture;

    public ScraperService(ILogger<ScraperService> logger) => _logger = logger;

    private static ChromeOptions ChromeAyarlari()
    {
        var options = new ChromeOptions();
        options.AddArgument("--headless=new");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-gpu");
        options.AddArgument("--window-size=1920,1080");
        options.AddArgument("--disable-blink-features=AutomationControlled");
        options.AddArgument("--disable-dev-shm-usage");
        options.AddArgument("--disable-extensions");
        options.AddExcludedArgument("enable-automation");
        options.AddAdditionalOption("useAutomationExtension", false);
        options.AddArgument("user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                            "AppleWebKit/537.36 (KHTML, like Gecko) " +
                            "Chrome/124.0.0.0 Safari/537.36");
        return options;
    }

    private static ChromeDriver DriverOlustur()
    {
        var service = ChromeDriverService.CreateDefaultService();
        service.SuppressInitialDiagnosticInformation = true;
        service.HideCommandPromptWindow = true;
        return new ChromeDriver(service, ChromeAyarlari());
    }

    // ─────────────────────────────────────────────────────────
    // Akıllı bekleme — sabit 5sn yerine, hedef selectorlar gelene kadar bekle
    // En geç maxSaniye sonra döner (timeout olsa bile patlamaz)
    // ─────────────────────────────────────────────────────────
    private static async Task SelectorBekleAsync(IWebDriver driver, By[] selectors, int maxSaniye = 15)
    {
        var bitis = DateTime.UtcNow.AddSeconds(maxSaniye);
        while (DateTime.UtcNow < bitis)
        {
            try
            {
                foreach (var sel in selectors)
                    if (driver.FindElements(sel).Count > 0) return;
            }
            catch { }
            await Task.Delay(300);
        }
    }

    private async Task<T> RetryAsync<T>(Func<Task<T>> islem, int maxDeneme = 3)
    {
        Exception? sonHata = null;
        for (int i = 1; i <= maxDeneme; i++)
        {
            try { return await islem(); }
            catch (Exception ex)
            {
                sonHata = ex;
                _logger.LogWarning("[ScraperService] Deneme {i}/{max}: {Msg}", i, maxDeneme, ex.Message);
                if (i < maxDeneme)
                    await Task.Delay(TimeSpan.FromSeconds(i * 2));
            }
        }
        throw sonHata!;
    }

    // ─────────────────────────────────────────────────────────
    // Desteklenen site tespiti
    // ─────────────────────────────────────────────────────────
    public enum Site { Bilinmiyor, Hepsiburada, Ciceksepeti, Teknosa, N11, Pttavm }

    public static Site SiteTespit(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return Site.Bilinmiyor;
        var host = uri.Host.ToLowerInvariant();
        if (host.Contains("hepsiburada.com")) return Site.Hepsiburada;
        if (host.Contains("ciceksepeti.com")) return Site.Ciceksepeti;
        if (host.Contains("teknosa.com")) return Site.Teknosa;
        if (host.Contains("n11.com")) return Site.N11;
        if (host.Contains("pttavm.com")) return Site.Pttavm;
        return Site.Bilinmiyor;
    }

    public static string SiteAdi(Site site) => site switch
    {
        Site.Hepsiburada => "Hepsiburada",
        Site.Ciceksepeti => "Çiçeksepeti",
        Site.Teknosa => "Teknosa",
        Site.N11 => "n11",
        Site.Pttavm => "PttAVM",
        _ => "Bilinmiyor"
    };

    // Multi-seller (?magaza= parametresi olan) siteler — buy-box mantığı
    private static bool MultiSellerSite(Site site) =>
        site == Site.Hepsiburada || site == Site.N11;

    // ─────────────────────────────────────────────────────────
    // URL'in geçerli bir ürün linki olup olmadığını kontrol et
    // Geçerliyse null, değilse kullanıcıya gösterilecek hata mesajı döndürür
    // Frontend (Takip.razor) bunu scrape'ten ÖNCE çağırır → exception fırlamaz
    // ─────────────────────────────────────────────────────────
    public static string? UrunUrlHatasi(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "URL boş olamaz.";

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return "Geçersiz URL formatı.";

        var site = SiteTespit(url);
        if (site == Site.Bilinmiyor)
            return "Sadece Hepsiburada, Çiçeksepeti, Teknosa, n11 ve PttAVM linkleri destekleniyor.";

        if (site == Site.Hepsiburada)
        {
            // Hepsiburada ürün URL kalıpları:
            //   -p-HBVxxxx, -pm-HBCxxxx, -p-SPORDELTA1903-S vb.
            if (!Regex.IsMatch(uri.AbsolutePath, @"-p[a-z]*-[A-Za-z0-9]", RegexOptions.IgnoreCase))
                return "Bu link bir ürün sayfası değil (kategori veya başka bir sayfa olabilir). " +
                       "Lütfen tekil bir ürünün detay sayfasının linkini yapıştırın.";
        }
        else if (site == Site.Ciceksepeti)
        {
            // Çiçeksepeti ürün URL kalıbı: /<slug>-<harfler><sayılar>
            if (!Regex.IsMatch(uri.AbsolutePath, @"-[a-z]+\d+/?$", RegexOptions.IgnoreCase))
                return "Bu link bir ürün sayfası değil (kategori veya başka bir sayfa olabilir). " +
                       "Lütfen tekil bir ürünün detay sayfasının linkini yapıştırın.";
        }
        else if (site == Site.Teknosa)
        {
            // Teknosa ürün URL kalıbı: /<slug>-p-<sayılar>
            // Örn: /huawei-freebuds-se-2-beyaz-bluetooth-kulaklik-p-780292169
            if (!Regex.IsMatch(uri.AbsolutePath, @"-p-\d+/?$", RegexOptions.IgnoreCase))
                return "Bu link bir ürün sayfası değil (kategori veya başka bir sayfa olabilir). " +
                       "Lütfen tekil bir ürünün detay sayfasının linkini yapıştırın.";
        }
        else if (site == Site.N11)
        {
            // n11 ürün URL kalıbı: /urun/<slug>-<sayılar>
            // Örn: /urun/bioderma-photoderm-spf-50-...-34727879
            if (!Regex.IsMatch(uri.AbsolutePath, @"^/urun/.+-\d+/?$", RegexOptions.IgnoreCase))
                return "Bu link bir ürün sayfası değil (kategori veya başka bir sayfa olabilir). " +
                       "Lütfen tekil bir ürünün detay sayfasının linkini yapıştırın.";
        }
        else if (site == Site.Pttavm)
        {
            // PttAVM ürün URL kalıbı: /<slug>-p-<sayılar>
            // Örn: /apple-airpods-4-nesil-apple-turkiye-garantili-p-1451724108
            if (!Regex.IsMatch(uri.AbsolutePath, @"-p-\d+/?$", RegexOptions.IgnoreCase))
                return "Bu link bir ürün sayfası değil (kategori veya başka bir sayfa olabilir). " +
                       "Lütfen tekil bir ürünün detay sayfasının linkini yapıştırın.";
        }

        return null;
    }

    // Defansif: bypass eden çağrılar için. Normal akışta tetiklenmez.
    private static void UrunUrliDogrula(string url)
    {
        var hata = UrunUrlHatasi(url);
        if (hata != null) throw new InvalidOperationException(hata);
    }

    // ─────────────────────────────────────────────────────────
    // URL'den ?magaza= parametresini al
    // ─────────────────────────────────────────────────────────
    private static string? UrlSaticiAl(string url)
    {
        try
        {
            var uri = new Uri(url);
            if (string.IsNullOrEmpty(uri.Query)) return null;
            foreach (var pair in uri.Query.TrimStart('?').Split('&'))
            {
                var kv = pair.Split('=', 2);
                if (kv.Length == 2 && kv[0].Equals("magaza", StringComparison.OrdinalIgnoreCase))
                    return Uri.UnescapeDataString(kv[1]);
            }
        }
        catch { }
        return null;
    }

    // ─────────────────────────────────────────────────────────
    // TEŞHİS — başarısız scrape'ten sonra ne döndüğünü logla
    // ─────────────────────────────────────────────────────────
    private void TeshisLogla(string url, IWebDriver driver, string pageSource)
    {
        try
        {
            var baslik = driver.Title ?? "(başlık yok)";
            var uzunluk = pageSource?.Length ?? 0;
            var alt = (pageSource ?? "").ToLowerInvariant();

            var supheli = new List<string>();
            if (alt.Contains("captcha")) supheli.Add("captcha");
            if (alt.Contains("robot")) supheli.Add("robot");
            if (alt.Contains("doğrulama") || alt.Contains("dogrulama")) supheli.Add("doğrulama");
            if (alt.Contains("access denied") || alt.Contains("erişim engellendi")) supheli.Add("access-denied");
            if (alt.Contains("forbidden")) supheli.Add("forbidden");
            if (alt.Contains("blocked")) supheli.Add("blocked");

            var jsonLdVar = (pageSource ?? "").Contains("application/ld+json");
            var itemPropVar = (pageSource ?? "").Contains("itemprop=\"price\"");
            var sepetOzelVar = alt.Contains("sepete özel fiyat");

            _logger.LogWarning(
                "[TEŞHİS] BAŞARISIZ SCRAPE\n" +
                "  URL          : {Url}\n" +
                "  Sayfa başlığı: {Baslik}\n" +
                "  HTML uzunluğu: {Uzunluk} karakter\n" +
                "  JSON-LD var? : {JsonLd}\n" +
                "  itemprop var?: {ItemProp}\n" +
                "  Sepete özel? : {Sepet}\n" +
                "  Şüpheli      : {Supheli}",
                url, baslik, uzunluk, jsonLdVar, itemPropVar, sepetOzelVar,
                supheli.Count > 0 ? string.Join(", ", supheli) : "yok");

            var logDir = Path.Combine(Directory.GetCurrentDirectory(), "logs");
            if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
            
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var dosyaAdiBase = $"error_{timestamp}_{Guid.NewGuid().ToString().Substring(0,4)}";
            
            File.WriteAllText(Path.Combine(logDir, $"{dosyaAdiBase}.html"), pageSource);
            
            if (driver is ITakesScreenshot ts)
            {
                var screenshot = ts.GetScreenshot();
                screenshot.SaveAsFile(Path.Combine(logDir, $"{dosyaAdiBase}.png"));
            }
            
            _logger.LogWarning("[TEŞHİS] Ekran görüntüsü ve HTML '{Dir}' klasörüne kaydedildi. ({Base})", logDir, dosyaAdiBase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[TEŞHİS] Log üretilirken hata: {Msg}", ex.Message);
        }
    }

    // <img> elementinden lazy-load attribute'larını da hesaba katan src'i al
    // Çoğu site src'e placeholder koyup gerçek URL'i data-src'te tutar
    private static string? ResimSrcAl(IWebElement img)
    {
        try
        {
            // Sıra önemli: önce gerçek src'i veren attribute'ları dene
            var candidates = new[] { "src", "data-src", "data-original", "data-lazy", "data-srcset" };
            foreach (var attr in candidates)
            {
                var val = img.GetAttribute(attr)?.Trim();
                if (string.IsNullOrWhiteSpace(val)) continue;
                // srcset formatı: "url1 1x, url2 2x" → ilk URL'i al
                if (attr == "data-srcset" || val.Contains(','))
                    val = val.Split(',')[0].Trim().Split(' ')[0];
                if (val.StartsWith("http") || val.StartsWith("//"))
                    return val.StartsWith("//") ? "https:" + val : val;
            }
        }
        catch { }
        return null;
    }

    // n11'de Product JSON-LD YOK. Tüm veri inline JS objesinde:
    //   <script>window.model = { ..., "price":"890,01 TL", "priceFloat":890.01,
    //                             "images":[{"path":".../{0}/..."}], "seller":{"nickName":"..."} }
    // Bu regex parser sayfa kaynağından doğrudan çeker, Selenium DOM'a gerek yok
    // ?magaza=X URL'i ile sayfa zaten o satıcı için render edildiği için bu veriler magaza-specific
    private static UrunDetay? N11SayfaParse(string pageSource)
    {
        if (string.IsNullOrEmpty(pageSource)) return null;
        var detay = new UrunDetay();

        // Title — ilk eşleşme genelde ürün adı (HTML escape decode et)
        var titleMatch = Regex.Match(pageSource, @"""title"":""([^""\\]*(?:\\.[^""\\]*)*)""");
        if (titleMatch.Success)
        {
            var ad = System.Net.WebUtility.HtmlDecode(titleMatch.Groups[1].Value);
            // " Fiyatları ve Özellikleri" suffix'ini kaldır
            ad = Regex.Replace(ad, @"\s*Fiyatları ve Özellikleri\s*$", "",
                RegexOptions.IgnoreCase).Trim();
            if (!string.IsNullOrEmpty(ad)) detay.Ad = ad;
        }

        // Fiyat — "price":"890,01 TL"
        var priceMatch = Regex.Match(pageSource, @"""price"":""(\d{1,3}(?:\.\d{3})*(?:,\d{1,2})?\s*TL)""");
        if (priceMatch.Success) detay.Fiyat = priceMatch.Groups[1].Value;

        // priceFloat — sayısal değer ("priceFloat":890.01)
        var floatMatch = Regex.Match(pageSource, @"""priceFloat"":(\d+(?:\.\d+)?)");
        if (floatMatch.Success &&
            decimal.TryParse(floatMatch.Groups[1].Value,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var fs))
            detay.FiyatSayi = fs;

        // Resim — "images":[{"path":"https://n11scdn..../{0}/..."}]
        // {0} placeholder'ını "org" ile değiştir (orijinal kalite)
        var imgMatch = Regex.Match(pageSource,
            @"""images"":\[\s*\{\s*[^}]*?""path"":""(https://n11scdn[^""]+?IMG-[^""]+?)""");
        if (imgMatch.Success)
            detay.ResimUrl = imgMatch.Groups[1].Value.Replace("{0}", "org");

        // Satıcı — "nickName" alanı (ana satıcı bloğunda)
        // Not: "sellerNickName" (diğer mağazalar) ve "sellerNickname" (yorumlar) ile karışmıyor —
        // o alanlardaki büyük/küçük harf farkları nedeniyle bu regex sadece ana satıcıyı yakalar
        var sellerMatch = Regex.Match(pageSource, @"""nickName""\s*:\s*""([^""]+)""");
        if (sellerMatch.Success) detay.Satici = sellerMatch.Groups[1].Value;

        // Hiçbir şey bulunamadıysa null dön
        if (string.IsNullOrEmpty(detay.Fiyat) && string.IsNullOrEmpty(detay.Ad))
            return null;
        return detay;
    }

    // Sayfanın ana satıcısını (buy-box) DOM'dan al
    private static string? BuyBoxSatici(IWebDriver driver)
    {
        try
        {
            var el = driver.FindElements(By.CssSelector("[data-test-id='buyBox-seller'] a"));
            if (el.Count == 0) return null;
            var ad = el[0].GetAttribute("title")?.Trim() ?? el[0].Text?.Trim();
            return string.IsNullOrWhiteSpace(ad) ? null : ad;
        }
        catch { return null; }
    }

    // İki satıcı adını gevşek karşılaştır — boşluk/case/Türkçe karakter farklarını yok say
    // Örn: "Vizyon Mağaza" vs "vizyonmagaza" (URL slug) → eşleşir
    private static bool SaticiEslesir(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
        var na = SaticiNormalize(a);
        var nb = SaticiNormalize(b);
        return na.Contains(nb) || nb.Contains(na);
    }

    // URL slug'larıyla JSON-LD satıcı adlarını eşleştirmek için
    // Türkçe → ASCII dönüşümü + boşluk/tire silme
    private static string SaticiNormalize(string s)
    {
        return s.Replace(" ", "").Replace("-", "").Replace("_", "")
                .Replace("ğ", "g").Replace("Ğ", "g")
                .Replace("ü", "u").Replace("Ü", "u")
                .Replace("ş", "s").Replace("Ş", "s")
                .Replace("ı", "i").Replace("İ", "i")
                .Replace("ö", "o").Replace("Ö", "o")
                .Replace("ç", "c").Replace("Ç", "c")
                .ToLowerInvariant();
    }

    // ─────────────────────────────────────────────────────────
    // JSON-LD — birincil yöntem, en stabil
    // SEO zorunluluğu olduğu için Hepsiburada bunu nadiren değiştirir
    // hedefSatici: URL'deki ?magaza= değeri — varsa o satıcının fiyatı seçilir
    // ─────────────────────────────────────────────────────────
    private static UrunDetay? JsonLdParse(string pageSource, string? hedefSatici = null)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(pageSource);

        var scripts = doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");
        if (scripts == null) return null;

        foreach (var script in scripts)
        {
            var json = script.InnerText?.Trim();
            if (string.IsNullOrEmpty(json)) continue;

            try
            {
                using var jdoc = JsonDocument.Parse(json);

                // Aday Product node'larını bul (root, @graph içi, ya da root array)
                foreach (var product in ProductNodelariBul(jdoc.RootElement))
                {
                    var detay = ProductElementiParse(product, hedefSatici);
                    if (detay != null && !string.IsNullOrEmpty(detay.Fiyat))
                        return detay;
                }
            }
            catch { }
        }

        return null;
    }

    // JSON-LD root içinde @type=Product olan tüm node'ları topla
    private static IEnumerable<JsonElement> ProductNodelariBul(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
                foreach (var p in ProductNodelariBul(item)) yield return p;
            yield break;
        }

        if (root.ValueKind != JsonValueKind.Object) yield break;

        if (TipKontrol(root, "Product")) yield return root;

        if (root.TryGetProperty("@graph", out var graph))
            foreach (var p in ProductNodelariBul(graph)) yield return p;
    }

    // @type alanı string ya da array olabilir — verilen tipi içeriyor mu?
    private static bool TipKontrol(JsonElement node, string tip)
    {
        if (!node.TryGetProperty("@type", out var t)) return false;
        if (t.ValueKind == JsonValueKind.String) return t.GetString() == tip;
        if (t.ValueKind == JsonValueKind.Array)
            foreach (var x in t.EnumerateArray())
                if (x.ValueKind == JsonValueKind.String && x.GetString() == tip) return true;
        return false;
    }

    private static UrunDetay? ProductElementiParse(JsonElement product, string? hedefSatici)
    {
        var detay = new UrunDetay();

        if (product.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
            detay.Ad = nameProp.GetString() ?? "İsimsiz Ürün";

        if (product.TryGetProperty("image", out var imgProp))
        {
            if (imgProp.ValueKind == JsonValueKind.Array && imgProp.GetArrayLength() > 0)
                detay.ResimUrl = imgProp[0].GetString() ?? "";
            else if (imgProp.ValueKind == JsonValueKind.String)
                detay.ResimUrl = imgProp.GetString() ?? "";
            else if (imgProp.ValueKind == JsonValueKind.Object &&
                     imgProp.TryGetProperty("url", out var imgUrl))
                detay.ResimUrl = imgUrl.GetString() ?? "";
        }

        if (!product.TryGetProperty("offers", out var offersProp))
            return null;

        if (offersProp.ValueKind == JsonValueKind.Object)
        {
            // AggregateOffer ise içindeki offers array'ini parse et
            if (TipKontrol(offersProp, "AggregateOffer") &&
                offersProp.TryGetProperty("offers", out var nested) &&
                nested.ValueKind == JsonValueKind.Array)
            {
                ParseOffersArray(nested, detay, hedefSatici);
            }
            else
            {
                OfferParse(offersProp, detay);
            }
        }
        else if (offersProp.ValueKind == JsonValueKind.Array)
        {
            ParseOffersArray(offersProp, detay, hedefSatici);
        }

        return detay;
    }

    // En son JSON-LD parse'da bulunan satıcı/fiyat çiftleri (teşhis için)
    // Statik metodlardan çağrıldığı için instance logger yerine global bir teşhis state
    private static string? _sonJsonLdTeshis;

    private static void ParseOffersArray(JsonElement offers, UrunDetay detay, string? hedefSatici)
    {
        var teklifler = new List<UrunDetay>();
        foreach (var offer in offers.EnumerateArray())
        {
            var temp = new UrunDetay();
            OfferParse(offer, temp);
            if (temp.FiyatSayi > 0) teklifler.Add(temp);
        }

        // Teşhis: JSON-LD'deki satıcı/fiyat çiftlerini kaydet
        if (teklifler.Count > 0)
        {
            _sonJsonLdTeshis = string.Join(" | ", teklifler.Select(t =>
                $"\"{t.Satici}\" → {t.FiyatSayi:N2}"));
        }

        UrunDetay? secilen;
        if (!string.IsNullOrEmpty(hedefSatici))
        {
            secilen = teklifler.FirstOrDefault(t => SaticiEslesir(t.Satici, hedefSatici));
            if (secilen == null) return;
        }
        else
        {
            secilen = teklifler.OrderBy(t => t.FiyatSayi).FirstOrDefault();
        }

        if (secilen != null)
        {
            detay.Fiyat = secilen.Fiyat;
            detay.FiyatSayi = secilen.FiyatSayi;
            detay.Satici = secilen.Satici;
        }
    }

    private static void OfferParse(JsonElement offer, UrunDetay detay)
    {
        // Fiyatı birkaç olası yerden dene: price, priceSpecification.price, lowPrice
        decimal fiyat = OfferdanFiyatAl(offer);
        if (fiyat > 0)
        {
            detay.FiyatSayi = fiyat;
            detay.Fiyat = fiyat.ToString("N2", new System.Globalization.CultureInfo("tr-TR")) + " TL";
        }

        if (offer.TryGetProperty("seller", out var sellerProp) &&
            sellerProp.ValueKind == JsonValueKind.Object &&
            sellerProp.TryGetProperty("name", out var sellerName) &&
            sellerName.ValueKind == JsonValueKind.String)
            detay.Satici = sellerName.GetString() ?? "Bilinmiyor";
    }

    private static decimal OfferdanFiyatAl(JsonElement offer)
    {
        // 1) "price"
        if (offer.TryGetProperty("price", out var priceProp))
        {
            var f = FiyatElementiParse(priceProp);
            if (f > 0) return f;
        }

        // 2) "priceSpecification.price" (Teknosa gibi siteler bunu kullanabilir)
        if (offer.TryGetProperty("priceSpecification", out var spec) &&
            spec.ValueKind == JsonValueKind.Object &&
            spec.TryGetProperty("price", out var specPrice))
        {
            var f = FiyatElementiParse(specPrice);
            if (f > 0) return f;
        }

        // 3) "lowPrice" (AggregateOffer'da olabilir)
        if (offer.TryGetProperty("lowPrice", out var lowPrice))
        {
            var f = FiyatElementiParse(lowPrice);
            if (f > 0) return f;
        }

        return 0;
    }

    private static decimal FiyatElementiParse(JsonElement priceEl)
    {
        string? priceStr = priceEl.ValueKind switch
        {
            JsonValueKind.String => priceEl.GetString(),
            JsonValueKind.Number => priceEl.GetDecimal().ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
            _ => null
        };
        if (string.IsNullOrEmpty(priceStr)) return 0;

        return decimal.TryParse(priceStr, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out decimal fiyat) ? fiyat : 0;
    }

    // ─────────────────────────────────────────────────────────
    // Fiyat string normalizasyonu
    // "30.400,00 TL 36.199 TL" → ["30.400,00 TL", "36.199 TL"] → en düşük
    // İndirim sonrası fiyat her zaman üstü çizili eski fiyattan düşük olur
    // ─────────────────────────────────────────────────────────
    private static string? FiyatlardanEnDusuk(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        // Daha esnek Regex: 1.250,00 TL, 1250 TL, 1.250 veya sadece 1250 gibi sayısal formatları bulur.
        // Fiyat etiketleri içindeki (TL, ₺, TRY olsun veya olmasın) geçerli tutarları yakalar.
        var matches = Regex.Matches(text, @"(\d{1,3}(?:\.\d{3})*(?:,\d{1,2})?)(?:\s*(?:TL|₺|TRY))?",
            RegexOptions.IgnoreCase);

        var fiyatlar = new List<decimal>();
        foreach (Match m in matches)
        {
            var temiz = m.Groups[1].Value.Replace(".", "").Replace(",", ".");
            if (decimal.TryParse(temiz, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal f) && f > 0)
                fiyatlar.Add(f);
        }

        if (fiyatlar.Count == 0) return null;

        return fiyatlar.Min().ToString("N2", new System.Globalization.CultureInfo("tr-TR")) + " TL";
    }

    // ─────────────────────────────────────────────────────────
    // itemprop — ikincil yöntem
    // Semantic HTML, data-test-id'den çok daha stabil
    // ─────────────────────────────────────────────────────────
    private static string? ItemPropFiyat(string pageSource)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(pageSource);

        // Önce buy-box (offers container) içindekini ara, yoksa ilk price element
        var node = doc.DocumentNode.SelectSingleNode("(//*[@itemprop='offers']//*[@itemprop='price'])[1]")
                   ?? doc.DocumentNode.SelectSingleNode("(//*[@itemprop='price'])[1]");
        if (node == null) return null;

        // 1. content="771.00" — machine-readable, en güvenilir
        var content = node.GetAttributeValue("content", "");
        if (!string.IsNullOrEmpty(content) &&
            decimal.TryParse(content, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal f) && f > 0)
            return f.ToString("N2", new System.Globalization.CultureInfo("tr-TR")) + " TL";

        // 2. innerText'te birden fazla fiyat olabilir (güncel + üstü çizili eski) → en düşüğü al
        return FiyatlardanEnDusuk(node.InnerText);
    }

    // ─────────────────────────────────────────────────────────
    // "Sepete özel fiyat" label'ının yakınındaki fiyatı yakala
    // Hepsiburada'ya özel ama Türkçe label stabil
    // ─────────────────────────────────────────────────────────
    private static string? SepeteOzelFiyat(string pageSource)
    {
        var match = Regex.Match(pageSource,
            @"Sepete\s*özel\s*fiyat[\s\S]{0,300}?(\d{1,3}(?:\.\d{3})*(?:,\d{1,2})?)\s*TL",
            RegexOptions.IgnoreCase);
        if (!match.Success) return null;

        var temiz = match.Groups[1].Value.Replace(".", "").Replace(",", ".");
        if (decimal.TryParse(temiz, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out decimal f) && f > 0)
            return f.ToString("N2", new System.Globalization.CultureInfo("tr-TR")) + " TL";

        return null;
    }

    // ─────────────────────────────────────────────────────────
    // data-test-id — son çare fallback
    // Hepsiburada sık değiştiriyor, sadece üsttekiler başarısız olursa kullanılır
    // ─────────────────────────────────────────────────────────
    private static string FiyatCekFallback(IWebDriver driver)
    {
        var defaultPrice = driver.FindElements(By.CssSelector("[data-test-id='default-price']"));
        if (defaultPrice.Count > 0)
        {
            ((IJavaScriptExecutor)driver).ExecuteScript(
                "var el=document.querySelector('[data-test-id=\"see-earnings\"]'); if(el) el.remove();");
            defaultPrice = driver.FindElements(By.CssSelector("[data-test-id='default-price']"));
            if (defaultPrice.Count > 0)
            {
                // innerText'te güncel + üstü çizili eski fiyat olabilir → en düşüğü al
                var enDusuk = FiyatlardanEnDusuk(defaultPrice[0].Text);
                if (enDusuk != null) return enDusuk;
            }
        }

        var elements = driver.FindElements(By.CssSelector("[data-test-id='price-current-price']"));
        if (elements.Count > 0)
        {
            var hepsi = string.Join(" ", elements.Select(el => el.Text ?? ""));
            var enDusuk = FiyatlardanEnDusuk(hepsi);
            if (enDusuk != null) return enDusuk;
        }

        return "Fiyat bulunamadı";
    }

    // ─────────────────────────────────────────────────────────
    // 1) Sadece fiyat — FiyatGuncellemeJob tarafından kullanılır
    // ─────────────────────────────────────────────────────────
    public Task<string> GetPriceAsync(string url)
    {
        UrunUrliDogrula(url);
        return RetryAsync(() => Task.Run(async () =>
        {
            await _slot.WaitAsync();
            try
            {
                var site = SiteTespit(url);

                using var driver = DriverOlustur();
                driver.Navigate().GoToUrl(url);

                var beklemeSelectors = site == Site.Hepsiburada
                    ? new[] {
                        By.CssSelector("script[type='application/ld+json']"),
                        By.CssSelector("[itemprop='price']"),
                        By.CssSelector("[data-test-id='buyBox-seller']")
                      }
                    : new[] {
                        By.CssSelector("script[type='application/ld+json']"),
                        By.CssSelector("[itemprop='price']"),
                        By.CssSelector("h1")
                      };
                await SelectorBekleAsync(driver, beklemeSelectors, 15);

                var pageSource = driver.PageSource;

                // Multi-seller sitelerde URL'deki ?magaza= parametresi hedef satıcı belirler
                // BuyBoxSatici sadece Hepsiburada selector'larını kullandığı için ona özel
                string? hedefSatici = null;
                if (site == Site.Hepsiburada)
                    hedefSatici = UrlSaticiAl(url) ?? BuyBoxSatici(driver);
                else if (site == Site.N11)
                    hedefSatici = UrlSaticiAl(url);

                // 1a. n11 — Product JSON-LD'si yok, window.model JSON'unu kullan
                if (site == Site.N11)
                {
                    var n11Detay = N11SayfaParse(pageSource);
                    if (n11Detay != null && !string.IsNullOrEmpty(n11Detay.Fiyat))
                    {
                        _logger.LogInformation("[n11-SayfaParse] {Fiyat} ({Satici}) | {Url}",
                            n11Detay.Fiyat, n11Detay.Satici, url);
                        return n11Detay.Fiyat;
                    }
                }

                // 1b. JSON-LD
                var jsonLd = JsonLdParse(pageSource, hedefSatici);
                if (jsonLd != null && !string.IsNullOrEmpty(jsonLd.Fiyat))
                {
                    _logger.LogInformation("[JSON-LD] {Fiyat} ({Satici}) | {Url}",
                        jsonLd.Fiyat, jsonLd.Satici, url);
                    return jsonLd.Fiyat;
                }

                // 2. "Sepete özel fiyat" — sadece Hepsiburada
                if (site == Site.Hepsiburada)
                {
                    var sepetOzel = SepeteOzelFiyat(pageSource);
                    if (sepetOzel != null)
                    {
                        _logger.LogInformation("[SepeteOzel] {Fiyat} | {Url}", sepetOzel, url);
                        return sepetOzel;
                    }
                }

                // 3. itemprop
                var itemProp = ItemPropFiyat(pageSource);
                if (itemProp != null)
                {
                    _logger.LogInformation("[itemprop] {Fiyat} | {Url}", itemProp, url);
                    return itemProp;
                }

                // 4. data-test-id (sadece Hepsiburada — Çiçeksepeti'nde işe yaramaz)
                if (site == Site.Hepsiburada)
                {
                    var fallback = FiyatCekFallback(driver);
                    if (fallback == "Fiyat bulunamadı")
                        TeshisLogla(url, driver, pageSource);
                    _logger.LogInformation("[fallback] {Fiyat} | {Url}", fallback, url);
                    return fallback;
                }

                TeshisLogla(url, driver, pageSource);
                return "Fiyat bulunamadı";
            }
            finally { _slot.Release(); }
        }));
    }

    // ─────────────────────────────────────────────────────────
    // 2) Tam ürün detayı — Takip.razor tarafından kullanılır
    // ─────────────────────────────────────────────────────────
    public Task<UrunDetay> GetUrunDetayAsync(string url)
    {
        UrunUrliDogrula(url);
        return RetryAsync(() => Task.Run(async () =>
        {
            await _slot.WaitAsync();
            try
            {
                var site = SiteTespit(url);

                using var driver = DriverOlustur();
                driver.Navigate().GoToUrl(url);

                // Akıllı bekleme: site bazlı bekleme selector'ları
                var beklemeSelectors = site == Site.Hepsiburada
                    ? new[] {
                        By.CssSelector("script[type='application/ld+json']"),
                        By.CssSelector("[itemprop='price']"),
                        By.CssSelector("[data-test-id='buyBox-seller']")
                      }
                    : new[] {
                        By.CssSelector("script[type='application/ld+json']"),
                        By.CssSelector("[itemprop='price']"),
                        By.CssSelector("h1")
                      };
                await SelectorBekleAsync(driver, beklemeSelectors, 15);

                var pageSource = driver.PageSource;

                // Hedef satıcı (sadece Hepsiburada'da anlamlı — multi-seller buy box)
                // Multi-seller sitelerde URL'deki ?magaza= parametresi hedef satıcı belirler
                // BuyBoxSatici sadece Hepsiburada selector'larını kullandığı için ona özel
                string? hedefSatici = null;
                if (site == Site.Hepsiburada)
                    hedefSatici = UrlSaticiAl(url) ?? BuyBoxSatici(driver);
                else if (site == Site.N11)
                    hedefSatici = UrlSaticiAl(url);

                // 1. Ana parser — site bazlı
                _sonJsonLdTeshis = null;
                UrunDetay detay;

                if (site == Site.N11)
                {
                    // n11'de Product JSON-LD YOK, sayfa içi window.model JSON'ı kullan
                    detay = N11SayfaParse(pageSource) ?? new UrunDetay();
                    _logger.LogInformation(
                        "[n11-SayfaParse] Ad=\"{Ad}\" | Fiyat=\"{Fiyat}\" | Satici=\"{Satici}\" | Resim=\"{Resim}\"",
                        detay.Ad, detay.Fiyat, detay.Satici,
                        string.IsNullOrEmpty(detay.ResimUrl) ? "(yok)" : "var");
                }
                else
                {
                    detay = JsonLdParse(pageSource, hedefSatici) ?? new UrunDetay();
                    _logger.LogInformation(
                        "[JsonLd] hedefSatici=\"{Hedef}\" | offers=[{Teklifler}] | secilen=\"{Satici}\"/{Fiyat}",
                        hedefSatici ?? "(yok)",
                        _sonJsonLdTeshis ?? "(boş)",
                        detay.Satici,
                        detay.Fiyat);
                }
                detay.Url = url;

                // 2. Fiyat fallback zinciri
                if (string.IsNullOrEmpty(detay.Fiyat))
                {
                    if (site == Site.Hepsiburada)
                        detay.Fiyat = SepeteOzelFiyat(pageSource)
                                      ?? ItemPropFiyat(pageSource)
                                      ?? FiyatCekFallback(driver);
                    else
                        detay.Fiyat = ItemPropFiyat(pageSource) ?? "";
                }

                // 3. Ad DOM'dan tamamla
                if (string.IsNullOrEmpty(detay.Ad) || detay.Ad == "İsimsiz Ürün")
                {
                    IList<IWebElement> adEl;
                    if (site == Site.Hepsiburada)
                    {
                        adEl = driver.FindElements(By.CssSelector("[data-test-id='title'] h1"));
                        if (adEl.Count == 0)
                            adEl = driver.FindElements(By.CssSelector("[data-test-id='title']"));
                    }
                    else
                    {
                        adEl = driver.FindElements(By.CssSelector("h1"));
                    }
                    if (adEl.Count > 0)
                    {
                        var ad = adEl[0].Text?.Trim();
                        if (!string.IsNullOrWhiteSpace(ad)) detay.Ad = ad;
                    }
                }

                // 4. Resim DOM'dan tamamla
                if (string.IsNullOrEmpty(detay.ResimUrl))
                {
                    var resimFiltresi = site == Site.Hepsiburada
                        ? "https://productimages.hepsiburada"
                        : null;

                    // 4a. og:image meta tag — neredeyse her sitede var, en güvenilir fallback
                    var ogImage = driver.FindElements(By.CssSelector("meta[property='og:image']"))
                                        .FirstOrDefault();
                    if (ogImage != null)
                    {
                        var src = ogImage.GetAttribute("content")?.Trim();
                        if (!string.IsNullOrWhiteSpace(src) &&
                            (resimFiltresi == null || src.StartsWith(resimFiltresi)))
                            detay.ResimUrl = src;
                    }

                    // 4b. picture > img (modern responsive image)
                    if (string.IsNullOrEmpty(detay.ResimUrl))
                    {
                        foreach (var img in driver.FindElements(By.CssSelector("picture img")))
                        {
                            var src = ResimSrcAl(img);
                            if (string.IsNullOrWhiteSpace(src)) continue;
                            if (resimFiltresi != null && !src.StartsWith(resimFiltresi)) continue;
                            detay.ResimUrl = src;
                            break;
                        }
                    }
                    // 4c. picture > source srcset
                    if (string.IsNullOrEmpty(detay.ResimUrl))
                    {
                        foreach (var source in driver.FindElements(By.CssSelector("picture source")))
                        {
                            var srcset = source.GetAttribute("srcset")?.Trim();
                            if (string.IsNullOrWhiteSpace(srcset)) continue;
                            if (resimFiltresi != null && !srcset.StartsWith(resimFiltresi)) continue;
                            detay.ResimUrl = srcset.Split(' ')[0];
                            break;
                        }
                    }
                    // 4d. Generic son fallback: ilk uygun img (lazy-load attribute'larıyla)
                    if (string.IsNullOrEmpty(detay.ResimUrl))
                    {
                        foreach (var img in driver.FindElements(By.CssSelector("img")))
                        {
                            var src = ResimSrcAl(img);
                            if (string.IsNullOrWhiteSpace(src) || !src.StartsWith("http")) continue;
                            if (resimFiltresi != null && !src.StartsWith(resimFiltresi)) continue;
                            // İkon/spinner/placeholder kaçır — boyutu çok küçük olanları atla
                            if (src.Contains("placeholder") || src.Contains("loading") ||
                                src.Contains("spinner") || src.Contains("logo")) continue;
                            detay.ResimUrl = src;
                            break;
                        }
                    }
                }

                // 5. Satıcı DOM'dan tamamla — sadece Hepsiburada (Çiçeksepeti tek satıcı)
                if (site == Site.Hepsiburada &&
                    (string.IsNullOrEmpty(detay.Satici) || detay.Satici == "Bilinmiyor"))
                {
                    var saticiEl = driver.FindElements(By.CssSelector("[data-test-id='buyBox-seller'] a"));
                    if (saticiEl.Count > 0)
                    {
                        var satici = saticiEl[0].GetAttribute("title")?.Trim() ?? saticiEl[0].Text?.Trim();
                        if (!string.IsNullOrWhiteSpace(satici)) detay.Satici = satici;
                    }
                }
                else if ((site == Site.Ciceksepeti || site == Site.Teknosa || site == Site.Pttavm) &&
                         (string.IsNullOrEmpty(detay.Satici) || detay.Satici == "Bilinmiyor"))
                {
                    detay.Satici = SiteAdi(site);
                }
                else if (site == Site.N11 &&
                         (string.IsNullOrEmpty(detay.Satici) || detay.Satici == "Bilinmiyor"))
                {
                    // JSON-LD satıcı vermediyse URL'deki magaza, o da yoksa "n11" göster
                    detay.Satici = UrlSaticiAl(url) ?? "n11";
                }

                // Tam başarısızlık: hem ad hem fiyat yoksa → hata fırlat
                var adYok = detay.Ad == "İsimsiz Ürün" || string.IsNullOrEmpty(detay.Ad);
                var fiyatYok = string.IsNullOrEmpty(detay.Fiyat) || detay.Fiyat == "Fiyat bulunamadı";

                if (adYok && fiyatYok)
                {
                    TeshisLogla(url, driver, pageSource);
                    throw new InvalidOperationException(
                        "Ürün bilgisi alınamadı. Lütfen linkin geçerli bir ürün sayfasına ait olduğundan emin olun.");
                }

                // Kısmi başarısızlık (fiyat veya ad eksik) — hata fırlatmadan teşhis logla
                if (adYok || fiyatYok)
                    TeshisLogla(url, driver, pageSource);

                _logger.LogInformation("[ScraperService] Detay: {Ad} | {Fiyat}", detay.Ad, detay.Fiyat);
                return detay;
            }
            finally { _slot.Release(); }
        }));
    }

    // ─────────────────────────────────────────────────────────
    // 3) Kategori ürün listesi — KategoriUrunler.razor
    // ─────────────────────────────────────────────────────────
    public Task<List<UrunKart>> GetKategoriUrunleriAsync(string kategoriUrl) =>
        RetryAsync(() => Task.Run(async () =>
        {
            await _slot.WaitAsync();
            try
            {
                using var driver = DriverOlustur();
                driver.Navigate().GoToUrl(kategoriUrl);

                // Akıllı bekleme: ürün kartları gelene kadar (max 15 sn)
                await SelectorBekleAsync(driver, new[] {
                    By.CssSelector("li[class*='productListContent-']"),
                    By.CssSelector("div[data-test-id*='product-card']")
                }, 15);

                string source;
                try { source = driver.PageSource; }
                catch { await Task.Delay(2000); source = driver.PageSource; }

                var doc = new HtmlDocument();
                doc.LoadHtml(source);
                var urunler = new List<UrunKart>();

                var kartlar = doc.DocumentNode.SelectNodes("//li[contains(@class,'productListContent-')]")
                    ?? doc.DocumentNode.SelectNodes("//div[contains(@data-test-id,'product-card')]");

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

                        if (string.IsNullOrWhiteSpace(ad) || string.IsNullOrWhiteSpace(fiyat)) continue;

                        if (link != null && !link.StartsWith("http"))
                            link = "https://www.hepsiburada.com" + link;

                        urunler.Add(new UrunKart { Ad = ad, Fiyat = fiyat, ResimUrl = resim ?? "", UrunUrl = link ?? "" });
                    }
                }

                return urunler;
            }
            finally { _slot.Release(); }
        }));
}

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
    public decimal FiyatSayi { get; set; }
    public string ResimUrl { get; set; } = "";
    public string Satici { get; set; } = "Bilinmiyor";
}
