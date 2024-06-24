using ai_maestro_proxy.ModelBinders;
using ai_maestro_proxy.Services;
using Newtonsoft.Json;

namespace ai_maestro_proxy
{
    public class Startup(IConfiguration configuration)
    {
        public IConfiguration Configuration { get; } = configuration; public void ConfigureServices(IServiceCollection services)
        {
            // Add logging
            services.AddLogging();

            // Register other services
            string? connectionString = Configuration.GetConnectionString("MariaDb");
            services.AddSingleton(new DatabaseService(connectionString ?? ""));

            RedisConfig? redisConfig = Configuration.GetSection("Redis").Get<RedisConfig>();
            ArgumentNullException.ThrowIfNull(redisConfig);
            services.AddSingleton(new CacheService(redisConfig.ConnectionString));

            services.AddSingleton<GpuManagerService>();
            services.AddSingleton<ProxiedRequestService>();
            services.AddSingleton<RequestHandlerService>();

            // Add controllers with custom model binder provider
            services.AddControllers(options =>
            {
                options.ModelBinderProviders.Insert(0, new RequestModelBinderProvider());
            }).AddNewtonsoftJson(options =>
            {
                options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            });
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
