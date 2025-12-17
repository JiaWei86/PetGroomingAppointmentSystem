using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetGroomingAppointmentSystem.Migrations
{
    public partial class FixServiceCategoryRelation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
                          WHERE TABLE_NAME='Services' AND COLUMN_NAME='CategoryId')
                BEGIN
                    ALTER TABLE [Services] DROP COLUMN [CategoryId];
                END
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
                              WHERE TABLE_NAME='Services' AND COLUMN_NAME='CategoryId')
                BEGIN
                    ALTER TABLE [Services] ADD [CategoryId] nvarchar(10) NULL;
                END
            ");
        }
    }
}
