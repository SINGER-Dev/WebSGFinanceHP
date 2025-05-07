using App.Model;
using App.Repositories.Interfaces;
using App.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Serilog;
using System.Net.Http;
using System.Text;

namespace App.Services.Implementations
{
    public class StartFlowService : IStartFlowService
    {
        private readonly HttpClient _httpClient;
        private readonly AppSettings _appSettings;
        private readonly IHttpContextAccessor _httpContextAccessor; // Add IHttpContextAccessor
        private readonly IPaymentRepository _repository;

        public StartFlowService(IHttpClientFactory httpClientFactory, AppSettings appSettings, IHttpContextAccessor httpContextAccessor)
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

            Log.Debug("OrderID: {OrderID} | Status: REQUEST | Desc: {Desc} | Type: {Type}", startFlowRq.ApplicationCode, $"By {empCode} | {fullName} | {JsonConvert.SerializeObject(startFlowRq)}", "StartFlow");

            var messageReturn = new MessageReturn();
            messageReturn.StatusCode = "200";
            messageReturn.Message = "SUCCESS";
            /*try
            {
                var requestData = new
                {
                    totel = modifiedNumber,
                    smsmsg = message,
                    company = smsModelRequest.Company
                };

                var json = JsonConvert.SerializeObject(requestData);
                using var request = new HttpRequestMessage(HttpMethod.Post, _appSettings.SMSURL)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                request.Headers.Add("apikey", _appSettings.SMSToken);

                var response = await _httpClient.SendAsync(request);

                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    messageReturn.StatusCode = "200";
                    messageReturn.Message = "PASS";
                    Log.Information("OrderID: {OrderID} | Status: SUCCESS | Desc: {Desc} | Type: {Type}", startFlowRq.ApplicationCode, $"By {empCode} | {fullName} | {JsonConvert.SerializeObject(responseContent)}", "StartFlow");
                }
                else
                {
                    messageReturn.StatusCode = "500";
                    messageReturn.Message = "SMS_SEND_FAILED";
                    Log.Error("OrderID: {OrderID} | Status: ERROR | Desc: {Desc} | Type: {Type}", startFlowRq.ApplicationCode, $"By {empCode} | {fullName} | {JsonConvert.SerializeObject(responseContent)}", "StartFlow");
                }
            }
            catch (Exception ex)
            {
                messageReturn.StatusCode = "500";
                messageReturn.Message = ex.Message;
                Log.Error("OrderID: {OrderID} | Status: ERROR | Desc: {Desc} | Type: {Type}", startFlowRq.ApplicationCode, $"By {empCode} | {fullName} | {ex.Message}", "StartFlow");
            }*/

            return messageReturn;
        }
    }
}
