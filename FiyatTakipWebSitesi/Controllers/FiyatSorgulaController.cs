// =====================================================
// FiyatSorgulaController.cs
// /api/fiyat-sorgula uç noktasını yönetir. Verilen
// ürün URL'sini ScraperService aracılığıyla ziyaret
// ederek güncel fiyatı çeker ve JSON olarak döndürür.
// İsteğe bağlı olarak veritabanındaki ürünün fiyatını
// da otomatik günceller.
// =====================================================

using FiyatTakipWebSitesi.DTOs;
using FiyatTakipWebSitesi.Services;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace FiyatTakipWebSitesi.Controllers;

[ApiController]
[Route("api/fiyat-sorgula")]
[Produces("application/json")]
public class FiyatSorgulaController : ControllerBase
{
    private readonly ScraperService _scraperService;
    private readonly UrunService _urunService;
    private readonly ILogger<FiyatSorgulaController> _logger;

    public FiyatSorgulaController(
        ScraperService scraperService,
        UrunService urunService,
        ILogger<FiyatSorgulaController> logger)
    {
        _scraperService = scraperService;
        _urunService = urunService;
        _logger = logger;
    }

    // ── POST /api/fiyat-sorgula ───────────────────────
    /// <summary>
    /// Verilen URL'den anlık fiyat bilgisini çeker (scrape eder).
    /// Body'de { "url": "...", "urunId": 5 } göndererek ürünün
    /// veritabanı fiyatını da otomatik güncelleyebilirsiniz.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(FiyatSorgulaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Sorgula([FromBody] FiyatSorgulaRequest istek)
    {
        if (string.IsNullOrWhiteSpace(istek.URL))
            return BadRequest(new { hata = "Geçerli bir URL gereklidir." });

        if (!Uri.TryCreate(istek.URL, UriKind.Absolute, out _))
            return BadRequest(new { hata = "URL formatı geçersiz." });

        _logger.LogInformation("Fiyat sorgulanıyor: {URL}", istek.URL);

        try
        {
            var fiyatMetin = await _scraperService.GetPriceAsync(istek.URL);

            var basarili = fiyatMetin != "Fiyat bulunamadı";
            decimal? fiyatSayi = null;

            if (basarili)
            {
                // "1.299 TL" → 1299 gibi sayıya çevir
                var temiz = fiyatMetin
                    .Replace(" TL", "")
                    .Replace(".", "")
                    .Replace(",", ".")
                    .Trim();

                if (decimal.TryParse(temiz, NumberStyles.Any,
                        CultureInfo.InvariantCulture, out var sayi))
                {
                    fiyatSayi = sayi;
                }
            }

            // İsteğe bağlı: ürün fiyatını veritabanında güncelle
            if (basarili && fiyatSayi.HasValue && istek.UrunId.HasValue)
            {
                await _urunService.FiyatGuncelleAsync(istek.UrunId.Value, fiyatSayi.Value);
                _logger.LogInformation(
                    "Ürün {UrunId} fiyatı {Fiyat} TL olarak güncellendi.",
                    istek.UrunId.Value, fiyatSayi.Value);
            }

            var yanit = new FiyatSorgulaResponse
            {
                URL = istek.URL,
                FiyatMetin = fiyatMetin,
                FiyatSayi = fiyatSayi,
                Basarili = basarili,
                HataMesaji = basarili ? null : "Sayfa kaynağında geçerli bir fiyat bulunamadı.",
                SorgulanmaTarihi = DateTime.UtcNow
            };

            return Ok(yanit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fiyat sorgulama sırasında hata oluştu. URL={URL}", istek.URL);
            return StatusCode(500, new FiyatSorgulaResponse
            {
                URL = istek.URL,
                FiyatMetin = string.Empty,
                Basarili = false,
                HataMesaji = "Fiyat sorgulanırken beklenmeyen bir hata oluştu: " + ex.Message,
                SorgulanmaTarihi = DateTime.UtcNow
            });
        }
    }

    // ── GET /api/fiyat-sorgula?url=... ────────────────
    /// <summary>
    /// URL parametresiyle GET isteği üzerinden anlık fiyat sorgular.
    /// Sadece okuma amaçlıdır; veritabanı güncellemesi yapmaz.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(FiyatSorgulaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SorgulaGet([FromQuery] string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return BadRequest(new { hata = "Geçerli bir 'url' parametresi gereklidir." });

        return await Sorgula(new FiyatSorgulaRequest { URL = url });
    }
}
