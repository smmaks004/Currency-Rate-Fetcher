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

// API endpoint to get currency rates
app.MapGet("/api/currencyRates", async (MyDbContext dbContext, string? currencyCode, DateTime? startPeriod, DateTime? endPeriod) =>
{
    try
    {
        var query = dbContext.CurrencyRates
            .Include(cr => cr.Currency)
            .AsQueryable();

        // Filtering by currency code
        if (!string.IsNullOrEmpty(currencyCode))
        {
            var currencies = currencyCode.Split(','); // Splitting by comma
            query = query.Where(cr => currencies.Contains(cr.Currency.CurrencyCode));
        }


        // Filtering by date
        if (startPeriod.HasValue)
        {
            query = query.Where(cr => cr.Date.ToDateTime(TimeOnly.MinValue) >= startPeriod.Value);
        }
        if (endPeriod.HasValue)
        {
            query = query.Where(cr => cr.Date.ToDateTime(TimeOnly.MinValue) <= endPeriod.Value);
        }

        // If there is at least one date - sort by date
        if (startPeriod.HasValue || endPeriod.HasValue)
        {
            query = query.OrderBy(cr => cr.Date);
        }
        // If date isn't specified - sort by currency code and then date
        else if (!string.IsNullOrEmpty(currencyCode))
        {
            query = query.OrderBy(cr => cr.Currency.CurrencyCode).ThenBy(cr => cr.Date);
        }


        var results = await query
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

// API endpoint to get all currencies
app.MapGet("/api/currency", async (MyDbContext dbContext) =>
{
    try
    {
        var currencies = await dbContext.Currencies
            .OrderBy(c => c.CurrencyCode)
            .Select(c => new { c.CurrencyCode })
            .ToListAsync();

        return Results.Json(currencies);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error accessing database: {ex.Message}");
    }
});

app.Run();