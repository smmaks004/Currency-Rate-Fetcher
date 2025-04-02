using System;
using System.Xml.Linq;
using System.Net;

using MySqlConnector; 
using Serilog;
using System.Net.Mail;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using CurrencyRateFetcher;
using static CurrencyRateFetcher.SettingsHelper;

public static class LoggerExtensions // Extension method for Serilog
{
    public static ILogger Here(this ILogger logger,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        return logger
            .ForContext("MemberName", memberName)
            .ForContext("FilePath", sourceFilePath)
            .ForContext("LineNumber", sourceLineNumber);
    }
}

class Program
{
    private const string SdmxSchemaUrl = "http://www.sdmx.org/resources/sdmxml/schemas/v2_1/data/generic";

    static async Task Main(string[] args)
    {
        // Logging configuration
        Log.Logger = new LoggerConfiguration()
        .WriteTo.File(
            path: "logs/log-.txt", // Set the file location
            rollingInterval: RollingInterval.Day, // Create a new file every day
            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message}{NewLine}{Exception}"
        )
        .CreateLogger();

        Log.Information("Program start");

        /****************************************************************/
        // JSON configuration files (SettingsHelper.cs file)

        Log.Information("Reading configuration files");

        var (userConfig, databaseConfig, smtpConfig) = SettingsHelper.SettingsLoading(); 

        Log.Information("Configuration files were read successfully");

        /****************************************************************/
        // Key and date selection (KeysHelper.cs file)

        string SelectedDay = KeysHelper.ValidateKeys(args, userConfig.DesiredResponseTime);
        if (SelectedDay.Length == 0) return;

        /**************************************************************/
        // ECB API processing

        // API URL
        string apiUrl = $"https://data-api.ecb.europa.eu/service/data/EXR/D..EUR.SP00.A?startPeriod={SelectedDay}&endPeriod={SelectedDay}&format=xml";

        // Create an 'HttpClient' object
        using HttpClient client = new HttpClient();
        // Add the 'Accept' header
        client.DefaultRequestHeaders.Add("Accept", "application/xml");
        // Send a GET request
        HttpResponseMessage response = await client.GetAsync(apiUrl);

        try { response.EnsureSuccessStatusCode(); } // Check the response status
        catch (Exception)
        {
            Log.Logger.Information($"An error occurred while receiving data from the ECB API. Status code: {response.StatusCode}");
            EmailHelper.SendEmail(userConfig, smtpConfig);
            return;
        }

        string responseBody = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(responseBody)) // Check if the response is empty
        {
            Log.Logger.Information($"There are no entries for {SelectedDay}");
            return;
        }

        // XML parsing
        XDocument xmlDoc = XDocument.Parse(responseBody);

        /****************************************************************/
        // Database connection

        string connectionString = 
            $"Server={databaseConfig.dbAddress};" +
            $"Database={databaseConfig.dbName};" +
            $"Port={databaseConfig.dbPort};" +
            $"User={databaseConfig.dbUser};" +
            $"Password={databaseConfig.dbPassword};";   
        
        using MySqlConnection conn = new MySqlConnection(connectionString);
        try 
        { 
            conn.Open();
            Log.Information("Connection to DB was established");
        }
        catch (Exception ex)
        {
            Log.Logger.Here().Error(ex, "Connection to DB was not established");
            EmailHelper.SendEmail(userConfig, smtpConfig);
            return;
        }

        /****************************************************************/

        try
        {
            foreach (var series in xmlDoc.Descendants($"{SdmxSchemaUrl}Series"))
            {
                Log.Information("START - Parsing series");

                // Extracting Currency from 'SeriesKey'
                var currencyElement = series.Element($"{SdmxSchemaUrl}SeriesKey")
                                            ?.Elements($"{SdmxSchemaUrl}Value")
                                            ?.FirstOrDefault(e => e.Attribute("id")?.Value == "CURRENCY");

                string currency = currencyElement?.Attribute("value")?.Value; // Currency code

                // Extracting data
                foreach (var obs in series.Descendants($"{SdmxSchemaUrl}Obs")) 
                {
                    Log.Information("Parsing obs"); 

                    string date = obs.Element($"{SdmxSchemaUrl}ObsDimension")?.Attribute("value")?.Value;
                    string value = obs.Element($"{SdmxSchemaUrl}ObsValue")?.Attribute("value")?.Value;

                    Log.Information($"Checking existence in DB: Date: {date}, Currency: {currency}");

                    // SQL query to verify the existence of a record
                    string checkQuery = "SELECT COUNT(*) FROM CurrencyRates WHERE date = @date AND currency_code = @currency;";

                    // Check if the record already exists
                    using (MySqlCommand checkCmd = new MySqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@date", date);
                        checkCmd.Parameters.AddWithValue("@currency", currency);

                        int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                        if (count > 0) // Record already exists
                        {
                            Log.Information($"Record already exists for Date: {date}, Currency: {currency}. Skipping insert.");
                            continue;
                        }
                    }

                    Log.Information($"Trying to save in DB: Date: {date}, Currency: {currency}, Value in EUR: {value}");

                    // SQL query to insert a new record
                    string insertQuery = "INSERT INTO CurrencyRates (date, currency_code, exchange_rate) " +
                                         "VALUES (@date, @currency, @rate);";

                    using (MySqlCommand insertCmd = new MySqlCommand(insertQuery, conn)) // Inserting a new record
                    {
                        insertCmd.Parameters.AddWithValue("@date", date);
                        insertCmd.Parameters.AddWithValue("@currency", currency);
                        insertCmd.Parameters.AddWithValue("@rate", value);
                        insertCmd.ExecuteNonQuery();

                        Log.Information($"Information about currency - {currency} was saved");
                    }
                }
                Log.Information("END - Parsing series");
            }
            Log.Information("The program has been successfully completed");
        }
        catch (Exception ex)
        {
            Log.Logger.Here().Error(ex, "An error has occurred");
            Console.WriteLine("An error has occurred, check the logs for more information");
            EmailHelper.SendEmail(userConfig, smtpConfig);
        }

        Log.CloseAndFlush();    // Closedown
    }

}
