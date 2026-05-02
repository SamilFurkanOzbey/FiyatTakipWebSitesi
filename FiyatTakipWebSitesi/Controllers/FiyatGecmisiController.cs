// =====================================================
// FiyatGecmisiController.cs
// /api/fiyat-gecmisi uç noktasını yönetir. Ürüne ait
// fiyat geçmişi kayıtlarını listeler; tarih aralığı,
// limit ve istatistik (min/max/ortalama) sorgularını
// destekler. FiyatGecmisiService'i kullanır.
// =====================================================

using FiyatTakipWebSitesi.DTOs;
using FiyatTakipWebSitesi.Services;
using Microsoft.AspNetCore.Mvc;

namespace FiyatTakipWebSitesi.Controllers;

[ApiController]
[Route("api/fiyat-gecmisi")]
[Produces("application/json")]
public class FiyatGecmisiController : ControllerBase
{
    private readonly FiyatGecmisiService _fiyatGecmisiService;
    private readonly ILogger<FiyatGecmisiController> _logger;

    public FiyatGecmisiController(
        FiyatGecmisiService fiyatGecmisiService,
        ILogger<FiyatGecmisiController> logger)
    {
        _fiyatGecmisiService = fiyatGecmisiService;
        _logger = logger;
    }

    // ── GET /api/fiyat-gecmisi ────────────────────────
    /// <summary>Bir ürünün fiyat geçmişini döndürür</summary>
    /// <param name="urunId">Ürün kimliği (zorunlu)</param>
    /// <param name="limit">Kaç kayıt döneceği (opsiyonel)</param>
    /// <param name="baslangic">Tarih aralığı başlangıcı (opsiyonel)</param>
    /// <param name="bitis">Tarih aralığı bitişi (opsiyonel)</param>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<FiyatGecmisiResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetFiyatGecmisi(
        [FromQuery] int urunId,
        [FromQuery] int? limit,
        [FromQuery] DateTime? baslangic,
        [FromQuery] DateTime? bitis)
    {
        if (urunId <= 0)
            return BadRequest(new { hata = "Geçerli bir 'urunId' parametresi gereklidir." });

        try
        {
            if (baslangic.HasValue && bitis.HasValue)
            {
                if (baslangic > bitis)
                    return BadRequest(new { hata = "'baslangic' tarihi 'bitis' tarihinden büyük olamaz." });

                var aralikSonucu = await _fiyatGecmisiService
                    .GetByDateRangeAsync(urunId, baslangic.Value, bitis.Value);

                return Ok(aralikSonucu.Select(MapToResponse));
            }

            var liste = await _fiyatGecmisiService.GetByUrunAsync(urunId, limit);
            return Ok(liste.Select(MapToResponse));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fiyat geçmişi alınırken hata oluştu. UrunId={UrunId}", urunId);
            return StatusCode(500, new { hata = "Fiyat geçmişi alınırken bir hata oluştu." });
        }
    }

    // ── GET /api/fiyat-gecmisi/istatistik ─────────────
    /// <summary>Ürüne ait min/max/ortalama fiyat istatistiklerini döndürür</summary>
    /// <param name="urunId">Ürün kimliği (zorunlu)</param>
    /// <param name="sonGun">Kaç günlük ortalama hesaplanacak (varsayılan: 30)</param>
    [HttpGet("istatistik")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetIstatistik(
        [FromQuery] int urunId,
        [FromQuery] int sonGun = 30)
    {
        if (urunId <= 0)
            return BadRequest(new { hata = "Geçerli bir 'urunId' parametresi gereklidir." });

        try
        {
            var (min, max) = await _fiyatGecmisiService.GetMinMaxAsync(urunId);
            var ortalama = await _fiyatGecmisiService.GetOrtalamaBySonGunAsync(urunId, sonGun);

            return Ok(new
            {
                urunId,
                minFiyat = min,
                maxFiyat = max,
                ortalamaFiyat = Math.Round(ortalama, 2),
                ortalamaGunSayisi = sonGun
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fiyat istatistikleri alınırken hata oluştu. UrunId={UrunId}", urunId);
            return StatusCode(500, new { hata = "İstatistikler alınırken bir hata oluştu." });
        }
    }

    // ── Yardımcı Dönüşüm Metodu ───────────────────────

    private static FiyatGecmisiResponse MapToResponse(Models.FiyatGecmisi fg) => new()
    {
        Id = fg.Id,
        UrunId = fg.UrunId,
        Fiyat = fg.Fiyat,
        OncekiFiyat = fg.OncekiFiyat,
        FiyatDegisimi = fg.FiyatDegisimi,
        FiyatDegisimYuzdesi = fg.FiyatDegisimYüzdesi,
        StokDurumuMevcutMu = fg.StokDurumuMevcutMu,
        Durum = fg.Durum,
        Tarih = fg.Tarih
    };
}
