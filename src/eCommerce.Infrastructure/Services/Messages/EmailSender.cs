﻿using eCommerce.Core.Entities.Configuration.CustomSettings;
using eCommerce.Core.Entities.Messages;
using eCommerce.Core.Interfaces;
using eCommerce.Core.Services.Messages;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace eCommerce.Infrastructure.Services.Messages;

public class EmailSender : IEmailSender
{
    #region Fields

    private readonly EmailAccountSettings _emailAccountSettings;

    private readonly IRepository<EmailAccount> _emailAccountRepository;

    #endregion

    #region Constructure and Destructure

    public EmailSender(
        EmailAccountSettings emailAccountSettings, 
        IRepository<EmailAccount> emailAccountRepository)
    {
        _emailAccountSettings = emailAccountSettings;
        _emailAccountRepository = emailAccountRepository;
    }

    #endregion

    #region Public Methods

    public async Task SendEmailAsync(
        EmailAccount emailAccount,
        string subject,
        string body,
        string fromAddress,
        string fromName,
        string toAddress,
        string toName,
        string? replyTo = null,
        string? replyToName = null,
        IEnumerable<string>? bcc = null,
        IEnumerable<string>? cc = null,
        string? attachmentFilePath = null,
        string? attachmentFileName = null,
        int? attachedDownloadId = null,
        IDictionary<string, string>? headers = null)
    {
        var message = new MimeMessage();

        message.From.Add(new MailboxAddress(fromName, fromAddress));
        message.To.Add(new MailboxAddress(toName, toAddress));

        if (!string.IsNullOrEmpty(replyTo))
        {
            message.ReplyTo.Add(new MailboxAddress(replyToName, replyTo));
        }

        //BCC
        if (bcc != null)
        {
            foreach (var address in bcc.Where(bccValue => !string.IsNullOrWhiteSpace(bccValue)))
            {
                message.Bcc.Add(new MailboxAddress("", address.Trim()));
            }
        }

        //CC
        if (cc != null)
        {
            foreach (var address in cc.Where(ccValue => !string.IsNullOrWhiteSpace(ccValue)))
            {
                message.Cc.Add(new MailboxAddress("", address.Trim()));
            }
        }

        //content
        message.Subject = subject;

        //headers
        if (headers is not null)
        {
            foreach (var header in headers)
            {
                message.Headers.Add(header.Key, header.Value);
            }
        }

        var multipart = new Multipart("mixed")
        {
            new TextPart(TextFormat.Html) { Text = body }
        };

        message.Body = multipart;

        //send email
        using var smtpClient = await BuildAsync(emailAccount);
        await smtpClient.SendAsync(message);
        await smtpClient.DisconnectAsync(true);
    }

    #endregion

    #region Methods

    private async Task<SmtpClient> BuildAsync(EmailAccount? emailAccount = null)
    {
        emailAccount ??= await _emailAccountRepository.GetByIdAsync(_emailAccountSettings.DefaultEmailAccountId)
            ?? throw new Exception("Email account could not be loaded");

        var client = new SmtpClient
        {
            ServerCertificateValidationCallback = ValidateServerCertificate
        };

        try
        {
            await client.ConnectAsync(
                emailAccount.Host,
                emailAccount.Port,
                emailAccount.EnableSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable);

            await client.AuthenticateAsync(new NetworkCredential(emailAccount.Username, emailAccount.Password));

            return client;
        }
        catch (Exception ex)
        {
            client.Dispose();
            throw new Exception(ex.Message, ex);
        }
    }

    private bool ValidateServerCertificate(
        object sender,
        X509Certificate certificate,
        X509Chain chain,
        SslPolicyErrors sslPolicyErrors)
    {
        //By default, server certificate verification is disabled.
        return true;
    }

    #endregion
}