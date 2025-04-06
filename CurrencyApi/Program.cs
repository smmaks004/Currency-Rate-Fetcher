using CurrencyRateFetcher;
using Microsoft.EntityFrameworkCore;
using CurrencyRateFetcher.Models;
using System;

var builder = WebApplication.CreateBuilder(args);

var(userConfig, databaseConfig, smtpConfig) = SettingsHelper.SettingsLoading();

string connectionString = $"" +
    $"Server={databaseConfig.dbAddress};" +
    $"Database={databaseConfig.dbName};" +
    $"Port={databaseConfig.dbPort};" +
    $"User={databaseConfig.dbUser};" +
    $"Password={databaseConfig.dbPassword};";


builder.Services.AddSingleton(databaseConfig);
builder.Services.AddDbContext<MyDbContext>(options =>
    options.UseMySQL(connectionString));


var app = builder.Build();

app.MapGet("/api/currencyRates", async (MyDbContext dbContext) =>
{
    try
    {
        var results = await dbContext.CurrencyRates
            .Include(cr => cr.Currency)
            .Select(cr => new
            {
                Date = cr.Date.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-dd"), 
                Currency = cr.Currency.CurrencyCode, 
                Rate = cr.ExchangeRate
            })
            .ToListAsync();

        return Results.Json(results);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error accessing database: {ex.Message}");
    }
});

app.Run();