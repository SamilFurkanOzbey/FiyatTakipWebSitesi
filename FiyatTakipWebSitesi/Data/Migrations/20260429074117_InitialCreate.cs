// =====================================================
// 20260429074117_InitialCreate.cs
// Bu dosya, Entity Framework Core tarafından üretilen
// ilk veritabanı migration dosyasıdır. Çalıştırıldığında
// SQL Server'da aşağıdaki tabloları oluşturur:
//   - Kategoriler  : Ürün kategorileri
//   - Kullanicilar : Sisteme kayıtlı kullanıcılar
//   - Urunler      : Takip edilen ürünler (FK: Kategori, Kullanici)
//   - FiyatGecmisleri : Her ürüne ait fiyat geçmişi (FK: Urun)
//   - Uyarilar     : Fiyat uyarıları (FK: Urun, Kullanici)
// Down() metodu ise bu tabloları sırayla kaldırır.
// =====================================================

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FiyatTakipWebSitesi.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Kategoriler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Icon = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OlusturulmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kategoriler", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Kullanicilar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Soyad = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TelefonNumarasi = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SifreHash = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    SifreTuzu = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    OlusturulmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SonGirisTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Aktif = table.Column<bool>(type: "bit", nullable: false),
                    EmailDogrulandi = table.Column<bool>(type: "bit", nullable: false),
                    EmailBildirimleriniAc = table.Column<bool>(type: "bit", nullable: false),
                    PushBildirimleriniAc = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kullanicilar", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Urunler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    URL = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Resim = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    KategoriId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    BaslangicFiyati = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    SonFiyati = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    EklendigiTarih = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SonGuncellemeTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Aktif = table.Column<bool>(type: "bit", nullable: false),
                    FiyatDususuBildir = table.Column<bool>(type: "bit", nullable: false),
                    FiyatArtisiiBildir = table.Column<bool>(type: "bit", nullable: false),
                    HedefFiyati = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Urunler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Urunler_Kategoriler_KategoriId",
                        column: x => x.KategoriId,
                        principalTable: "Kategoriler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Urunler_Kullanicilar_UserId",
                        column: x => x.UserId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "FiyatGecmisleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UrunId = table.Column<int>(type: "int", nullable: false),
                    Fiyat = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Tarih = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OncekiFiyat = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    FiyatDegisimi = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    FiyatDegisimYüzdesi = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    StokDurumuMevcutMu = table.Column<bool>(type: "bit", nullable: false),
                    Durum = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiyatGecmisleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FiyatGecmisleri_Urunler_UrunId",
                        column: x => x.UrunId,
                        principalTable: "Urunler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Uyarilar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UrunId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Baslik = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Mesaj = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Tip = table.Column<int>(type: "int", nullable: false),
                    OlusturulmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Okundu = table.Column<bool>(type: "bit", nullable: false),
                    OkunduğuTarih = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Uyarilar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Uyarilar_Kullanicilar_UserId",
                        column: x => x.UserId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Uyarilar_Urunler_UrunId",
                        column: x => x.UrunId,
                        principalTable: "Urunler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FiyatGecmisleri_Tarih",
                table: "FiyatGecmisleri",
                column: "Tarih");

            migrationBuilder.CreateIndex(
                name: "IX_FiyatGecmisleri_UrunId",
                table: "FiyatGecmisleri",
                column: "UrunId");

            migrationBuilder.CreateIndex(
                name: "IX_Kullanicilar_Email",
                table: "Kullanicilar",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Urunler_KategoriId",
                table: "Urunler",
                column: "KategoriId");

            migrationBuilder.CreateIndex(
                name: "IX_Urunler_UserId",
                table: "Urunler",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Uyarilar_OlusturulmaTarihi",
                table: "Uyarilar",
                column: "OlusturulmaTarihi");

            migrationBuilder.CreateIndex(
                name: "IX_Uyarilar_UrunId",
                table: "Uyarilar",
                column: "UrunId");

            migrationBuilder.CreateIndex(
                name: "IX_Uyarilar_UserId",
                table: "Uyarilar",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FiyatGecmisleri");

            migrationBuilder.DropTable(
                name: "Uyarilar");

            migrationBuilder.DropTable(
                name: "Urunler");

            migrationBuilder.DropTable(
                name: "Kategoriler");

            migrationBuilder.DropTable(
                name: "Kullanicilar");
        }
    }
}
