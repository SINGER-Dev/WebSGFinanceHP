using App.Model;
using App.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Serilog;
using System.Net.Http;
using System.Net.Http.Headers;
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

            //เช็คสถานะใบคำขอ
            int StatusPayment = await _repository.CheckValidateStatusPayment(genEsignatureRq);
            if(StatusPayment <= 0)
            {
                result.StatusCode = "500";
                result.Message = "ใบคำขอไม่ได้อยู่ในสถานะ CLOSING กรุณาติดต่อเจ้าหน้าที่";
                return result;
            }

            // Use _httpContextAccessor.HttpContext to access HttpContext
            var session = _httpContextAccessor.HttpContext?.Session;
            var empCode = session?.GetString("EMP_CODE") ?? "UNKNOWN";
            var fullName = session?.GetString("FullName") ?? "UNKNOWN";

            Log.Debug("OrderID: {OrderID} | Status: REQUEST | Desc: {Desc} | Type: {Type}",
                genEsignatureRq.ApplicationCode,
                $"By {empCode} | {fullName} | {JsonConvert.SerializeObject(genEsignatureRq)}",
                "LinkPayment");


            var url = _appSettings.LinkPayment;

            var soapRequest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
            <soap12:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap12=""http://www.w3.org/2003/05/soap-envelope"">
              <soap12:Body>
                <GenLinkWithSms xmlns=""http://tempuri.org/"">
                  <AppCode>{genEsignatureRq.ApplicationCode}</AppCode>
                </GenLinkWithSms>
              </soap12:Body>
            </soap12:Envelope>";

            var content = new StringContent(soapRequest, Encoding.UTF8, "application/soap+xml");

            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/soap+xml"));

            try
            {
                var response = await _httpClient.PostAsync(url, content);
                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadAsStringAsync();

                result.StatusCode = "200";
                result.Message = "SUCCESS";
                Log.Debug(responseBody);
            }
            catch (Exception ex)
            {
                result.StatusCode = "500";
                result.Message = ex.Message;
                Log.Error($"Error: {ex.Message}");
            }

            return result;
        }
    }
}
