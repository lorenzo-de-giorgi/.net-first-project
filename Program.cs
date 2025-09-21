using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using MyUserApi.Data;
using MyUserApi.Helpers;
using MyUserApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddEndpointsApiExplorer();

// Swagger + supporto Bearer in UI
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "Inserisci 'Bearer {token}'",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement()
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            },
            new List<string>()
        }
    });
});

// Bind JwtSettings
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()!;

// DB: PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Application services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Authentication - JWT Bearer
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // true in produzione
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key))
    };
});

// Authorization (default)
builder.Services.AddAuthorization();

var app = builder.Build();

// Apply migrations at startup (dev convenience)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Authentication / Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Middleware custom che richiede auth per tutte le /api/** tranne register/login e swagger
app.Use(async (context, next) =>
{
    var logger = app.Logger;
    var path = context.Request.Path;
    var publicPaths = new[]
    {
        "/api/users/register",
        "/api/users/login",
        "/swagger",
        "/swagger/index.html",
        "/swagger/v1/swagger.json",
        "/swagger/swagger.json",
        "/swagger/oauth2-redirect.html",
        "/" // keep root public
    };

    // Se non è una API o è un path pubblico -> allow
    if (!path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) ||
        publicPaths.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase)))
    {
        logger.LogDebug("Public path allow: {Path}", path);
        await next();
        return;
    }

    // Forziamo l'authentication per assicurarci che il JWT handler venga eseguito
    var authResult = await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
    if (authResult?.Succeeded == true && authResult.Principal != null)
    {
        context.User = authResult.Principal;
        logger.LogDebug("AuthenticateAsync succeeded for {Path}. IsAuthenticated={IsAuth}", path, context.User.Identity?.IsAuthenticated);
    }
    else
    {
        var failureMsg = authResult?.Failure?.Message ?? "no failure message";
        logger.LogDebug("AuthenticateAsync did NOT produce principal for {Path}. Success={Success}. Failure={Failure}", path, authResult?.Succeeded, failureMsg);
    }

    // Ora controlliamo lo stato dell'utente
    if (context.User?.Identity != null && context.User.Identity.IsAuthenticated)
    {
        await next();
        return;
    }

    // Non autenticato -> 401
    logger.LogInformation("Unauthenticated request blocked: {Method} {Path}", context.Request.Method, path);
    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    await context.Response.WriteAsJsonAsync(new { error = "Unauthorized" });
});

// Root
app.MapGet("/", () => Results.Ok(new { message = "MyUserApi running" }));

// Register -> restituisce user + token
app.MapPost("/api/users/register", async (UserCreateDto dto, IUserService svc, IAuthService auth) =>
{
    var created = await svc.CreateAsync(dto);
    if (created is null) return Results.Conflict(new { error = "Email already in use" });

    var token = auth.GenerateToken(created);
    return Results.Created($"/api/users/{created.Id}", new { user = created, token });
});

// Login -> restituisce user + token
app.MapPost("/api/users/login", async (UserLoginDto dto, IUserService svc, IAuthService auth) =>
{
    var user = await svc.ValidateCredentialsAsync(dto.Email, dto.Password);
    if (user is null) return Results.Unauthorized();

    var token = auth.GenerateToken(user);
    return Results.Ok(new { user, token });
});

// Get all (protetto dal middleware)
app.MapGet("/api/users", async (IUserService svc) => Results.Ok(await svc.GetAllAsync()));

// Get by id (protetto)
app.MapGet("/api/users/{id:int}", async (int id, IUserService svc) =>
{
    var user = await svc.GetByIdAsync(id);
    return user is null ? Results.NotFound() : Results.Ok(user);
});

app.Run();
