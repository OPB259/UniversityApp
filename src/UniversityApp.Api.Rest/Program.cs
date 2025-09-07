using System.Text;
using Microsoft.EntityFrameworkCore;
using UniversityApp.Domain.Entities;
using UniversityApp.Infrastructure;
using UniversityApp.Infrastructure.Data;
using UniversityApp.Api.Rest; // DTO z folderu Struktury

var builder = WebApplication.CreateBuilder(args);

// EF InMemory (Infrastructure)
builder.Services.AddInfrastructure();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// --- Middleware: log nag³ówków + body + X-Correlation-Id ---
app.Use(async (context, next) =>
{
    Console.WriteLine($"[{DateTime.UtcNow:O}] {context.Request.Method} {context.Request.Path}");
    foreach (var h in context.Request.Headers)
        Console.WriteLine($"H {h.Key}: {h.Value}");

    context.Request.EnableBuffering();

    string? body = null;
    if (context.Request.ContentLength is > 0)
    {
        // (stream, encoding, detectBOM, bufferSize, leaveOpen)
        using var reader = new StreamReader(
            context.Request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024,
            leaveOpen: true);

        body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;
    }

    if (!string.IsNullOrWhiteSpace(body))
        Console.WriteLine($"Body: {body}");

    var cid = context.Request.Headers.TryGetValue("X-Correlation-Id", out var v)
        ? v.ToString()
        : Guid.NewGuid().ToString();

    context.Response.Headers["X-Correlation-Id"] = cid;

    await next();
});

var api = app.MapGroup("/api");

// ===== Students =====
var students = api.MapGroup("/students");

students.MapGet("/", async (ApplicationDbContext db) =>
    Results.Ok(await db.Students.AsNoTracking().ToListAsync()));

students.MapGet("/{id:int}", async (int id, ApplicationDbContext db) =>
    await db.Students.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id) is { } s
        ? Results.Ok(s)
        : Results.NotFound());

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

// ===== Courses =====
var courses = api.MapGroup("/courses");

courses.MapGet("/", async (ApplicationDbContext db) =>
    Results.Ok(await db.Courses.AsNoTracking().ToListAsync()));

courses.MapGet("/{id:int}", async (int id, ApplicationDbContext db) =>
    await db.Courses.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id) is { } c
        ? Results.Ok(c)
        : Results.NotFound());

courses.MapPost("/", async (CourseCreateDto dto, ApplicationDbContext db) =>
{
    var c = new Course { Title = dto.Title, Credits = dto.Credits };
    db.Courses.Add(c);
    await db.SaveChangesAsync();
    return Results.Created($"/api/courses/{c.Id}", c);
});

courses.MapPut("/{id:int}", async (int id, CourseUpdateDto dto, ApplicationDbContext db) =>
{
    var c = await db.Courses.FindAsync(id);
    if (c is null) return Results.NotFound();

    if (!string.IsNullOrWhiteSpace(dto.Title)) c.Title = dto.Title!;
    if (dto.Credits is not null) c.Credits = dto.Credits.Value;

    await db.SaveChangesAsync();
    return Results.Ok(c);
});

courses.MapDelete("/{id:int}", async (int id, ApplicationDbContext db) =>
{
    var c = await db.Courses.FindAsync(id);
    if (c is null) return Results.NotFound();

    db.Courses.Remove(c);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

// ===== Enrollments =====
var enrollments = api.MapGroup("/enrollments");

enrollments.MapGet("/", async (ApplicationDbContext db) =>
{
    var list = await db.Enrollments
        .AsNoTracking()
        .Include(e => e.Student)
        .Include(e => e.Course)
        .Select(e => new EnrollmentDto(
            e.Id,
            e.StudentId,
            e.Student!.Name,
            e.CourseId,
            e.Course!.Title,
            e.EnrolledAt))
        .ToListAsync();

    return Results.Ok(list);
});

enrollments.MapGet("/{id:int}", async (int id, ApplicationDbContext db) =>
{
    var e = await db.Enrollments.AsNoTracking()
        .Include(x => x.Student).Include(x => x.Course)
        .SingleOrDefaultAsync(x => x.Id == id);

    return e is null
        ? Results.NotFound()
        : Results.Ok(new EnrollmentDto(
            e.Id, e.StudentId, e.Student!.Name, e.CourseId, e.Course!.Title, e.EnrolledAt));
});

enrollments.MapPost("/", async (EnrollmentCreateDto dto, ApplicationDbContext db) =>
{
    if (!await db.Students.AnyAsync(s => s.Id == dto.StudentId))
        return Results.BadRequest($"Student {dto.StudentId} does not exist.");

    if (!await db.Courses.AnyAsync(c => c.Id == dto.CourseId))
        return Results.BadRequest($"Course {dto.CourseId} does not exist.");

    if (await db.Enrollments.AnyAsync(e => e.StudentId == dto.StudentId && e.CourseId == dto.CourseId))
        return Results.Conflict($"Student {dto.StudentId} is already enrolled to course {dto.CourseId}.");

    var e = new Enrollment { StudentId = dto.StudentId, CourseId = dto.CourseId };
    db.Enrollments.Add(e);
    await db.SaveChangesAsync();

    return Results.Created($"/api/enrollments/{e.Id}",
        new { e.Id, e.StudentId, e.CourseId, e.EnrolledAt });
});

enrollments.MapPut("/{id:int}", async (int id, EnrollmentUpdateDto dto, ApplicationDbContext db) =>
{
    var e = await db.Enrollments.FindAsync(id);
    if (e is null) return Results.NotFound();

    if (dto.StudentId is not null)
    {
        if (!await db.Students.AnyAsync(s => s.Id == dto.StudentId))
            return Results.BadRequest($"Student {dto.StudentId} does not exist.");
        e.StudentId = dto.StudentId.Value;
    }

    if (dto.CourseId is not null)
    {
        if (!await db.Courses.AnyAsync(c => c.Id == dto.CourseId))
            return Results.BadRequest($"Course {dto.CourseId} does not exist.");
        e.CourseId = dto.CourseId.Value;
    }

    await db.SaveChangesAsync();
    return Results.Ok(new { e.Id, e.StudentId, e.CourseId, e.EnrolledAt });
});

enrollments.MapDelete("/{id:int}", async (int id, ApplicationDbContext db) =>
{
    var e = await db.Enrollments.FindAsync(id);
    if (e is null) return Results.NotFound();

    db.Enrollments.Remove(e);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.Run();
