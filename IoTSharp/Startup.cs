using EasyCaching.Core.Configurations;
using HealthChecks.UI.Client;
using IoTSharp.Contracts;
using IoTSharp.Data;
using IoTSharp.Data.Extensions;
using IoTSharp.Data.TimeSeries;
using IoTSharp.EventBus;
using IoTSharp.EventBus.CAP;
using IoTSharp.FlowRuleEngine;
using IoTSharp.Gateways;
using IoTSharp.Interpreter;
using IoTSharp.McpTools;
using IoTSharp.Extensions;
using IoTSharp.Services;
using IoTSharp.TaskActions;
using Jdenticon.AspNetCore;
using Jdenticon.Rendering;
using LettuceEncrypt;
using LettuceEncrypt.Dns.Ali;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.Server;
using MQTTnet.AspNetCore;
using Newtonsoft.Json.Serialization;
using Quartz;
using Quartz.AspNetCore;
using Storage.Net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace IoTSharp
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; private set; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            System.Text.Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var settings = Configuration.Get<AppSettings>();
            services.Configure((Action<AppSettings>)(setting =>
            {
                var option = setting.MqttBroker;
                Configuration.Bind(setting);
            }));
            var healthChecksUI = services.AddHealthChecksUI(setup =>
            {
                setup.SetHeaderText("IoTSharp HealthChecks");
                //Maximum history entries by endpoint
                setup.MaximumHistoryEntriesPerEndpoint(50);
                setup.AddIoTSharpHealthCheckEndpoint();
            });

            var healthChecks = services.AddHealthChecks()
                .AddDiskStorageHealthCheck(dso =>
                {
                    System.IO.DriveInfo.GetDrives()
                        .Where(d => d.DriveType == System.IO.DriveType.Fixed && d.DriveFormat != "overlay" && !d.Name.StartsWith("/sys"))
                        .Select(f => f.Name).Distinct().ToList()
                        .ForEach(f => dso.AddDrive(f));
                }, name: "Disk Storage");

            ValidateRuntimeSettings(settings);
            services.ConfigureNpgsql(Configuration.GetConnectionString("IoTSharp"), settings.DbContextPoolSize, healthChecks, healthChecksUI);
            services.AddDatabaseDeveloperPageExceptionFilter();
            services.AddIdentity<IdentityUser, IdentityRole>()
                .AddRoles<IdentityRole>()
                .AddRoleManager<RoleManager<IdentityRole>>()
                .AddDefaultTokenProviders()
                .AddEntityFrameworkStores<ApplicationDbContext>();





            services.AddAuthentication(option =>
            {
                option.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                option.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                option.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(options =>
            {
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = Configuration["JwtIssuer"],
                    ValidAudience = Configuration["JwtAudience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuration["JwtKey"])),
                    //     ClockSkew=TimeSpan.Zero //JWT的缓冲时间默认5分钟，token实际过期时间为 appsettings.json 当中JwtExpireHours配置的时间（小时）加上这个时间。
                }; ;
            });

            services.AddCors();
            services.AddLogging(loggingBuilder =>
                {
                    loggingBuilder.AddRinLogger();
                    loggingBuilder.AddPrettyConsole();
                }
            );
            services.AddRin();
            services.AddOpenApiDocument(configure =>
            {
                Assembly assembly = typeof(Startup).GetTypeInfo().Assembly;
                var description = (AssemblyDescriptionAttribute)Attribute.GetCustomAttribute(assembly, typeof(AssemblyDescriptionAttribute));
                configure.Title = typeof(Startup).GetTypeInfo().Assembly.GetName().Name;
                configure.Version = typeof(Startup).GetTypeInfo().Assembly.GetName().Version.ToString();
                configure.Description = description?.Description;
                configure.AddJWTSecurity();
            });

            services.AddTransient<ApplicationDBInitializer>();
            services.AddIoTSharpMqttServer(settings.MqttBroker);
            services.AddMqttClient(settings.MqttClient);
            services.AddQuartz(q =>
            {
                q.DiscoverJobs();
            });

            // ASP.NET Core hosting
            services.AddQuartzServer(options =>
            {
                options.StartDelay = TimeSpan.FromSeconds(10);
                // when shutting down we want jobs to complete gracefully
                options.WaitForJobsToComplete = true;
            });
            services.AddResponseCompression();
            services.AddControllers();

            services.AddMemoryCache();
            string _hc_Caching = $"{nameof(CachingUseIn)}-{Enum.GetName(settings.CachingUseIn)}";
            services.AddEasyCaching(options =>
            {
                switch (settings.CachingUseIn)
                {
                    case CachingUseIn.Redis:
                        options.UseRedis(config =>
                        {
                            settings.CachingUseRedisHosts?.Split(';').ToList().ForEach(h =>
                            {
                                var hx = h.Split(':');
                                config.DBConfig.Endpoints.Add(new ServerEndPoint(hx[0], int.Parse(hx[1])));
                            });
                            config.SerializerName = "json";
                        }, _hc_Caching);
                        options.WithJson("json");
                        break;

                    case CachingUseIn.LiteDB:
                        options.UseLiteDB(cfg => cfg.DBConfig = new EasyCaching.LiteDB.LiteDBDBOptions() { }, name: _hc_Caching);
                        break;

                    case CachingUseIn.InMemory:
                    default:
                        options.UseInMemory(_hc_Caching);
                        break;
                }
            });
            services.AddTelemetryStorage(settings, healthChecks);
            services.AddEventBus(opt =>
            {
                opt.AppSettings = settings;
                opt.EventBusStore = Configuration.GetConnectionString("EventBusStore");
                opt.EventBusMQ = Configuration.GetConnectionString("EventBusMQ");
                opt.HealthChecks = healthChecks;
                opt.UserCAP();
            });
            services.AddHostedService<CoAPService>();
            services.AddScoped<CollectionTaskService>();
            services.AddScoped<DeviceTypeProfileService>();
            services.AddModbusCollection();
            services.AddTransient(_ => StorageFactory.Blobs.FromConnectionString(Configuration.GetConnectionString("BlobStorage") ?? $"disk://path={Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.Create)}/IoTSharp/"));

            services.AddControllers().AddNewtonsoftJson(options =>
            {
                options.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            });
            services.AddRazorPages();

            services.AddScriptEngines(Configuration.GetSection("EngineSetting"));
            services.AddTransient<FlowRuleProcessor>();
            services.AddTransient<CustomeAlarmPullExcutor>();
            services.AddSingleton<TaskExecutorHelper>();
            services.AddTransient<PublishAttributeDataTask>();
            services.AddTransient<PublishTelemetryDataTask>();
            services.AddTransient<PublishAlarmDataTask>();
            services.AddTransient<RawDataGateway>();
            services.AddTransient<KepServerEx>();

            if (Environment.GetEnvironmentVariable("IOTSHARP_ACME") == "true")
            {
                services.AddLettuceEncrypt()
                        .PersistDataToDirectory(new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "security")), "kissme")
                        .Services.AddAliDnsChallengeProvider();
                services.AddHsts(options =>
                {
                    options.Preload = true;
                    options.IncludeSubDomains = true;
                    options.MaxAge = TimeSpan.FromDays(60);
                });
            }

            services.AddMcpServer()
             .WithHttpTransport(options =>
             {
                 options.ConfigureSessionOptions += async (context, serverOptions, token) =>
                 {
                     var api_key = context.Request.RouteValues["api_key"]?.ToString()?.ToLower() ?? "none";
                     serverOptions.InitializationTimeout = TimeSpan.FromSeconds(600);
                     serverOptions.Capabilities!.Experimental = new Dictionary<string, object>();
                     serverOptions.Capabilities.Experimental.Add("API_KEY", api_key);
                     await Task.CompletedTask;
                 };

                 options.Stateless = true;
             })
             .WithPromptsFromAssembly()
             .WithResourcesFromAssembly()
                .WithToolsFromAssembly();
        }

        private static void ValidateRuntimeSettings(AppSettings settings)
        {
            if (settings.DataBase != DataBaseType.PostgreSql)
            {
                throw new NotSupportedException($"当前仅支持关系数据库 PostgreSql，收到配置值: {settings.DataBase}");
            }

            if (settings.EventBus != EventBusFramework.CAP)
            {
                throw new NotSupportedException($"当前仅支持事件总线框架 CAP，收到配置值: {settings.EventBus}");
            }

            if (settings.EventBusStore != EventBusStore.PostgreSql)
            {
                throw new NotSupportedException($"当前仅支持事件总线存储 PostgreSql，收到配置值: {settings.EventBusStore}");
            }

            if (settings.EventBusMQ != EventBusMQ.RabbitMQ)
            {
                throw new NotSupportedException($"当前仅支持消息中间件 RabbitMQ，收到配置值: {settings.EventBusMQ}");
            }

            if (settings.TelemetryStorage != TelemetryStorage.TimescaleDB && settings.TelemetryStorage != TelemetryStorage.InfluxDB)
            {
                throw new NotSupportedException($"当前仅支持时序存储 TimescaleDB 或 InfluxDB，收到配置值: {settings.TelemetryStorage}");
            }
        }





        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {

            if (env.IsDevelopment() || !env.IsEnvironment("Production"))
            {
                app.UseRin();
                app.UseRinMvcSupport();
                app.UseDeveloperExceptionPage();
                app.UseRinDiagnosticsHandler();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.CheckApplicationDBMigrations();
            //添加定时任务创建表

            app.UseRouting();
            app.UseCors(option => option
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader());
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseResponseCompression();
            app.UseIotSharpMqttServer();
            app.UseSwaggerUi();
            app.UseHealthChecksUI();
            app.UseOpenApi();
            app.UseEventBus(opt =>
            {
                var frp = app.ApplicationServices.GetService<FlowRuleProcessor>();
                return frp.RunRules;
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapMqtt("/mqtt");
                endpoints.MapHealthChecks("/healthz", new HealthCheckOptions()
                {
                    Predicate = _ => true,
                    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
                });
                endpoints.MapHealthChecksUI();
                endpoints.MapDefaultControllerRoute();
                endpoints.MapRazorPages();
                endpoints.MapMcp("/mcp/{api_key}");
            });

            app.UseJdenticon(defaultStyle =>
            {
                // Custom identicon style
                // https://jdenticon.com/icon-designer.html?config=8644440010c4330a24461852
                defaultStyle.Hues = new HueCollection { { 196, HueUnit.Degrees } };
                defaultStyle.BackColor = Color.FromRgba(134, 68, 68, 0);
                defaultStyle.ColorLightness = Jdenticon.Range.Create(0.36f, 0.70f);
                defaultStyle.GrayscaleLightness = Jdenticon.Range.Create(0.24f, 0.82f);
                defaultStyle.ColorSaturation = 0.51f;
                defaultStyle.GrayscaleSaturation = 0.10f;
            });
            app.UseTelemetryStorage();



            var provider = new FileExtensionContentTypeProvider();
            provider.Mappings[".fbx"] = "application/octet-stream";
            provider.Mappings[".glb"] = "application/octet-stream";
            app.UseStaticFiles(new StaticFileOptions
            {
                ContentTypeProvider = provider,
            });
        }
    }
}
