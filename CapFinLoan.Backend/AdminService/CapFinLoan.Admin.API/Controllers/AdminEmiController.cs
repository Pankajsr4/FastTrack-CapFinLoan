using CapFinLoan.Admin.Application.Contracts.Requests;
using CapFinLoan.Admin.Application.Interfaces;
using CapFinLoan.Admin.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapFinLoan.Admin.API.Controllers;

[ApiController]
[Route("api/admin/emi")]
[Authorize(Roles = RoleNames.Admin)]
public class AdminEmiController : ControllerBase
{
    private readonly IEmiCalculatorService _emiCalculator;

    public AdminEmiController(IEmiCalculatorService emiCalculator)
    {
        _emiCalculator = emiCalculator;
    }

    /// <summary>
    /// Calculates EMI, total payment, and total interest for a loan.
    /// </summary>
    /// <remarks>
    /// Formula: EMI = [P × R × (1+R)^N] / [(1+R)^N – 1]
    /// where R = annual interest rate / 12 / 100
    /// </remarks>
    [HttpPost("calculate")]
    public IActionResult Calculate([FromBody] EmiCalculationRequest request)
    {
        // FluentValidation runs automatically via AddFluentValidationAutoValidation.
        // If validation fails, ASP.NET returns 400 before this method is called.
        var result = _emiCalculator.Calculate(request);
        return Ok(result);
    }
}
