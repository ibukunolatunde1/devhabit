using FluentValidation;
using DevHabit.Api.Middleware;
using DevHabit.Api.Services.Sorting;
using DevHabit.Api.DTOs.Habits;
using DevHabit.Api.Entities;
using DevHabit.Api.Services;
using DevHabit.Api;
using DevHabit.Api.Extensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddControllers().AddErrorHandling().AddDatabase().AddOpenTelemetry().AddApplicationServices();

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    await app.ApplyMigrationsAsync();
}

app.UseHttpsRedirection();
app.UseExceptionHandler();

app.MapControllers();

await app.RunAsync();
