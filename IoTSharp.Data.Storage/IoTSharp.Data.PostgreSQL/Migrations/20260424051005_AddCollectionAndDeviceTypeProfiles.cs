using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IoTSharp.Data.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionAndDeviceTypeProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DeviceTypeProfileId",
                table: "Device",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HvacDeviceType",
                table: "Device",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CollectionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GatewayDeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    PointId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RequestAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequestFrame = table.Column<string>(type: "text", nullable: true),
                    ResponseAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResponseFrame = table.Column<string>(type: "text", nullable: true),
                    ParsedValue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ConvertedValue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    DurationMs = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CollectionTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    GatewayDeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Protocol = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Modbus"),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ConnectionJson = table.Column<string>(type: "text", nullable: true),
                    ReportPolicyJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectionTasks_Device_GatewayDeviceId",
                        column: x => x.GatewayDeviceId,
                        principalTable: "Device",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeviceTypeProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProfileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DeviceType = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Icon = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceTypeProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProduceDataMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProduceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProduceKeyName = table.Column<string>(type: "text", nullable: true),
                    DataCatalog = table.Column<int>(type: "integer", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceKeyName = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProduceDataMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProduceDataMappings_Produces_ProduceId",
                        column: x => x.ProduceId,
                        principalTable: "Produces",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CollectionDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DeviceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SlaveId = table.Column<byte>(type: "smallint", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ProtocolOptionsJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectionDevices_CollectionTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "CollectionTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CollectionRuleTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    PointKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PointName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    FunctionCode = table.Column<byte>(type: "smallint", nullable: false),
                    Address = table.Column<int>(type: "integer", nullable: false),
                    RegisterCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    RawDataType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "uint16"),
                    ByteOrder = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true, defaultValue: "AB"),
                    WordOrder = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true, defaultValue: "AB"),
                    ReadPeriodMs = table.Column<int>(type: "integer", nullable: false, defaultValue: 30000),
                    PollingGroup = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TransformsJson = table.Column<string>(type: "text", nullable: true),
                    TargetName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TargetType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, defaultValue: "Telemetry"),
                    TargetValueType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, defaultValue: "Double"),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    GroupName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionRuleTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectionRuleTemplates_DeviceTypeProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "DeviceTypeProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CollectionPoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PointKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PointName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FunctionCode = table.Column<byte>(type: "smallint", nullable: false),
                    Address = table.Column<int>(type: "integer", nullable: false),
                    RegisterCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    RawDataType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "uint16"),
                    ByteOrder = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true, defaultValue: "AB"),
                    WordOrder = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true, defaultValue: "AB"),
                    ReadPeriodMs = table.Column<int>(type: "integer", nullable: false, defaultValue: 30000),
                    PollingGroup = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TransformsJson = table.Column<string>(type: "text", nullable: true),
                    TargetDeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TargetType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, defaultValue: "Telemetry"),
                    TargetValueType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, defaultValue: "Double"),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    GroupName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionPoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectionPoints_CollectionDevices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "CollectionDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CollectionPoints_Device_TargetDeviceId",
                        column: x => x.TargetDeviceId,
                        principalTable: "Device",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Device_DeviceTypeProfileId",
                table: "Device",
                column: "DeviceTypeProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionDevices_SlaveId",
                table: "CollectionDevices",
                column: "SlaveId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionDevices_TaskId",
                table: "CollectionDevices",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionDevices_TaskId_SlaveId",
                table: "CollectionDevices",
                columns: new[] { "TaskId", "SlaveId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollectionLogs_CreatedAt",
                table: "CollectionLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionLogs_GatewayDeviceId",
                table: "CollectionLogs",
                column: "GatewayDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionLogs_GatewayDeviceId_CreatedAt",
                table: "CollectionLogs",
                columns: new[] { "GatewayDeviceId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionLogs_RequestId",
                table: "CollectionLogs",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionLogs_Status",
                table: "CollectionLogs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionPoints_DeviceId",
                table: "CollectionPoints",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionPoints_DeviceId_Address_FunctionCode",
                table: "CollectionPoints",
                columns: new[] { "DeviceId", "Address", "FunctionCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollectionPoints_TargetDeviceId",
                table: "CollectionPoints",
                column: "TargetDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionRuleTemplates_ProfileId",
                table: "CollectionRuleTemplates",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionTasks_Enabled",
                table: "CollectionTasks",
                column: "Enabled");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionTasks_GatewayDeviceId",
                table: "CollectionTasks",
                column: "GatewayDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionTasks_TaskKey",
                table: "CollectionTasks",
                column: "TaskKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceTypeProfiles_DeviceType",
                table: "DeviceTypeProfiles",
                column: "DeviceType");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceTypeProfiles_Enabled",
                table: "DeviceTypeProfiles",
                column: "Enabled");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceTypeProfiles_ProfileKey",
                table: "DeviceTypeProfiles",
                column: "ProfileKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProduceDataMappings_ProduceId",
                table: "ProduceDataMappings",
                column: "ProduceId");

            migrationBuilder.AddForeignKey(
                name: "FK_Device_DeviceTypeProfiles_DeviceTypeProfileId",
                table: "Device",
                column: "DeviceTypeProfileId",
                principalTable: "DeviceTypeProfiles",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Device_DeviceTypeProfiles_DeviceTypeProfileId",
                table: "Device");

            migrationBuilder.DropTable(
                name: "CollectionLogs");

            migrationBuilder.DropTable(
                name: "CollectionPoints");

            migrationBuilder.DropTable(
                name: "CollectionRuleTemplates");

            migrationBuilder.DropTable(
                name: "ProduceDataMappings");

            migrationBuilder.DropTable(
                name: "CollectionDevices");

            migrationBuilder.DropTable(
                name: "DeviceTypeProfiles");

            migrationBuilder.DropTable(
                name: "CollectionTasks");

            migrationBuilder.DropIndex(
                name: "IX_Device_DeviceTypeProfileId",
                table: "Device");

            migrationBuilder.DropColumn(
                name: "DeviceTypeProfileId",
                table: "Device");

            migrationBuilder.DropColumn(
                name: "HvacDeviceType",
                table: "Device");
        }
    }
}
