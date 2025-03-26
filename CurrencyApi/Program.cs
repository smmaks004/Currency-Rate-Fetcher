using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);


var systemSettings = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("systemSettings.json")
            .Build();

var databaseConfig = systemSettings.GetSection("SystemPreferences:Database");

string connectionString = $"Server={databaseConfig["Address"]};Database={databaseConfig["Name"]};Port={databaseConfig["Port"]};User={databaseConfig["User"]};Password={databaseConfig["Password"]};";
using var connection = new MySqlConnection(connectionString);

var app = builder.Build();


app.MapGet("/api/currencyRates", async () =>
{
    var results = new List<object>();

    try
    {
        using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();

        string query = "SELECT date, currency_code, exchange_rate FROM CurrencyRates;";
        using var command = new MySqlCommand(query, connection);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new
            {
                Date = reader.GetDateTime("date").ToString("yyyy-MM-dd"), // Format the date as a string
                Currency = reader.GetString("currency_code"),
                Rate = reader.GetDecimal("exchange_rate")
            });

        }
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error accessing database: {ex.Message}");
    }

    return Results.Json(results); // Return the result in JSON format
});

app.Run();