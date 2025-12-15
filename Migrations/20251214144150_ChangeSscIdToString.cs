using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetGroomingAppointmentSystem.Migrations
{
    /// <inheritdoc />
    public partial class ChangeSscIdToString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ServiceServiceCategories",
                table: "ServiceServiceCategories");

            migrationBuilder.DropColumn(
                name: "SscId",
                table: "ServiceServiceCategories");

            migrationBuilder.AddColumn<string>(
                name: "SscId",
                table: "ServiceServiceCategories",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ServiceServiceCategories",
                table: "ServiceServiceCategories",
                column: "SscId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ServiceServiceCategories",
                table: "ServiceServiceCategories");

            migrationBuilder.DropColumn(
                name: "SscId",
                table: "ServiceServiceCategories");

            migrationBuilder.AddColumn<int>(
                name: "SscId",
                table: "ServiceServiceCategories",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ServiceServiceCategories",
                table: "ServiceServiceCategories",
                column: "SscId");
        }
    }
}
