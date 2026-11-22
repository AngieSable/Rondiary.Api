using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rondiary.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAlbumId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AlbumId",
                table: "Entries",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlbumId",
                table: "Entries");
        }
    }
}
