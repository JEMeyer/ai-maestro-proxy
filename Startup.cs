using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

            var connectionString = Configuration.GetConnectionString("MariaDb");
            services.AddSingleton(new DatabaseService(connectionString));

            var redisConfig = Configuration.GetSection("Redis").Get<RedisConfig>();
            services.AddSingleton(new CacheService(redisConfig.ConnectionString));
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
