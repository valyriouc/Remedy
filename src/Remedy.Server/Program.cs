using Remedy.Shared.Data;

namespace Remedy.Server;

public class Program
{
    public static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        // Add DbContext
        builder.Services.AddDbContext<RemedyDbContext>();

        // Add controllers and Swagger
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        // Add CORS for local development
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });

        WebApplication app = builder.Build();

        // Ensure database is created
        using (var scope = app.Services.CreateScope())
        {
            RemedyDbContext db = scope.ServiceProvider.GetRequiredService<RemedyDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        // Configure middleware
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseCors();
        app.MapControllers();

        await app.RunAsync();
    }
}