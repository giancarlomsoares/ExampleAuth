using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExampleAuth.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClientAccessGrantRevocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_client_access_grants_user_id_application_id",
                table: "client_access_grants");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "revoked_at_utc",
                table: "client_access_grants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "revoked_by_user_id",
                table: "client_access_grants",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_client_access_grants_revoked_by_user_id",
                table: "client_access_grants",
                column: "revoked_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_client_access_grants_user_id_application_id",
                table: "client_access_grants",
                columns: new[] { "user_id", "application_id" },
                unique: true,
                filter: "revoked_at_utc IS NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_client_access_grants_asp_net_users_revoked_by_user_id",
                table: "client_access_grants",
                column: "revoked_by_user_id",
                principalTable: "asp_net_users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_client_access_grants_asp_net_users_revoked_by_user_id",
                table: "client_access_grants");

            migrationBuilder.DropIndex(
                name: "ix_client_access_grants_revoked_by_user_id",
                table: "client_access_grants");

            migrationBuilder.DropIndex(
                name: "ix_client_access_grants_user_id_application_id",
                table: "client_access_grants");

            migrationBuilder.DropColumn(
                name: "revoked_at_utc",
                table: "client_access_grants");

            migrationBuilder.DropColumn(
                name: "revoked_by_user_id",
                table: "client_access_grants");

            migrationBuilder.CreateIndex(
                name: "ix_client_access_grants_user_id_application_id",
                table: "client_access_grants",
                columns: new[] { "user_id", "application_id" },
                unique: true);
        }
    }
}
