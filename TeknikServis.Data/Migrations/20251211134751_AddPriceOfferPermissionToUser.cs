using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeknikServis.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceOfferPermissionToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPriceOfferEnabled",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPriceOfferEnabled",
                table: "AspNetUsers");
        }
    }
}
