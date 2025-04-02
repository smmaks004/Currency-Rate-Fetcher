using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using static CurrencyRateFetcher.SettingsHelper;

namespace CurrencyRateFetcher
{
    internal class EmailHelper
    {
        public static void SendEmail(UserConfig userConfig, SmtpConfig smtpConfig)
        {
            int smtpPort = int.Parse(smtpConfig.smtpPort); // Convert the port to an integer
            
            SmtpClient smtpClient = new SmtpClient(smtpConfig.smtpHost)
            {
                Port = smtpPort,
                Credentials = new NetworkCredential(userConfig.emailSender, smtpConfig.smtpPassword),
                EnableSsl = true
            };

            // Create the email message
            MailMessage mailMessage = new MailMessage
            {
                From = new MailAddress(userConfig.emailSender),
                Subject = "Error Warning",
                Body = "Error in application work, please check logs"
            };
            // Add the recipient
            mailMessage.To.Add(userConfig.emailRecipient);

            // Sending a message
            smtpClient.Send(mailMessage);
        }
    }

}
