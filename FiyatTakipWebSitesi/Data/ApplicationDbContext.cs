using FiyatTakipWebSitesi.Models;
using Microsoft.EntityFrameworkCore;

namespace FiyatTakipWebSitesi.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Kullanici> Kullanicilar { get; set; }
    public DbSet<Kategori> Kategoriler { get; set; }
    public DbSet<Urun> Urunler { get; set; }
    public DbSet<FiyatGecmisi> FiyatGecmisleri { get; set; }
    public DbSet<Uyari> Uyarilar { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Kategori - Urun ilişkisi
        modelBuilder.Entity<Urun>()
            .HasOne(u => u.Kategori)
            .WithMany(k => k.Urunler)
            .HasForeignKey(u => u.KategoriId)
            .OnDelete(DeleteBehavior.Cascade);

        // Kullanici - Urun ilişkisi
        modelBuilder.Entity<Urun>()
            .HasOne(u => u.Kullanici)
            .WithMany(k => k.Urunler)
            .HasForeignKey(u => u.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Urun - FiyatGecmisi ilişkisi
        modelBuilder.Entity<FiyatGecmisi>()
            .HasOne(fg => fg.Urun)
            .WithMany(u => u.FiyatGecmisleri)
            .HasForeignKey(fg => fg.UrunId)
            .OnDelete(DeleteBehavior.Cascade);

        // Urun - Uyari ilişkisi
        modelBuilder.Entity<Uyari>()
            .HasOne(uy => uy.Urun)
            .WithMany(u => u.Uyarilar)
            .HasForeignKey(uy => uy.UrunId)
            .OnDelete(DeleteBehavior.Cascade);

        // Kullanici - Uyari ilişkisi
        modelBuilder.Entity<Uyari>()
            .HasOne(uy => uy.Kullanici)
            .WithMany(k => k.Uyarilar)
            .HasForeignKey(uy => uy.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Decimal precision ayarları
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

        modelBuilder.Entity<FiyatGecmisi>()
            .Property(fg => fg.FiyatDegisimYüzdesi)
            .HasPrecision(5, 2);

        // Unique constraints
        modelBuilder.Entity<Kullanici>()
            .HasIndex(k => k.Email)
            .IsUnique();

        // Index'ler
        modelBuilder.Entity<FiyatGecmisi>()
            .HasIndex(fg => fg.Tarih);

        modelBuilder.Entity<Uyari>()
            .HasIndex(uy => uy.OlusturulmaTarihi);
    }
}
