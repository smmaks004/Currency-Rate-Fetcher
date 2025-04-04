using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static CurrencyRateFetcher.SettingsHelper;

namespace CurrencyRateFetcher
{
    public static class SettingsHelper
    {
        public class UserConfig
        {
            public TimeSpan DesiredResponseTime { get; set; }
            public string? emailSender { get; set; }
            public string? emailRecipient { get; set; }
        }
        public class DatabaseConfig
        {
            public string? dbName { get; set; }
            public string? dbAddress { get; set; }
            public string? dbPort { get; set; }
            public string? dbUser { get; set; }
            public string? dbPassword { get; set; }
        }
        public class SmtpConfig
        {
            public string? smtpHost { get; set; }
            public string? smtpPort { get; set; }
            public string? smtpPassword { get; set; }
        }
        public static (UserConfig, DatabaseConfig, SmtpConfig) SettingsLoading()
        {
            // User settings from the JSON file
            var userSettings = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory) // Root folder of the project
                .AddJsonFile("userSettings.json", optional: false, reloadOnChange: true)
                .Build();

            var userPreferences = userSettings.GetSection("UserPreferences");
            var userConfig = new UserConfig
            {
                DesiredResponseTime = TimeSpan.ParseExact(userPreferences["DesiredResponseTime"], "hh\\:mm", System.Globalization.CultureInfo.InvariantCulture),
                emailSender = userPreferences["Sender"],
                emailRecipient = userPreferences["Recipient"]
            };


            // System settings from the JSON file
            var systemSettings = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory) // Executable directory
                .AddJsonFile("systemSettings.json", optional: false, reloadOnChange: true) // File from the current directory
                .Build();

            var databaseSection = systemSettings.GetSection("SystemPreferences:Database");
            var databaseConfig = new DatabaseConfig
            {
                dbName = databaseSection["Name"],
                dbAddress = databaseSection["Address"],
                dbPort = databaseSection["Port"],
                dbUser = databaseSection["User"],
                dbPassword = databaseSection["Password"]
            };

            var smtpSection = systemSettings.GetSection("SystemPreferences:Smtp");
            var smtpConfig = new SmtpConfig
            {
                smtpHost = smtpSection["Host"],
                smtpPort = smtpSection["Port"],
                smtpPassword = smtpSection["Password"]
            };

            return (userConfig, databaseConfig, smtpConfig);
        }

    }
}
