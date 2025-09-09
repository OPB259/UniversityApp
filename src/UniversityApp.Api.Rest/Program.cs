using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

using UniversityApp.Domain.Entities;
using UniversityApp.Infrastructure;
using UniversityApp.Infrastructure.Data;
using UniversityApp.Api.Rest;             // DTO + JwtOptions + WebApiUser
using UniversityApp.Api.Rest.Services;    // IUsersRepository

var builder = WebApplication.CreateBuilder(args);

// EF InMemory
builder.Services.AddInfrastructure();

// Repo u¿ytkowników + bindowanie opcji JWT
builder.Services.AddSingleton<IUsersRepository, UsersRepository>();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

// JWT auth + DIAGNOSTYKA
builder.Services
    .AddAuthentication(o =>
    {
        o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(o =>
    {
        var cfg = builder.Configuration;

        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = cfg["Jwt:Issuer"],
            ValidAudience = cfg["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!)),
            // Daj 2 minuty marginesu czasu na ewentualny drift zegara
            ClockSkew = TimeSpan.FromMinutes(2)
        };

        // Bardzo czytelne logi DLACZEGO 401
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var hasAuth = ctx.Request.Headers.ContainsKey("Authorization");
                Console.WriteLine($"[JWT] OnMessageReceived: Authorization header present? {hasAuth}");
                if (hasAuth)
                {
                    var auth = ctx.Request.Headers["Authorization"].ToString();
                    Console.WriteLine($"[JWT] Authorization header: {auth[..Math.Min(auth.Length, 60)]}...");
                }
                return Task.CompletedTask;
            },
            OnTokenValidated = ctx =>
            {
                var principal = ctx.Principal!;
                var name = principal.Identity?.Name ?? "(no name)";
                var role = principal.FindFirstValue(ClaimTypes.Role) ?? "(no role)";
                Console.WriteLine($"[JWT] Token VALID. Name={name}, Role={role}");
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = ctx =>
            {
                Console.WriteLine($"[JWT] AuthenticationFailed: {ctx.Exception.GetType().Name} - {ctx.Exception.Message}");
                return Task.CompletedTask;
            },
            OnChallenge = ctx =>
            {
                // To siê odpala przy 401 – poka¿e np. „invalid_token”, „The signature is invalid”, itp.
                Console.WriteLine($"[JWT] Challenge: error={ctx.Error}, desc={ctx.ErrorDescription}");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// Swagger (+Bearer w³aœciwy, bez rêcznego wpisywania „Bearer ” w polu)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "UniversityApp.Api.Rest", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Name = "Authorization",
        Description = "Kliknij Authorize i wklej **sam** token (bez s³owa Bearer)."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Prosty logger nag³ówków (pomocny)
app.Use(async (context, next) =>
{
    Console.WriteLine($"[{DateTime.UtcNow:O}] {context.Request.Method} {context.Request.Path}");
    foreach (var h in context.Request.Headers)
        if (h.Key is "Authorization" or "X-Correlation-Id")
            Console.WriteLine($"H {h.Key}: {h.Value}");
    await next();
});

app.UseAuthentication();
app.UseAuthorization();

var api = app.MapGroup("/api");

// ===== SECURITY: token (UWAGA: **Token** z wielkiej litery) =====
api.MapPost("/security/generatetoken", (WebApiUser user, IUsersRepository repo, IConfiguration cfg) =>
{
    if (!repo.AuthorizeUser(user.Username, user.Password))
        return Results.Unauthorized();

    var claims = new[]
    {
        new Claim(JwtRegisteredClaimNames.Sub, user.Username),
        new Claim(ClaimTypes.Name, user.Username),
        new Claim(ClaimTypes.Role, repo.GetRole(user.Username)),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
    };

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
        issuer: cfg["Jwt:Issuer"],
        audience: cfg["Jwt:Audience"],
        claims: claims,
        expires: DateTime.UtcNow.AddMinutes(60),
        signingCredentials: creds);

    var jwt = new JwtSecurityTokenHandler().WriteToken(token);
    return Results.Ok(new { Token = jwt });   // <<— wa¿ne: „Token”
}).AllowAnonymous();

// ===== Students (autoryzacja wymagana) =====
var students = api.MapGroup("/students").RequireAuthorization();

students.MapGet("/", async (ApplicationDbContext db) =>
    Results.Ok(await db.Students.AsNoTracking().ToListAsync()));

students.MapGet("/{id:int}", async (int id, ApplicationDbContext db) =>
    await db.Students.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id) is { } s ? Results.Ok(s) : Results.NotFound());

students.MapPost("/", async (StudentCreateDto dto, ApplicationDbContext db) =>
{
    var s = new Student { Name = dto.Name, Email = dto.Email };
    db.Students.Add(s);
    await db.SaveChangesAsync();
    return Results.Created($"/api/students/{s.Id}", s);
});

students.MapPut("/{id:int}", async (int id, StudentUpdateDto dto, ApplicationDbContext db) =>
{
    var s = await db.Students.FindAsync(id);
    if (s is null) return Results.NotFound();
    if (!string.IsNullOrWhiteSpace(dto.Name)) s.Name = dto.Name!;
    if (!string.IsNullOrWhiteSpace(dto.Email)) s.Email = dto.Email!;
    await db.SaveChangesAsync();
    return Results.Ok(s);
});

students.MapDelete("/{id:int}", async (int id, ApplicationDbContext db) =>
{
    var s = await db.Students.FindAsync(id);
    if (s is null) return Results.NotFound();
    db.Students.Remove(s);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.Run();
