using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using cartservice.cartstore;
using cartservice.services;
using OpenTelemetry.Trace;
using OpenTelemetry.Logs;

namespace cartservice
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }
        
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            // using var loggerFactory = LoggerFactory.Create(builder =>
            // {
            //     builder.AddOpenTelemetry(options =>
            //     {
            //         options.AddConsoleExporter();
            //     });
            // });

            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            var loggerFactory = LoggerFactory.Create(logging =>
            {
                logging.Configure(options =>
                {
                    options.ActivityTrackingOptions = ActivityTrackingOptions.SpanId
                                                        | ActivityTrackingOptions.TraceId
                                                        | ActivityTrackingOptions.ParentId
                                                        | ActivityTrackingOptions.Baggage
                                                        | ActivityTrackingOptions.Tags;
                }).AddJsonConsole(options =>
                {
                    options.IncludeScopes = true;
                    options.UseUtcTimestamp = true;
                    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ssZ";
                });
            });

            var logger = loggerFactory.CreateLogger<Startup>();

            logger.LogInformation(eventId: 123, "Hello from {name} {price}.", "tomato", 2.99);

            if (logger.IsEnabled(LogLevel.Debug))
            {
                // If logger.IsEnabled returned false, the code doesn't have to spend time evaluating the arguments.
                // This can be especially helpful if the arguments are expensive to calculate.
                logger.LogDebug(eventId: 501, "System.Environment.Version: {version}.", System.Environment.Version);
            }   
            
            string redisAddress = Configuration["REDIS_ADDR"];
            RedisCartStore cartStore = null;
            if (string.IsNullOrEmpty(redisAddress))
            {
                Console.WriteLine("Redis cache host(hostname+port) was not specified.");
                Console.WriteLine("This sample was modified to showcase OpenTelemetry RedisInstrumentation.");
                Console.WriteLine("REDIS_ADDR environment variable is required.");
                System.Environment.Exit(1);
            }
            cartStore = new RedisCartStore(redisAddress, loggerFactory.CreateLogger<RedisCartStore>());

            // Initialize the redis store
            cartStore.InitializeAsync().GetAwaiter().GetResult();
            Console.WriteLine("Initialization completed");

            services.AddSingleton<ICartStore>(cartStore);

            services.AddOpenTelemetryTracing((builder) => builder
                .AddRedisInstrumentation(
                    cartStore.GetConnection(),
                    options => options.SetVerboseDatabaseStatements = true)
                .AddAspNetCoreInstrumentation()
                .AddGrpcClientInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(opt =>
                    {
                        //opt.Endpoint = new Uri("http://otelcollector:4317");
                        opt.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                    })
                );

            services.AddGrpc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGrpcService<CartService>();
                endpoints.MapGrpcService<cartservice.services.HealthCheckService>();

                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
                });
            });
        }
    }
}