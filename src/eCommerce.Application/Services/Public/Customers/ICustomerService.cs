﻿using eCommerce.Application.Models.Customers;
using eCommerce.Core.Primitives;
using YerdenYuksek.Application.Models.Customers;
using YerdenYuksek.Core.Domain.Customers;

namespace YerdenYuksek.Application.Services.Public.Customers;

public partial interface ICustomerService
{
    #region Commands

    Task InsertCustomerAsync(Customer customer);

    Task<RegisterResponseModel> RegisterCustomerAsync(string email, string password);

    Task<Result> ValidateCustomerAsync(string email, string password);

    #endregion

    #region Queries

    Task<Customer?> GetCustomerByEmailAsync(string email, bool includeDeleted = false);

    string GetCustomerFullName(Customer customer);

    Task<CustomerRole?> GetCustomerRoleByNameAsync(string name);

    #endregion
}