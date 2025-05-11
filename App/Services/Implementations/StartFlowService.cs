using App.Model;
using App.Repositories.Interfaces;
using App.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Newtonsoft.Json;
using Serilog;
using System.Net.Http;
using System.Text;

namespace App.Services.Implementations
{
    public class StartFlowService : IStartFlowService
    {
        private readonly HttpClient _httpClient;
        private readonly AppConfiguration _appSettings;
        private readonly IHttpContextAccessor _httpContextAccessor; // Add IHttpContextAccessor
        private readonly IPaymentRepository _repository;

        public StartFlowService(IHttpClientFactory httpClientFactory, AppConfiguration appSettings, IHttpContextAccessor httpContextAccessor)
        {
            _appSettings = appSettings;
            _httpContextAccessor = httpContextAccessor; // Initialize IHttpContextAccessor
            _httpClient = httpClientFactory.CreateClient("RetryClient");
        }

        public async Task<MessageReturn> StartFlow(StartFlowRq startFlowRq)
        {
            // Use _httpContextAccessor.HttpContext to access HttpContext
            var session = _httpContextAccessor.HttpContext?.Session;
            var empCode = session?.GetString("EMP_CODE") ?? "UNKNOWN";
            var fullName = session?.GetString("FullName") ?? "UNKNOWN";

            Log.Debug("OrderID: {OrderID} | Status: REQUEST | Desc: {Desc} | Type: {Type}", startFlowRq.RefCode, $"By {empCode} | {fullName} | {JsonConvert.SerializeObject(startFlowRq)}", "StartFlow");

            var messageReturn = new MessageReturn();

            try
            {
                var requestData = new
                {
                    ref_Code = startFlowRq.RefCode
                };

                var json = JsonConvert.SerializeObject(requestData);
                using var request = new HttpRequestMessage(HttpMethod.Post, _appSettings.WsLos+ "/v1/LOS/SGF_ReCreateApplication")
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                var response = await _httpClient.SendAsync(request);

                var responseContent = await response.Content.ReadAsStringAsync();
                var responseObj = JsonConvert.DeserializeObject<ApiStartFlowRp>(responseContent);
                
                if (response.IsSuccessStatusCode)
                {
                    messageReturn.StatusCode = "200";
                    messageReturn.Message = "PASS";
                    Log.Information("OrderID: {OrderID} | Status: SUCCESS | Desc: {Desc} | Type: {Type}", startFlowRq.RefCode, $"By {empCode} | {fullName} | {JsonConvert.SerializeObject(responseContent)}", "StartFlow");
                }
                else
                {
                    messageReturn.StatusCode = "500";
                    messageReturn.Message = responseObj.message;
                    Log.Error("OrderID: {OrderID} | Status: ERROR | Desc: {Desc} | Type: {Type}", startFlowRq.RefCode, $"By {empCode} | {fullName} | {responseObj.message}", "StartFlow");
                }
            }
            catch (Exception ex)
            {
                messageReturn.StatusCode = "500";
                messageReturn.Message = ex.Message;
                Log.Error("OrderID: {OrderID} | Status: ERROR | Desc: {Desc} | Type: {Type}", startFlowRq.RefCode, $"By {empCode} | {fullName} | {ex.Message}", "StartFlow");
            }

            return messageReturn;
        }
    }
}
