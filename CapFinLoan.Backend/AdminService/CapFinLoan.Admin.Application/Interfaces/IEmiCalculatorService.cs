using CapFinLoan.Admin.Application.Contracts.Requests;
using CapFinLoan.Admin.Application.Contracts.Responses;

namespace CapFinLoan.Admin.Application.Interfaces;

public interface IEmiCalculatorService
{
    EmiCalculationResponse Calculate(EmiCalculationRequest request);
}
