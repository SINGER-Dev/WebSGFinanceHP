using App.Model;
using App.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace App.Controllers
{
    public class SearchController : Controller
    {
        private readonly ISearchService _iSearchService;
        public SearchController(ISearchService iSearchService)
        {
            _iSearchService = iSearchService;
        }

        [HttpPost]
        public async Task<ActionResult> Search(ApplicationRq applicationRq)
        {
            var result = new List<ApplicationResponeModel>();
            result = await _iSearchService.Search(applicationRq);
            return PartialView("_SearchResults", result);
        }
    }
}
