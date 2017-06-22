using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using System;

namespace WebSample
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            // Read the appsetting.json file for the configuration details
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add framework services.
            services.AddMvc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            // Create a logging provider based on the configuration information passed through the appsettings.json
            // You can even provide your custom formatting.
            loggerFactory.AddAWSProvider(this.Configuration.GetAWSLoggingConfigSection(), 
                formatter: (logLevel, message, exception) => $"[{DateTime.UtcNow}] {logLevel}: {message}");

            // Create a logger instance from the loggerFactory
            var logger = loggerFactory.CreateLogger<Program>();

            // Example Logging
            logger.LogInformation("Check the AWS Console CloudWatch Logs console in us-east-1");
            logger.LogInformation("to see messages in the log streams for the");
            logger.LogInformation("log group AspNetCore.WebSample");


            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseBrowserLink();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
