using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Personal_Blog_Application.Migrations
{
    /// <inheritdoc />
    public partial class UpdateBlogsAttributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Blogs",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Blogs");
        }
    }
}
