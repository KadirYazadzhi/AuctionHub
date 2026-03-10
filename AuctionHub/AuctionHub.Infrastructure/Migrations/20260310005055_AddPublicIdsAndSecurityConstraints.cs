using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuctionHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicIdsAndSecurityConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PublicId",
                table: "Auctions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "PublicId",
                table: "AspNetUsers",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddCheckConstraint(
                name: "CK_ApplicationUser_WalletBalance_Positive",
                table: "AspNetUsers",
                sql: "[WalletBalance] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ApplicationUser_WalletBalance_Positive",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "AspNetUsers");
        }
    }
}
