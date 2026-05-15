using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FiyatTakipWebSitesi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSaticiToUrun : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Satici",
                table: "Urunler",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Satici",
                table: "Urunler");
        }
    }
}
