using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeknikServis.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTechnicianToTicket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TechnicianId",
                table: "ServiceTickets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTickets_TechnicianId",
                table: "ServiceTickets",
                column: "TechnicianId");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceTickets_AspNetUsers_TechnicianId",
                table: "ServiceTickets",
                column: "TechnicianId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ServiceTickets_AspNetUsers_TechnicianId",
                table: "ServiceTickets");

            migrationBuilder.DropIndex(
                name: "IX_ServiceTickets_TechnicianId",
                table: "ServiceTickets");

            migrationBuilder.DropColumn(
                name: "TechnicianId",
                table: "ServiceTickets");
        }
    }
}
