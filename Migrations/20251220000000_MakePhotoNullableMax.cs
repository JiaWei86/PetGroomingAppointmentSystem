// Generated migration: make Photo nvarchar(max)
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetGroomingAppointmentSystem.Migrations
{
 public partial class MakePhotoNullableMax : Migration
 {
 protected override void Up(MigrationBuilder migrationBuilder)
 {
 migrationBuilder.AlterColumn<string>(
 name: "Photo",
 table: "Users",
 type: "nvarchar(max)",
 nullable: true,
 oldClrType: typeof(string),
 oldType: "nvarchar(300)",
 oldMaxLength:300,
 oldNullable: true);
 }

 protected override void Down(MigrationBuilder migrationBuilder)
 {
 migrationBuilder.AlterColumn<string>(
 name: "Photo",
 table: "Users",
 type: "nvarchar(300)",
 maxLength:300,
 nullable: true,
 oldClrType: typeof(string),
 oldType: "nvarchar(max)",
 oldNullable: true);
 }
 }
}
