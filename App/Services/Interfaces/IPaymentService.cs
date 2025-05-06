using App.Model;
using Microsoft.AspNetCore.Mvc;

public interface IPaymentService
{
    Task<RegisIMEIRespone> LinkPayment([FromBody] GetApplication _GetApplication);
}