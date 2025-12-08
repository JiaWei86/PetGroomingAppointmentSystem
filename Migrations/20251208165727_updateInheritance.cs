using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetGroomingAppointmentSystem.Migrations
{
    /// <inheritdoc />
    public partial class updateInheritance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Admins_Users_AdminId",
                table: "Admins");

            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_Users_UserId",
                table: "Appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Users_CustomerId",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK_Staffs_Admins_AdminId",
                table: "Staffs");

            migrationBuilder.DropForeignKey(
                name: "FK_Staffs_Users_StaffId",
                table: "Staffs");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_UserId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Appointments");

            migrationBuilder.RenameColumn(
                name: "AdminId",
                table: "Staffs",
                newName: "AdminUserId");

            migrationBuilder.RenameColumn(
                name: "StaffId",
                table: "Staffs",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_Staffs_AdminId",
                table: "Staffs",
                newName: "IX_Staffs_AdminUserId");

            migrationBuilder.RenameColumn(
                name: "CustomerId",
                table: "Customers",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "AdminId",
                table: "Admins",
                newName: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Admins_Users_UserId",
                table: "Admins",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Users_UserId",
                table: "Customers",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Staffs_Admins_AdminUserId",
                table: "Staffs",
                column: "AdminUserId",
                principalTable: "Admins",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Staffs_Users_UserId",
                table: "Staffs",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Admins_Users_UserId",
                table: "Admins");

            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Users_UserId",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK_Staffs_Admins_AdminUserId",
                table: "Staffs");

            migrationBuilder.DropForeignKey(
                name: "FK_Staffs_Users_UserId",
                table: "Staffs");

            migrationBuilder.RenameColumn(
                name: "AdminUserId",
                table: "Staffs",
                newName: "AdminId");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "Staffs",
                newName: "StaffId");

            migrationBuilder.RenameIndex(
                name: "IX_Staffs_AdminUserId",
                table: "Staffs",
                newName: "IX_Staffs_AdminId");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "Customers",
                newName: "CustomerId");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "Admins",
                newName: "AdminId");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Appointments",
                type: "nvarchar(10)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_UserId",
                table: "Appointments",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Admins_Users_AdminId",
                table: "Admins",
                column: "AdminId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_Users_UserId",
                table: "Appointments",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Users_CustomerId",
                table: "Customers",
                column: "CustomerId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Staffs_Admins_AdminId",
                table: "Staffs",
                column: "AdminId",
                principalTable: "Admins",
                principalColumn: "AdminId");

            migrationBuilder.AddForeignKey(
                name: "FK_Staffs_Users_StaffId",
                table: "Staffs",
                column: "StaffId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
