using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BadgeCraft_Net.Migrations
{
    /// <inheritdoc />
    public partial class SetDefaultIsGrantedTrue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Users SET IsGranted = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
