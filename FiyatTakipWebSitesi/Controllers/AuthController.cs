// =====================================================
// AuthController.cs
// Kullanıcı kaydı, girişi, profil görüntüleme ve
// şifre değiştirme işlemlerini yöneten API controller.
// POST /api/auth/kayit    → Yeni kullanıcı kaydı
// POST /api/auth/giris    → Giriş + JWT token
// GET  /api/auth/profil   → [Authorize] Profil bilgisi
// PUT  /api/auth/sifre    → [Authorize] Şifre değiştirme
// =====================================================

using System.Security.Claims;
using FiyatTakipWebSitesi.DTOs;
using FiyatTakipWebSitesi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FiyatTakipWebSitesi.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly KullaniciService _kullaniciService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(KullaniciService kullaniciService, ILogger<AuthController> logger)
    {
        _kullaniciService = kullaniciService;
        _logger = logger;
    }

    // ── POST /api/auth/kayit ──────────────────────────
    /// <summary>Yeni kullanıcı kaydı oluşturur ve JWT token döner</summary>
    [HttpPost("kayit")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Kayit([FromBody] KayitRequest istek)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var yanit = await _kullaniciService.KayitAsync(istek);
            return StatusCode(201, yanit);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { hata = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kullanıcı kaydı sırasında hata oluştu.");
            return StatusCode(500, new { hata = "Kayıt işlemi sırasında bir hata oluştu." });
        }
    }

    // ── POST /api/auth/giris ──────────────────────────
    /// <summary>E-posta ve şifre ile giriş yapar, JWT token döner</summary>
    [HttpPost("giris")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Giris([FromBody] GirisRequest istek)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var yanit = await _kullaniciService.GirisAsync(istek);
            return Ok(yanit);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { hata = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Giriş sırasında hata oluştu.");
            return StatusCode(500, new { hata = "Giriş işlemi sırasında bir hata oluştu." });
        }
    }

    // ── GET /api/auth/profil ──────────────────────────
    /// <summary>Oturum açmış kullanıcının profil bilgisini döner</summary>
    [Authorize]
    [HttpGet("profil")]
    [ProducesResponseType(typeof(ProfilResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Profil()
    {
        var kullaniciIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(kullaniciIdStr, out var kullaniciId))
            return Unauthorized(new { hata = "Geçersiz token." });

        var profil = await _kullaniciService.ProfilGetirAsync(kullaniciId);
        if (profil is null)
            return NotFound(new { hata = "Kullanıcı bulunamadı." });

        return Ok(profil);
    }

    // ── PUT /api/auth/sifre ───────────────────────────
    /// <summary>Mevcut şifreyi doğrulayarak yeni şifre belirler</summary>
    [Authorize]
    [HttpPut("sifre")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SifreDegistir([FromBody] SifreDegistirRequest istek)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var kullaniciIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(kullaniciIdStr, out var kullaniciId))
            return Unauthorized(new { hata = "Geçersiz token." });

        try
        {
            var basarili = await _kullaniciService.SifreDegistirAsync(kullaniciId, istek);
            if (!basarili)
                return NotFound(new { hata = "Kullanıcı bulunamadı." });

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { hata = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Şifre değiştirme sırasında hata oluştu.");
            return StatusCode(500, new { hata = "Şifre değiştirme sırasında bir hata oluştu." });
        }
    }
}
