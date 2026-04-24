using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IoTSharp.Data.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyDeviceModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceModelCommands");

            migrationBuilder.DropTable(
                name: "DeviceModels");

            migrationBuilder.DropColumn(
                name: "DeviceModelId",
                table: "Device");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DeviceModelId",
                table: "Device",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DeviceModels",
                columns: table => new
                {
                    DeviceModelId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreateDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Creator = table.Column<Guid>(type: "uuid", nullable: false),
                    ModelDesc = table.Column<string>(type: "text", nullable: true),
                    ModelName = table.Column<string>(type: "text", nullable: true),
                    ModelStatus = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceModels", x => x.DeviceModelId);
                });

            migrationBuilder.CreateTable(
                name: "DeviceModelCommands",
                columns: table => new
                {
                    CommandId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceModelId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommandI18N = table.Column<string>(type: "text", nullable: true),
                    CommandName = table.Column<string>(type: "text", nullable: true),
                    CommandParams = table.Column<string>(type: "text", nullable: true),
                    CommandStatus = table.Column<int>(type: "integer", nullable: false),
                    CommandTemplate = table.Column<string>(type: "text", nullable: true),
                    CommandTitle = table.Column<string>(type: "text", nullable: true),
                    CommandType = table.Column<int>(type: "integer", nullable: false),
                    CreateDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Creator = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceModelCommands", x => x.CommandId);
                    table.ForeignKey(
                        name: "FK_DeviceModelCommands_DeviceModels_DeviceModelId",
                        column: x => x.DeviceModelId,
                        principalTable: "DeviceModels",
                        principalColumn: "DeviceModelId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceModelCommands_DeviceModelId",
                table: "DeviceModelCommands",
                column: "DeviceModelId");
        }
    }
}
