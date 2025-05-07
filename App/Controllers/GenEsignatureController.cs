using App.Model;
using Microsoft.AspNetCore.Mvc;

namespace App.Controllers
{
    public class GenEsignatureController : Controller
    {
        private readonly IGenEsignatureService _iGenEsignatureService;
        public GenEsignatureController(IGenEsignatureService iGenEsignatureService)
        {
            _iGenEsignatureService = iGenEsignatureService;
        }

        [HttpPost]
        public async Task<MessageReturn> GenEsignature([FromBody] GenEsignatureRq genEsignatureRq)
        {
            var result = new MessageReturn();
            result = await _iGenEsignatureService.ValidateGenEsignature(genEsignatureRq);
            return result;
        }

    }
}
