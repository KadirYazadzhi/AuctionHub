using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuctionHub.Migrations
{
    /// <inheritdoc />
    public partial class AddIsSuspendedToAuctions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSuspended",
                table: "Auctions",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSuspended",
                table: "Auctions");
        }
    }
}
