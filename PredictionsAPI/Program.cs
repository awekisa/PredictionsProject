using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using PredictionsAPI.Data;
using PredictionsAPI.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "PredictionsAPI", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddPredictionsServices(builder.Configuration);

var corsOrigins = builder.Configuration["CorsOrigins"]?.Split(',') ?? ["http://localhost:5173"];
builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactDev", policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Apply pending migrations automatically
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// Seed roles and admin user
await app.Services.SeedRolesAndAdminAsync(builder.Configuration);

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("ReactDev");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
