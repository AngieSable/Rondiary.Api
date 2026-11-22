using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rondiary.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddReplyToEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReplyToEntryId",
                table: "Entries",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReplyToEntryId",
                table: "Entries");
        }
    }
}
