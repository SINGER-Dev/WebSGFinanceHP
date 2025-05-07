using App.Model;
using App.Services.Implementations;
using Microsoft.AspNetCore.Mvc;

namespace App.Controllers
{
    public class GenLinkPaymentController : Controller
    {
        private readonly IPaymentService _iPaymentService;
        public GenLinkPaymentController(IPaymentService iPaymentService)
        {
            _iPaymentService = iPaymentService;
        }

        [HttpPost]
        public async Task<MessageReturn> LinkPayment([FromBody] GenEsignatureRq genEsignatureRq)
        {
            var result = new MessageReturn();
            result = await _iPaymentService.LinkPayment(genEsignatureRq);
            return result;
        }
    }
}
