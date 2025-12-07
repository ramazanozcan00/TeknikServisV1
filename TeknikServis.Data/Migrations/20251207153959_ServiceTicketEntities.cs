using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeknikServis.Data.Migrations
{
    /// <inheritdoc />
    public partial class ServiceTicketEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Accessories",
                table: "ServiceTickets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InvoiceDate",
                table: "ServiceTickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PdfPath",
                table: "ServiceTickets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhysicalDamage",
                table: "ServiceTickets",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Accessories",
                table: "ServiceTickets");

            migrationBuilder.DropColumn(
                name: "InvoiceDate",
                table: "ServiceTickets");

            migrationBuilder.DropColumn(
                name: "PdfPath",
                table: "ServiceTickets");

            migrationBuilder.DropColumn(
                name: "PhysicalDamage",
                table: "ServiceTickets");
        }
    }
}
