using Core.Extensions;
using Microsoft.OpenApi.Models;

namespace PrivateApi;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddDefaultAWSOptions(Configuration.GetAWSOptions());

        services.AddEfficientDynamoDb();
        services.RegisterDataServices();

        services.AddControllers();

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "DRCS Private API", Version = "v1" });
            c.EnableAnnotations();
        });

        services.AddCors(options => options.AddDefaultPolicy(builder =>
        {
            builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
        }));
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        // API Gateway routes /private/* to this Lambda; strip the prefix for routing.
        if (Environment.GetEnvironmentVariable("LAMBDA_TASK_ROOT") is not null)
        {
            app.UsePathBase("/private");
        }

        app.UseCors();
        app.UseRouting();

        app.UseSwagger(c => c.RouteTemplate = "swagger/{documentName}/swagger.json");
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("v1/swagger.json", "DRCS Private API V1");
            c.RoutePrefix = "swagger";
        });

        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapGet("/", async ctx => await ctx.Response.WriteAsync("DRCS Private API"));
        });
    }
}
