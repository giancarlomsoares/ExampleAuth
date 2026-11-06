using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExampleAuth.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserDisabling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "disabled_at_utc",
                table: "asp_net_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "disabled_by_user_id",
                table: "asp_net_users",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "disabled_at_utc",
                table: "asp_net_users");

            migrationBuilder.DropColumn(
                name: "disabled_by_user_id",
                table: "asp_net_users");
        }
    }
}
