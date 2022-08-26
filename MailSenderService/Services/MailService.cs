using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using MailSenderService.Model;
using MailSenderService.Settings;
using Microsoft.Extensions.Options;
using MimeKit;

namespace MailSenderService.Services
{
    public class MailService : IMailService
    {
        private readonly MailSettings _mailSettings;

        public MailService(IOptions<MailSettings> options)
        {
            _mailSettings = options.Value;
        }

        public async Task MailRequestWithPdfUrl(MailRequest mailRequest)
        {
            var email = new MimeMessage();
            email.Sender = MailboxAddress.Parse(_mailSettings.Mail);
            email.Subject = mailRequest.Subject;
            var builder = new BodyBuilder();
            if (mailRequest.FileUrls != null)
            {
                byte[] fileBytes = Array.Empty<byte>();
                foreach (var url in mailRequest.FileUrls)
                {
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        using (var ms = new MemoryStream())
                        {
                            byte[] pdfAsByteArray = FileRetriever(url).Result;
                            ms.Write(pdfAsByteArray, 0, pdfAsByteArray.Length);
                            fileBytes = ms.ToArray();
                        }

                        string fileName = Path.GetFileName(url);
                        builder.Attachments.Add(fileName, fileBytes);
                    }
                }
            }

            builder.HtmlBody = mailRequest.Body;
            email.Body = builder.ToMessageBody();
            using (var smtp = new SmtpClient())
            {
                smtp.Connect(_mailSettings.Host, _mailSettings.Port, SecureSocketOptions.StartTls);
                smtp.Authenticate(_mailSettings.Mail, _mailSettings.Password);

                foreach (var ToUser in mailRequest.ToEmail)
                {
                    email.To.Add(MailboxAddress.Parse(ToUser));
                    await smtp.SendAsync(email);
                }

                smtp.Disconnect(true);
            }
        }

        async Task<byte[]> FileRetriever(string url)
        {
            Uri uri = new Uri(url);
            using (var client = new WebClient())
            {
                byte[] fileBytes = await client.DownloadDataTaskAsync(uri);
                return fileBytes;
            }
        }
    }
}