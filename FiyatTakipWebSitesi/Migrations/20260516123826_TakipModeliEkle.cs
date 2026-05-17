using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FiyatTakipWebSitesi.Migrations
{
    /// <inheritdoc />
    public partial class TakipModeliEkle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TakipModelleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    UrunModeliId = table.Column<int>(type: "INTEGER", nullable: false),
                    HedefFiyat = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: true),
                    FiyatDususuBildir = table.Column<bool>(type: "INTEGER", nullable: false),
                    FiyatArtisiBildir = table.Column<bool>(type: "INTEGER", nullable: false),
                    EklendigiTarih = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Aktif = table.Column<bool>(type: "INTEGER", nullable: false),
                    SonBildirilenEnUcuz = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TakipModelleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TakipModelleri_Kullanicilar_UserId",
                        column: x => x.UserId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TakipModelleri_UrunModelleri_UrunModeliId",
                        column: x => x.UrunModeliId,
                        principalTable: "UrunModelleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TakipModelleri_UrunModeliId",
                table: "TakipModelleri",
                column: "UrunModeliId");

            migrationBuilder.CreateIndex(
                name: "IX_TakipModelleri_UserId_UrunModeliId",
                table: "TakipModelleri",
                columns: new[] { "UserId", "UrunModeliId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TakipModelleri");
        }
    }
}
