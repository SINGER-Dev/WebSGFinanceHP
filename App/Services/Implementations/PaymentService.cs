using App.Model;
using App.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Serilog;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection.Emit;
using System.Text;

namespace App.Services.Implementations
{
    public class PaymentService : IPaymentService
    {
        private readonly HttpClient _httpClient;
        private readonly AppSettings _appSettings;
        private readonly IHttpContextAccessor _httpContextAccessor; // Add IHttpContextAccessor
        private readonly IPaymentRepository _repository;

        public PaymentService(IHttpClientFactory httpClientFactory, AppSettings appSettings, IHttpContextAccessor httpContextAccessor, IPaymentRepository repository)
        {
            _repository = repository;
            _appSettings = appSettings;
            _httpContextAccessor = httpContextAccessor; // Initialize IHttpContextAccessor
            _httpClient = httpClientFactory.CreateClient("RetryClient");
        }

        public async Task<MessageReturn> LinkPayment([FromBody] GenEsignatureRq genEsignatureRq)
        {
            MessageReturn result = new MessageReturn();
            var session = _httpContextAccessor.HttpContext?.Session;
            var empCode = session?.GetString("EMP_CODE") ?? "UNKNOWN";
            var fullName = session?.GetString("FullName") ?? "UNKNOWN";

            try
            {
                //เช็คสถานะใบคำขอ
                int StatusPayment = await _repository.CheckValidateStatusPayment(genEsignatureRq);
                if (StatusPayment <= 0)
                {
                    result.StatusCode = "500";
                    result.Message = "สถานะใบคำขอไม่ถูกต้อง กรุณาติดต่อเจ้าหน้าที่";
                    return result;
                }

                var requestData = new
                {
                    applicationCode = genEsignatureRq.ApplicationCode
                };

                var json = JsonConvert.SerializeObject(requestData);
                using var request = new HttpRequestMessage(HttpMethod.Post, _appSettings.WsLos+ "/v1/LOS/SGF_QrPayment")
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                var response = await _httpClient.SendAsync(request);

                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    result.StatusCode = "200";
                    result.Message = "PASS";
                    Log.Information("OrderID: {OrderID} | Status: SUCCESS | Desc: {Desc} | Type: {Type}", genEsignatureRq.ApplicationCode, $"By {empCode} | {fullName} | {JsonConvert.SerializeObject(responseContent)}", "StartFlow");
                }
                else
                {
                    result.StatusCode = "500";
                    result.Message = "SMS_SEND_FAILED";
                    Log.Error("OrderID: {OrderID} | Status: ERROR | Desc: {Desc} | Type: {Type}", genEsignatureRq.ApplicationCode, $"By {empCode} | {fullName} | {JsonConvert.SerializeObject(responseContent)}", "StartFlow");
                }
            }
            catch (Exception ex)
            {
                result.StatusCode = "500";
                result.Message = ex.Message;
                Log.Error("OrderID: {OrderID} | Status: ERROR | Desc: {Desc} | Type: {Type}", genEsignatureRq.ApplicationCode, $"By {empCode} | {fullName} | {ex.Message}", "StartFlow");
            }

            return result;
        }
    }
}
