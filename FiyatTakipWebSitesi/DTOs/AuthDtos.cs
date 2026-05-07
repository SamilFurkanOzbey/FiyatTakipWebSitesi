// =====================================================
// AuthDtos.cs
// Kullanıcı kaydı, girişi ve profil bilgisi için
// kullanılan veri transfer nesneleri (DTO).
// =====================================================

using System.ComponentModel.DataAnnotations;

namespace FiyatTakipWebSitesi.DTOs;

// ── Kayıt İsteği ──────────────────────────────────────

/// <summary>Yeni kullanıcı kaydı için kullanılan DTO</summary>
public class KayitRequest
{
    [Required(ErrorMessage = "Ad zorunludur.")]
    [MaxLength(50)]
    public string Ad { get; set; } = string.Empty;

    [Required(ErrorMessage = "Soyad zorunludur.")]
    [MaxLength(50)]
    public string Soyad { get; set; } = string.Empty;

    [Required(ErrorMessage = "E-posta zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
    [MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şifre zorunludur.")]
    [MinLength(6, ErrorMessage = "Şifre en az 6 karakter olmalıdır.")]
    public string Sifre { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? TelefonNumarasi { get; set; }
}

// ── Giriş İsteği ──────────────────────────────────────

/// <summary>Kullanıcı girişi için kullanılan DTO</summary>
public class GirisRequest
{
    [Required(ErrorMessage = "E-posta zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şifre zorunludur.")]
    public string Sifre { get; set; } = string.Empty;
}

// ── Kimlik Doğrulama Yanıtı ───────────────────────────

/// <summary>Başarılı giriş veya kayıt sonrası dönen JWT ve kullanıcı bilgisi</summary>
public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public int KullaniciId { get; set; }
    public string Ad { get; set; } = string.Empty;
    public string Soyad { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime BitisTarihi { get; set; }
}

// ── Profil Yanıtı ─────────────────────────────────────

/// <summary>Kimliği doğrulanmış kullanıcının profil bilgisi</summary>
public class ProfilResponse
{
    public int Id { get; set; }
    public string Ad { get; set; } = string.Empty;
    public string Soyad { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? TelefonNumarasi { get; set; }
    public DateTime OlusturulmaTarihi { get; set; }
    public DateTime? SonGirisTarihi { get; set; }
    public bool EmailBildirimleriniAc { get; set; }
    public bool PushBildirimleriniAc { get; set; }
    public int TakipEdilenUrunSayisi { get; set; }
    public int OkunmamisUyariSayisi { get; set; }
}

// ── Şifre Değiştirme İsteği ───────────────────────────

public class SifreDegistirRequest
{
    [Required]
    public string MevcutSifre { get; set; } = string.Empty;

    [Required]
    [MinLength(6, ErrorMessage = "Yeni şifre en az 6 karakter olmalıdır.")]
    public string YeniSifre { get; set; } = string.Empty;
}
