﻿using eCommerce.Core.Entities.Messages;
using eCommerce.Core.Interfaces;
using eCommerce.Core.Services.Messages;
using eCommerce.Core.Services.ScheduleTasks;
using eCommerce.Core.Shared;

namespace eCommerce.Infrastructure.BackgroundJobs;

public class QueuedMessagesSendTask : IScheduleTask
{
    #region Fields

    private readonly IRepository<EmailAccount> _emailAccountRepository;

    private readonly IEmailSender _emailSender;

    private readonly IQueuedEmailService _queuedEmailService;

    #endregion

    #region Constructure and Destructure

    public QueuedMessagesSendTask(
        IEmailSender emailSender,
        IQueuedEmailService queuedEmailService,
        IRepository<EmailAccount> emailAccountRepository)
    {
        _emailSender = emailSender;
        _queuedEmailService = queuedEmailService;
        _emailAccountRepository = emailAccountRepository;
    }

    #endregion

    #region Methods

    public async Task ExecuteAsync()
    {
        var maxTries = 3;
        var queuedEmails = await _queuedEmailService.SearchEmailsAsync(
            null,
            null,
            null,
            null,
            true,
            true,
            maxTries,
            false,
            0,
            500);

        foreach (var queuedEmail in queuedEmails)
        {
            var bcc = string.IsNullOrWhiteSpace(queuedEmail.Bcc)
                        ? null : queuedEmail.Bcc.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            var cc = string.IsNullOrWhiteSpace(queuedEmail.CC)
                        ? null : queuedEmail.CC.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            try
            {
                var email = await _emailAccountRepository.GetByIdAsync(queuedEmail.EmailAccountId);
                email.Password = EncryptionHelper.DecryptText(email.Password, email.PasswordSalt);

                await _emailSender.SendEmailAsync(
                    email,
                    queuedEmail.Subject,
                    queuedEmail.Body,
                    queuedEmail.From,
                    queuedEmail.FromName,
                    queuedEmail.To,
                    queuedEmail.ToName,
                    queuedEmail.ReplyTo,
                    queuedEmail.ReplyToName,
                    bcc,
                    cc,
                    queuedEmail.AttachmentFilePath,
                    queuedEmail.AttachmentFileName,
                    queuedEmail.AttachedDownloadId);

                queuedEmail.SentOnUtc = DateTime.UtcNow;
            }
            catch (Exception exc)
            {
                throw new Exception($"Error sending e-mail. {exc.Message}", exc);
            }
            finally
            {
                queuedEmail.SentTries += 1;
                await _queuedEmailService.UpdateQueuedEmailAsync(queuedEmail);
            }
        }
    }

    #endregion
}