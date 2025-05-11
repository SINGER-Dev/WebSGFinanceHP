using App.Model;
using App.Repositories.Interfaces;
using App.Services.Interfaces;
using Newtonsoft.Json;
using Serilog;
using System.Collections.Generic;

namespace App.Services.Implementations
{
    public class SearchService : ISearchService
    {
        private readonly HttpClient _httpClient;
        private readonly AppConfiguration _appSettings;
        private readonly IHttpContextAccessor _httpContextAccessor; // Add IHttpContextAccessor
        private readonly ISearchRepository _repository;
        public SearchService(AppConfiguration appSettings, IHttpContextAccessor httpContextAccessor, ISearchRepository repository, IHttpClientFactory httpClientFactory)
        {
            _repository = repository;
            _appSettings = appSettings;
            _httpContextAccessor = httpContextAccessor; // Initialize IHttpContextAccessor
            _httpClient = httpClientFactory.CreateClient("RetryClient");
        }

        public async Task<List<ApplicationResponeModel>> Search(ApplicationRq applicationRq)
        {


            List<ApplicationResponeModel> result = await _repository.Search(applicationRq);


            return result;
        }
    }
}
