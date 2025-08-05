using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace Nikii_Pic.Services
{
    public class MailService
    {
        private readonly IConfiguration _config;
        private readonly SmtpClient _smtpClient;
        private readonly string _sender;

        public MailService(IConfiguration config)
        {
            _config = config;
            var smtpSection = _config.GetSection("Smtp");
            _sender = smtpSection["Sender"];
            _smtpClient = new SmtpClient
            {
                Host = smtpSection["Host"],
                Port = int.Parse(smtpSection["Port"]),
                EnableSsl = bool.Parse(smtpSection["EnableSsl"]),
                Credentials = new NetworkCredential(smtpSection["User"], smtpSection["Password"])
            };
        }

        public async Task SendAsync(string to, string subject, string body)
        {
            var mail = new MailMessage(_sender, to, subject, body)
            {
                IsBodyHtml = true
            };
            await _smtpClient.SendMailAsync(mail);
        }
    }
} 