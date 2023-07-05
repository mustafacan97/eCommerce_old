﻿using YerdenYuksek.Core.Primitives;

namespace YerdenYuksek.Core.Domain.Messages;

public class EmailAccount : BaseEntity
{
    #region Constructure and Destructure

    public EmailAccount()
    {
        MessageTemplates = new HashSet<MessageTemplate>();
    }

    #endregion

    #region Public Properties

    public string Email { get; set; }

    public string? DisplayName { get; set; }

    public string Host { get; set; }

    public int Port { get; set; }

    public string Username { get; set; }

    public string Password { get; set; }

    public bool EnableSsl { get; set; }

    public bool Active { get; set; }

    public bool Deleted { get; set; }

    public ICollection<MessageTemplate> MessageTemplates { get; set; }

    #endregion
}
