using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CurrencyRateFetcher
{
    internal class KeysHelper
    {
        public static string ValidateKeys(string[] args, TimeSpan DesiredResponseTime)
        {
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
                return SelectedDay;
            }
            else
            {
                string key = args[0].ToLower();
                switch (key)
                {
                    case "--help":
                        ShowHelp();
                        return "";
                    case "today":
                        SelectedDay = DateTime.UtcNow.ToString("yyyy-MM-dd"); // Console.WriteLine($"Selected day: {SelectedDay}");
                        break;
                    case "yesterday":
                        SelectedDay = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd"); // Console.WriteLine($"Selected day: {SelectedDay}");
                        break;
                    default:
                        Console.WriteLine($"Unknown key: {key}. Use '--help' to view available keys..");
                        return "";
                }
                return SelectedDay;
            }
        }

        // Help method for Keys
        public static void ShowHelp()
        {
            Console.WriteLine("Available keys:");
            Console.WriteLine("--help       Show all keys.");
            Console.WriteLine("today        Get data for the current day.");
            Console.WriteLine("yesterday    Get data for the previous day.");
        }
    }
}
