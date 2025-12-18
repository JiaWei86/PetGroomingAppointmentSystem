using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetGroomingAppointmentSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddGiftSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "RedeemGifts",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "RedeemGifts");
        }
    }
}
