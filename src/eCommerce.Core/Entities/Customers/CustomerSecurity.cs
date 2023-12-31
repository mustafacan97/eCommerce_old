﻿using eCommerce.Core.Primitives;

namespace eCommerce.Core.Entities.Customers;

public class CustomerSecurity : BaseEntity
{
    #region Constructure and Destructure

    public CustomerSecurity()
    {
    }

    #endregion

    #region Public Properties

    public Guid CustomerId { get; private set; }

    public string Password { get; set; }

    public string PasswordSalt { get; set; }

    public string? LastIpAddress { get; set; }

    public bool RequireReLogin { get; set; }

    public int FailedLoginAttempts { get; set; }

    public DateTime? CannotLoginUntilDateUtc { get; set; }

    #endregion

    #region Public Methods

    public void SetCustomerId(Guid customerId) => CustomerId = customerId; 

    #endregion
}
