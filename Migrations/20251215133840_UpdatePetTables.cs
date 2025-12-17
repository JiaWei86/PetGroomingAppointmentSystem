using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetGroomingAppointmentSystem.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePetTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID('ResetPassword', 'U') IS NOT NULL
                BEGIN
                    ALTER TABLE [ResetPassword] DROP CONSTRAINT [FK_ResetPassword_Customers_CustomerId];
                    DROP TABLE [ResetPassword];
                END
            ");

            migrationBuilder.AddColumn<string>(
                name: "PetType",
                table: "ServiceCategories",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PetType",
                table: "ServiceCategories");
        }
    }
}
