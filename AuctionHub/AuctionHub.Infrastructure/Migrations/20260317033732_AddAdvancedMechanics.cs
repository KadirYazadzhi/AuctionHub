using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuctionHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdvancedMechanics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DutchDecrementAmount",
                table: "Auctions",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DutchDecrementIntervalMinutes",
                table: "Auctions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDutchAuction",
                table: "Auctions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastDutchDecrement",
                table: "Auctions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ParticipationFee",
                table: "Auctions",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReservePrice",
                table: "Auctions",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ViewCount",
                table: "Auctions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AuctionParticipants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuctionId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PaidOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuctionParticipants_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuctionParticipants_Auctions_AuctionId",
                        column: x => x.AuctionId,
                        principalTable: "Auctions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PrivateOffers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuctionId = table.Column<int>(type: "int", nullable: false),
                    BuyerId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrivateOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrivateOffers_AspNetUsers_BuyerId",
                        column: x => x.BuyerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PrivateOffers_Auctions_AuctionId",
                        column: x => x.AuctionId,
                        principalTable: "Auctions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionParticipants_AuctionId",
                table: "AuctionParticipants",
                column: "AuctionId");

            migrationBuilder.CreateIndex(
                name: "IX_AuctionParticipants_UserId",
                table: "AuctionParticipants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PrivateOffers_AuctionId",
                table: "PrivateOffers",
                column: "AuctionId");

            migrationBuilder.CreateIndex(
                name: "IX_PrivateOffers_BuyerId",
                table: "PrivateOffers",
                column: "BuyerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuctionParticipants");

            migrationBuilder.DropTable(
                name: "PrivateOffers");

            migrationBuilder.DropColumn(
                name: "DutchDecrementAmount",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "DutchDecrementIntervalMinutes",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "IsDutchAuction",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "LastDutchDecrement",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "ParticipationFee",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "ReservePrice",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "ViewCount",
                table: "Auctions");
        }
    }
}
