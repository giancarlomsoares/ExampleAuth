using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExampleAuth.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserCreationAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at_utc",
                table: "asp_net_users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "created_by_user_id",
                table: "asp_net_users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_users_created_by_user_id",
                table: "asp_net_users",
                column: "created_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_asp_net_users_asp_net_users_created_by_user_id",
                table: "asp_net_users",
                column: "created_by_user_id",
                principalTable: "asp_net_users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_asp_net_users_asp_net_users_created_by_user_id",
                table: "asp_net_users");

            migrationBuilder.DropIndex(
                name: "ix_asp_net_users_created_by_user_id",
                table: "asp_net_users");

            migrationBuilder.DropColumn(
                name: "created_at_utc",
                table: "asp_net_users");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                table: "asp_net_users");
        }
    }
}
