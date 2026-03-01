using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuctionHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OptimizationAndRefinements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AuctionId",
                table: "Transactions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDisputed",
                table: "Auctions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSettled",
                table: "Auctions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_AuctionId",
                table: "Transactions",
                column: "AuctionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Auctions_AuctionId",
                table: "Transactions",
                column: "AuctionId",
                principalTable: "Auctions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Auctions_AuctionId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_AuctionId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "AuctionId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "IsDisputed",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "IsSettled",
                table: "Auctions");
        }
    }
}
