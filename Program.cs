using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using Demtists.Services;
using Serilog;

try
{
    var builder = WebApplication.CreateBuilder(args);

    Console.WriteLine("=== Starting Dentis Application Configuration ===");

    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenAnyIP(80); // Listen on 0.0.0.0:80
    });


    // Serilog Configuration
    builder.Host.UseSerilog((context, configuration) =>
        configuration.ReadFrom.Configuration(context.Configuration));

    // Add services to the container
    builder.Services.AddControllers();

    Console.WriteLine("=== Configuring Database ===");

    // Database
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        Console.WriteLine($"Database connection configured: {!string.IsNullOrEmpty(connectionString)}");
        options.UseSqlServer(connectionString);
    });

    Console.WriteLine("=== Registering Services ===");

    // Services
    builder.Services.AddScoped<ISmsService, SmsSender>();
    builder.Services.AddScoped<IAuthService, AuthService>();

    Console.WriteLine("=== Configuring JWT Authentication ===");

    // JWT Authentication with validation
    var jwtKey = builder.Configuration["Authentication:JwtKey"];
    var jwtIssuer = builder.Configuration["Authentication:JwtIssuer"];
    var jwtAudience = builder.Configuration["Authentication:JwtAudience"];

    // Validate JWT Key
    if (string.IsNullOrEmpty(jwtKey))
    {
        throw new InvalidOperationException("JWT Key is not configured");
    }

    if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
    {
        throw new InvalidOperationException($"JWT Key is too short: {Encoding.UTF8.GetByteCount(jwtKey)} bytes. Minimum required: 32 bytes (256 bits)");
    }

    Console.WriteLine($"JWT Key length: {Encoding.UTF8.GetByteCount(jwtKey)} bytes (Valid: >= 32)");

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

    Console.WriteLine("=== Configuring Swagger ===");

    // Swagger
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Dentists Reservation API",
            Version = "v1",
            Description = "API سیستم رزرو کلینیک دندانپزشکی"
        });

        // JWT Authorization
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement
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

    Console.WriteLine("=== Configuring CORS ===");

    // CORS
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });
    });

    // Add health checks
    builder.Services.AddHealthChecks()
        .AddSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")!);

    Console.WriteLine("=== Building Application ===");

    var app = builder.Build();

    Console.WriteLine("=== Configuring Middleware Pipeline ===");

    // Configure the HTTP request pipeline
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Dentists API V1");
        c.RoutePrefix = "swagger";
    });

    app.UseSerilogRequestLogging();

    // Remove UseHttpsRedirection for production on Liara (they handle SSL)
    // app.UseHttpsRedirection();

    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();

    // Add health check endpoints
    app.MapHealthChecks("/health");
    app.MapHealthChecks("/ready");

    // Add test endpoint
    app.MapGet("/", () => Results.Ok(new
    {
        message = "Dentis API is running!",
        timestamp = DateTime.UtcNow,
        version = "1.0.0"
    }));

    app.MapControllers();

    Console.WriteLine("=== Testing Database Connection ===");

    // Test database connection
    using (var scope = app.Services.CreateScope())
    {
        try
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var canConnect = context.Database.CanConnect();
            Console.WriteLine($"Database connection test: {(canConnect ? "SUCCESS" : "FAILED")}");

            if (!canConnect)
            {
                Console.WriteLine("WARNING: Cannot connect to database, but continuing...");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Database connection error: {ex.Message}");
            Console.WriteLine("WARNING: Database connection failed, but continuing...");
        }
    }

    Console.WriteLine("=== Application configured successfully ===");
    Console.WriteLine($"Environment: {app.Environment.EnvironmentName}");
    Console.WriteLine("=== Starting server ===");

    app.Run();
}
catch (Exception ex)
{ 
    Console.WriteLine("=== CRITICAL ERROR DURING STARTUP ===");
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine($"Inner Exception: {ex.InnerException?.Message}");
    Console.WriteLine($"Stack Trace: {ex.StackTrace}");

    // Log to Serilog if available
    try
    {
        Log.Fatal(ex, "Application terminated unexpectedly");
    }
    catch
    {
        // Ignore if Serilog is not configured
    }
    finally
    {
        Log.CloseAndFlush();
    }

    Environment.Exit(1);
}