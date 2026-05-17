// =====================================================
// ApplicationDbContext.cs
// Bu sınıf, Entity Framework Core kullanarak veritabanı
// bağlantısını ve tablo yapılarını yöneten ana veritabanı
// bağlam (context) sınıfıdır. Kullanıcılar, ürünler,
// kategoriler, fiyat geçmişi ve uyarılar gibi tüm
// veritabanı tablolarını ve aralarındaki ilişkileri burada tanımlar.
// =====================================================

using FiyatTakipWebSitesi.Models;
using Microsoft.EntityFrameworkCore;

namespace FiyatTakipWebSitesi.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    // Veritabanındaki tabloları temsil eden DbSet'ler
    public DbSet<Kullanici> Kullanicilar { get; set; } = null!;
    public DbSet<Kategori> Kategoriler { get; set; } = null!;
    public DbSet<UrunModeli> UrunModelleri { get; set; } = null!;
    public DbSet<Urun> Urunler { get; set; } = null!;
    public DbSet<FiyatGecmisi> FiyatGecmisleri { get; set; } = null!;
    public DbSet<Uyari> Uyarilar { get; set; } = null!;
    public DbSet<TakipModeli> TakipModelleri { get; set; } = null!;

    // Tablo ilişkilerini, kısıtlamaları ve index'leri burada yapılandırıyoruz
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Bir ürün yalnızca bir kategoriye ait olabilir.
        // Kategori silinirse, bağlı ürünler de silinir (Cascade)
        modelBuilder.Entity<Urun>()
            .HasOne(u => u.Kategori)
            .WithMany(k => k.Urunler)
            .HasForeignKey(u => u.KategoriId)
            .OnDelete(DeleteBehavior.Cascade);

        // Bir ürün isteğe bağlı olarak bir modele bağlanabilir.
        // Model silinirse, ürünün UrunModeliId alanı NULL yapılır (SetNull)
        modelBuilder.Entity<Urun>()
            .HasOne(u => u.UrunModeli)
            .WithMany(m => m.Listeler)
            .HasForeignKey(u => u.UrunModeliId)
            .OnDelete(DeleteBehavior.SetNull);

        // UrunModeli kategoriye bağlı. Cascade YOK — çünkü Kategori → Urun de
        // cascade yapıyor ve SQL Server çoklu cascade yolunu (Kategori → Urun ve
        // Kategori → UrunModeli → Urun) yasaklıyor. NoAction ile: bir kategoriyi
        // silmek için önce o kategorideki modelleri silmek gerekir.
        modelBuilder.Entity<UrunModeli>()
            .HasOne(m => m.Kategori)
            .WithMany()
            .HasForeignKey(m => m.KategoriId)
            .OnDelete(DeleteBehavior.NoAction);

        // Bir ürün yalnızca bir kullanıcıya ait olabilir.
        // Kullanıcı silinirse, ürünün UserId alanı NULL yapılır (SetNull)
        modelBuilder.Entity<Urun>()
            .HasOne(u => u.Kullanici)
            .WithMany(k => k.Urunler)
            .HasForeignKey(u => u.UserId)
            .OnDelete(DeleteBehavior.SetNull);


        // Bir fiyat geçmişi kaydı yalnızca bir ürüne aittir.
        // Ürün silinirse, o ürüne ait tüm fiyat geçmişi de silinir (Cascade)
        modelBuilder.Entity<FiyatGecmisi>()
            .HasOne(fg => fg.Urun)
            .WithMany(u => u.FiyatGecmisleri)
            .HasForeignKey(fg => fg.UrunId)
            .OnDelete(DeleteBehavior.Cascade);

        // Bir uyarı yalnızca bir ürüne aittir.
        // Ürün silinirse, o ürüne ait tüm uyarılar da silinir (Cascade)
        modelBuilder.Entity<Uyari>()
            .HasOne(uy => uy.Urun)
            .WithMany(u => u.Uyarilar)
            .HasForeignKey(uy => uy.UrunId)
            .OnDelete(DeleteBehavior.Cascade);

        // Bir uyarı yalnızca bir kullanıcıya aittir.
        // Kullanıcı silinirse, o kullanıcıya ait tüm uyarılar da silinir (Cascade)
        modelBuilder.Entity<Uyari>()
            .HasOne(uy => uy.Kullanici)
            .WithMany(k => k.Uyarilar)
            .HasForeignKey(uy => uy.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Para birimi alanları için hassasiyet ayarları:
        // HasPrecision(10, 2) → toplam 10 basamak, virgülden sonra 2 basamak (örn: 99999999.99)
        modelBuilder.Entity<Urun>()
            .Property(u => u.BaslangicFiyati)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Urun>()
            .Property(u => u.SonFiyati)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Urun>()
            .Property(u => u.HedefFiyati)
            .HasPrecision(10, 2);

        modelBuilder.Entity<FiyatGecmisi>()
            .Property(fg => fg.Fiyat)
            .HasPrecision(10, 2);

        modelBuilder.Entity<FiyatGecmisi>()
            .Property(fg => fg.OncekiFiyat)
            .HasPrecision(10, 2);

        modelBuilder.Entity<FiyatGecmisi>()
            .Property(fg => fg.FiyatDegisimi)
            .HasPrecision(10, 2);

        // Yüzde değeri için daha dar hassasiyet: toplam 5 basamak, 2 ondalık (örn: 999.99)
        modelBuilder.Entity<FiyatGecmisi>()
            .Property(fg => fg.FiyatDegisimYüzdesi)
            .HasPrecision(5, 2);

        // Aynı e-posta adresiyle iki kullanıcı kaydedilemez
        modelBuilder.Entity<Kullanici>()
            .HasIndex(k => k.Email)
            .IsUnique();

        // Fiyat geçmişinde tarihe göre hızlı sorgulama için index
        modelBuilder.Entity<FiyatGecmisi>()
            .HasIndex(fg => fg.Tarih);

        // Uyarılarda oluşturulma tarihine göre hızlı sorgulama için index
        modelBuilder.Entity<Uyari>()
            .HasIndex(uy => uy.OlusturulmaTarihi);

        // ── TakipModeli ilişkileri ───────────────────────────────────
        // Kullanıcı silinirse takip kayıtları da silinir
        modelBuilder.Entity<TakipModeli>()
            .HasOne(t => t.Kullanici)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Model silinirse takip kayıtları da silinir
        modelBuilder.Entity<TakipModeli>()
            .HasOne(t => t.UrunModeli)
            .WithMany()
            .HasForeignKey(t => t.UrunModeliId)
            .OnDelete(DeleteBehavior.Cascade);

        // Aynı kullanıcı aynı modeli iki kez takipe alamaz
        modelBuilder.Entity<TakipModeli>()
            .HasIndex(t => new { t.UserId, t.UrunModeliId })
            .IsUnique();

        // Para birimi alanları
        modelBuilder.Entity<TakipModeli>()
            .Property(t => t.HedefFiyat)
            .HasPrecision(10, 2);

        modelBuilder.Entity<TakipModeli>()
            .Property(t => t.SonBildirilenEnUcuz)
            .HasPrecision(10, 2);
    }
}