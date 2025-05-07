using App.Model;
using App.Services.Implementations;
using App.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace App.Controllers
{
    public class StartFlowController : Controller
    {
        private readonly IStartFlowService _iStartFlowService;
        public StartFlowController(IStartFlowService iStartFlowService)
        {
            _iStartFlowService = iStartFlowService;
        }

        [HttpPost]
        public async Task<MessageReturn> StartFlow([FromBody] StartFlowRq startFlowRq)
        {
            var result = new MessageReturn();
            result = await _iStartFlowService.StartFlow(startFlowRq);
            return result;
        }
    }
}
