using System;
using System.Xml.Linq;
using System.Net;

using MySqlConnector; 
using Serilog;
using System.Net.Mail;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;

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
        // JSON configuration files

        Log.Information("Reading configuration files");

        // System settings from the JSON file
        var systemSettings = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory) // Executable directory
            .AddJsonFile("systemSettings.json", optional: false, reloadOnChange: true) // File from the current directory
            .Build();

        var databaseConfig = systemSettings.GetSection("SystemPreferences:Database");
        string? dbName = databaseConfig["Name"];
        string? dbAddress = databaseConfig["Address"];
        string? dbPort = databaseConfig["Port"];
        string? dbUser = databaseConfig["User"];
        string? dbPassword = databaseConfig["Password"];

        var smtpConfig = systemSettings.GetSection("SystemPreferences:Smtp");
        string? smtpHost = smtpConfig["Host"];
        string? smtpPort = smtpConfig["Port"];
        string? smtpPassword = smtpConfig["Password"];
        
        // User settings from the JSON file
        var userSettings = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory) // Root folder of the project
            .AddJsonFile("userSettings.json", optional: false, reloadOnChange: true)
            .Build();

        var userPreferences = userSettings.GetSection("UserPreferences");
        TimeSpan DesiredResponseTime = TimeSpan.ParseExact(userPreferences["DesiredResponseTime"], "hh\\:mm", System.Globalization.CultureInfo.InvariantCulture);
        string? emailSender = userPreferences["Sender"];
        string? emailRecipient = userPreferences["Recipient"];

        Log.Information("Configuration files were read successfully");

        /****************************************************************/
        // Key and date selection

        string SelectedDay = ""; // Declaring a variable for the selected day

        if (args.Length == 0)
        {
            //Console.WriteLine("No Keys");
            if (DateTime.Now.TimeOfDay > DesiredResponseTime) // If the current time is greater than the 'desired response time'
            {
                SelectedDay = DateTime.UtcNow.ToString("yyyy-MM-dd"); 
            }
            if (DateTime.Now.TimeOfDay < DesiredResponseTime) // If the current time is less than the 'desired response time'
            {
                SelectedDay = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
            }
        }
        else
        {
            string key = args[0].ToLower(); 
            switch (key)
            {
                case "--help":
                    ShowHelp();
                    return;
                case "today": 
                    SelectedDay = DateTime.UtcNow.ToString("yyyy-MM-dd"); // Console.WriteLine($"Selected day: {SelectedDay}");
                    break;
                case "yesterday":
                    SelectedDay = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd"); // Console.WriteLine($"Selected day: {SelectedDay}");
                    break;
                default:
                    Console.WriteLine($"Unknown key: {key}. Use '--help' to view available keys..");
                    return;
            }
        }

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
            SendEmail(emailSender, emailRecipient, smtpHost, smtpPort, smtpPassword);
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

        // DB connection string
        string connectionString = $"Server={dbAddress};Database={dbName};Port={dbPort};User={dbUser};Password={dbPassword};";   
        
        using MySqlConnection conn = new MySqlConnection(connectionString);
        try 
        { 
            conn.Open();
            Log.Information("Connection to DB was established");
        }
        catch (Exception ex)
        {
            Log.Logger.Here().Error(ex, "Connection to DB was not established");
            SendEmail(emailSender, emailRecipient, smtpHost, smtpPort, smtpPassword);
            return;
        }

        /****************************************************************/

        try
        {
            foreach (var series in xmlDoc.Descendants("{http://www.sdmx.org/resources/sdmxml/schemas/v2_1/data/generic}Series"))
            {
                Log.Information("START - Parsing series");

                // Extracting Currency from 'SeriesKey'
                var currencyElement = series.Element("{http://www.sdmx.org/resources/sdmxml/schemas/v2_1/data/generic}SeriesKey")
                                            ?.Elements("{http://www.sdmx.org/resources/sdmxml/schemas/v2_1/data/generic}Value")
                                            ?.FirstOrDefault(e => e.Attribute("id")?.Value == "CURRENCY");

                string currency = currencyElement?.Attribute("value")?.Value; // Currency code

                // Extracting data
                foreach (var obs in series.Descendants("{http://www.sdmx.org/resources/sdmxml/schemas/v2_1/data/generic}Obs")) 
                {
                    Log.Information("Parsing obs"); 

                    string date = obs.Element("{http://www.sdmx.org/resources/sdmxml/schemas/v2_1/data/generic}ObsDimension")?.Attribute("value")?.Value;
                    string value = obs.Element("{http://www.sdmx.org/resources/sdmxml/schemas/v2_1/data/generic}ObsValue")?.Attribute("value")?.Value;

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
            SendEmail(emailSender, emailRecipient, smtpHost, smtpPort, smtpPassword);
        }

        Log.CloseAndFlush();    // Closedown
    }

    static void SendEmail(string? emailSender, string? emailRecipient, string? smtpHost, string? smtpPortString, string? smtpPassword)
    {
        int smtpPort = int.Parse(smtpPortString); // Convert the port to an integer
        // Configure the SMTP client
        SmtpClient smtpClient = new SmtpClient(smtpHost)
        {
            Port = smtpPort,
            Credentials = new NetworkCredential(emailSender, smtpPassword),
            EnableSsl = true
        };

        // Create the email message
        MailMessage mailMessage = new MailMessage
        {
            From = new MailAddress(emailSender),
            Subject = "Error Warning",
            Body = "Error in application work, please check logs"
        };
        // Add the recipient
        mailMessage.To.Add(emailRecipient);

        // Sending a message
        smtpClient.Send(mailMessage);
    }

    // Help method for Keys
    static void ShowHelp()
    {
        Console.WriteLine("Available keys:");
        Console.WriteLine("--help       Show all keys.");
        Console.WriteLine("today        Get data for the current day.");
        Console.WriteLine("yesterday    Get data for the previous day.");
    }

}
