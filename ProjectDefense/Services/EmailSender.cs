using Microsoft.AspNetCore.Identity.UI.Services;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace ProjectDefense.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailSender> _logger;

        public EmailSender(IConfiguration configuration, ILogger<EmailSender> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            var apiKey = _configuration["SendGridKey"];

            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogError("SendGridKey is not configured.");
                throw new InvalidOperationException("SendGridKey is not configured.");
            }

            var client = new SendGridClient(apiKey);
            var from = new EmailAddress("hajdufaj@gmail.com", "Project Defense");
            var to = new EmailAddress("hajdufaj@gmail.com", "Test");

            if (htmlMessage.Contains("amp;"))
            {
                htmlMessage = htmlMessage.Replace("amp;", "");
            }

            var msg = MailHelper.CreateSingleEmail(from, to, subject, null, htmlMessage);
            var response = await client.SendEmailAsync(msg);

            if (!response.IsSuccessStatusCode)
            {
                string responseBody = await response.Body.ReadAsStringAsync();
                _logger.LogError($"Failure Email to {email}. Status: {response.StatusCode}. Body: {responseBody}");
                throw new InvalidOperationException($"Failed to send email. SendGrid returned: {response.StatusCode}. Details: {responseBody}");
            }
            _logger.LogInformation($"Email (to: {email}, redirected to: {to.Email}) queued successfully!");
        }
    }
}
