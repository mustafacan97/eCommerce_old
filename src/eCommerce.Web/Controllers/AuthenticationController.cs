﻿using eCommerce.Application.Features.Commands.Customers.InsertCustomer;
using eCommerce.Application.Features.Queries.Customers.GetCustomerByEmail;
using eCommerce.Core.Primitives;
using eCommerce.Core.Services.Customers;
using eCommerce.Core.Services.Security;
using eCommerce.Web.ViewModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eCommerce.Web.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class AuthenticationController : Controller
{
    #region Fields

    private readonly ISender _sender;

    private readonly ICustomerService _customerService;

    private readonly IJwtService _jwtService;

    #endregion

    #region Constructure and Destructure

    public AuthenticationController(
        ICustomerService customerService,
        IJwtService jwtService,
        ISender sender)
    {
        _customerService = customerService;
        _jwtService = jwtService;
        _sender = sender;
    }

    #endregion

    #region Methods

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(RegisterRequestModel model)
    {
        var checkCustomer = await _sender.Send(GetCustomerByEmailQuery.Create(model.Email));

        if (checkCustomer.Value is not null)
        {
            return Ok(Result.Failure(Error.Conflict(description: "Email is already registered!")));
        }

        var registrationResult = await _sender.Send(InsertCustomerCommand.Create(model.Email, model.Password));

        return Ok(registrationResult);
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Login([FromBody] LoginRequestModel model)
    {
        if (!ModelState.IsValid)
        {
            var result = Result.Failure(
                ModelState.Values
                    .SelectMany(q => q.Errors)
                    .Select(x => Error.Validation(description: x.ErrorMessage))
                    .ToArray());

            return BadRequest(result);
        }

        var loginResult = await _customerService.ValidateCustomerAsync(model.Email, model.Password);
        if (!loginResult.IsSuccess)
        {
            return Ok(loginResult);
        }

        var token = _jwtService.GenerateJwtToken(model.Email);
        if (token is null)
        {
            return Ok(Result.Failure(Error.Unexpected(description: "Unexpected error occurred!")));
        }

        return Ok(token);
    }

    #endregion
}
