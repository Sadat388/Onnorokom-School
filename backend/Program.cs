using Backend;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<AdmissionService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var origins = (builder.Configuration["CORS_ORIGINS"] ?? "http://localhost:3000")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Wait for PostgreSQL, then create the schema if it does not exist yet.
for (var attempt = 1; ; attempt++)
{
    try
    {
        using var scope = app.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        break;
    }
    catch (Exception ex) when (attempt < 30)
    {
        app.Logger.LogWarning("Database not ready (attempt {Attempt}): {Message}", attempt, ex.Message);
        await Task.Delay(TimeSpan.FromSeconds(2));
    }
}

app.UseExceptionHandler(errors => errors.Run(async ctx =>
{
    var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    if (ex is DomainException d)
    {
        ctx.Response.StatusCode = d.StatusCode;
        await ctx.Response.WriteAsJsonAsync(new ApiError(d.Code, d.Message));
        return;
    }
    app.Logger.LogError(ex, "Unhandled error");
    ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await ctx.Response.WriteAsJsonAsync(new ApiError("SERVER_ERROR", "Something went wrong on the server."));
}));
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();

app.MapPost("/classes", async (CreateClassRequest req, AdmissionService svc) =>
{
    var created = await svc.CreateClassAsync(req);
    return Results.Created($"/classes/{created.Id}", created);
});

app.MapGet("/classes", (AdmissionService svc) => svc.ListClassesAsync());

app.MapGet("/classes/{classId}", async (string classId, AdmissionService svc) =>
    Results.Ok(await svc.GetClassAsync(classId)));

app.MapPost("/classes/{classId}/holds", async (string classId, HttpRequest http, AdmissionService svc) =>
{
    var hold = await svc.CreateHoldAsync(classId, StudentId.From(http));
    return Results.Created($"/holds/{hold.HoldId}", hold);
});

app.MapGet("/holds/{holdId}", async (string holdId, HttpRequest http, AdmissionService svc) =>
    Results.Ok(await svc.GetHoldAsync(holdId, StudentId.From(http))));

app.MapPost("/holds/{holdId}/confirm", async (string holdId, HttpRequest http, AdmissionService svc) =>
    Results.Ok(await svc.ConfirmHoldAsync(holdId, StudentId.From(http))));

app.MapDelete("/holds/{holdId}", async (string holdId, HttpRequest http, AdmissionService svc) =>
{
    await svc.CancelHoldAsync(holdId, StudentId.From(http));
    return Results.NoContent();
});

app.MapGet("/me/holds", (HttpRequest http, AdmissionService svc) =>
    svc.MyHoldsAsync(StudentId.From(http)));

app.Run();

public partial class Program { } // lets the test project reference the app