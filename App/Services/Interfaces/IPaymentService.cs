using App.Model;
using Microsoft.AspNetCore.Mvc;

public interface IPaymentService
{
    Task<MessageReturn> LinkPayment([FromBody] GenEsignatureRq _GetApplication);
}