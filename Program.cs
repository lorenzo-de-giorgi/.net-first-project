using Microsoft.EntityFrameworkCore;
using MyUserApi.Data;
using MyUserApi.Models;
using MyUserApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// DB: PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Application services
builder.Services.AddScoped<IUserService, UserService>();

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

app.MapGet("/", () => Results.Ok(new { message = "MyUserApi running" }));

// Register
app.MapPost("/api/users/register", async (UserCreateDto dto, IUserService svc) =>
{
    var created = await svc.CreateAsync(dto);
    if (created is null) return Results.Conflict(new { error = "Email already in use" });
    return Results.Created($"/api/users/{created.Id}", created);
});

// Login
app.MapPost("/api/users/login", async (UserLoginDto dto, IUserService svc) =>
{
    var user = await svc.ValidateCredentialsAsync(dto.Email, dto.Password);
    return user is null ? Results.Unauthorized() : Results.Ok(user);
});

// Get all (no password)
app.MapGet("/api/users", async (IUserService svc) => Results.Ok(await svc.GetAllAsync()));

// Get by id
app.MapGet("/api/users/{id:int}", async (int id, IUserService svc) =>
{
    var user = await svc.GetByIdAsync(id);
    return user is null ? Results.NotFound() : Results.Ok(user);
});

app.Run();
