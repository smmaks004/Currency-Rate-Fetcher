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
using CurrencyRateFetcher.Models;
using System.Globalization;

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
    private const string SdmxSchemaUrl = "http://www.sdmx.org/resources/sdmxml/schemas/v2_1/data/generic"; // string URL for the XML schema
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

        XNamespace sdmxNamespace = SdmxSchemaUrl;

        try
        {
            using (var context = new MyDbContext(databaseConfig))
            {
                foreach (var series in xmlDoc.Descendants(sdmxNamespace + "Series"))
                {
                    Log.Information("START - Parsing series");

                    // Extracting Currency from 'SeriesKey'
                    var currencyElement = series.Element(sdmxNamespace + "SeriesKey")
                                                ?.Elements(sdmxNamespace + "Value")
                                                ?.FirstOrDefault(e => e.Attribute("id")?.Value == "CURRENCY");

                    string? currencyCode = currencyElement?.Attribute("value")?.Value; // Currency code
                    
                    // Find or create currency in the database
                    var currency = context.Currencies.FirstOrDefault(c => c.CurrencyCode == currencyCode);
                    if (currency == null)
                    {
                        Log.Information($"Currency {currencyCode} not found in DB. Creating...");
                        currency = new Currency { CurrencyCode = currencyCode }; // Create a new currency object
                        context.Currencies.Add(currency); // Add to the database context
                        context.SaveChanges(); // Save to the database
                        Log.Information($"Currency {currencyCode} added with ID {currency.Id}");
                    }

                    // Extracting data
                    foreach (var obs in series.Descendants(sdmxNamespace + "Obs"))
                    {
                        Log.Information("Parsing obs");

                        string? dateString = obs.Element(sdmxNamespace + "ObsDimension")?.Attribute("value")?.Value;
                        string? rateString = obs.Element(sdmxNamespace + "ObsValue")?.Attribute("value")?.Value;

                        DateOnly date = DateOnly.Parse(dateString); // Parse the date string
                        decimal exchangeRate = decimal.Parse(rateString, CultureInfo.InvariantCulture); // Parse the rate string

                        Log.Information($"Checking existence for Date: {date} and Currency: {currencyCode}");


                        // Check if a record already exists for the given date and currency
                        bool exists = context.CurrencyRates.Any(cr => cr.Date == date && cr.CurrencyId == currency.Id);
                        if (exists)
                        {
                            Log.Information($"Record already exists for Date: {date} and Currency: {currency}. Skipping.");
                            continue;
                        }
                        else
                        {
                            Log.Information($"Adding CurrencyRate for Date: {date}, Currency: {currencyCode}, Rate: {exchangeRate}");

                            // Create a new CurrencyRate object
                            var currencyRate = new CurrencyRates
                            {
                                Date = date,
                                CurrencyId = currency.Id,
                                ExchangeRate = exchangeRate
                            };

                            context.CurrencyRates.Add(currencyRate); // Add to the database context
                            context.SaveChanges(); // Save to the database

                            Log.Information($"CurrencyRate for {currencyCode} on {date} was saved.");
                        }

                    }

                    Log.Information("END - Parsing series");
                }

                Log.Information("The program has been successfully completed");
            }
        }
        catch (Exception ex)
        {
            Log.Logger.Here().Error(ex, "An error has occurred");
            Console.WriteLine("An error has occurred, check the logs for more information");
            EmailHelper.SendEmail(userConfig, smtpConfig);
        }

        Log.CloseAndFlush(); // Closedown
    }

}
