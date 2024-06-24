using ai_maestro_proxy.Services;
using Newtonsoft.Json;

namespace ai_maestro_proxy
{
    public class Startup(IConfiguration configuration)
    {
        public IConfiguration Configuration { get; } = configuration;

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers().AddNewtonsoftJson(options =>
            {
                options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            });

            services.AddHttpClient();

            // Register DatabaseService
            string? connectionString = Configuration.GetConnectionString("MariaDb");
            services.AddSingleton(new DatabaseService(connectionString ?? ""));

            // Register CacheService
            RedisConfig? redisConfig = Configuration.GetSection("Redis").Get<RedisConfig>();
            ArgumentNullException.ThrowIfNull(redisConfig);
            services.AddSingleton(new CacheService(redisConfig.ConnectionString));

            // Register additional services
            services.AddSingleton<GpuManagerService>();
            services.AddSingleton<ProxiedRequestService>();
            services.AddSingleton<RequestHandlerService>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        public class RedisConfig
        {
            public required string ConnectionString { get; set; }
        }
    }
}
