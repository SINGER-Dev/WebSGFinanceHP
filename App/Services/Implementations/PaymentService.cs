using App.Model;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Serilog;
using System.Net.Http.Headers;
using System.Text;

namespace App.Services.Implementations
{
    public class PaymentService : IPaymentService
    {
        private readonly HttpClient client;
        private readonly IConfiguration _configuration;
        private readonly AppSettings _appSettings;
        private readonly IHttpContextAccessor _httpContextAccessor; // Add IHttpContextAccessor

        public PaymentService(IConfiguration configuration, AppSettings appSettings, IHttpContextAccessor httpContextAccessor)
        {
            _configuration = configuration;
            _appSettings = appSettings;
            _httpContextAccessor = httpContextAccessor; // Initialize IHttpContextAccessor
            client = new HttpClient();
        }

        public async Task<RegisIMEIRespone> LinkPayment([FromBody] GetApplication _GetApplication)
        {
            RegisIMEIRespone _RegisIMEIRespone = new RegisIMEIRespone();

            // Use _httpContextAccessor.HttpContext to access HttpContext
            var session = _httpContextAccessor.HttpContext?.Session;
            if (session != null)
            {
                Log.Debug("SGBCancel By " + session.GetString("EMP_CODE") + " | " + session.GetString("FullName") + " : " + JsonConvert.SerializeObject(_GetApplication));
            }

            var url = _appSettings.LinkPayment;

            var soapRequest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
            <soap12:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap12=""http://www.w3.org/2003/05/soap-envelope"">
              <soap12:Body>
                <GenLinkWithSms xmlns=""http://tempuri.org/"">
                  <AppCode>{_GetApplication.ApplicationCode}</AppCode>
                </GenLinkWithSms>
              </soap12:Body>
            </soap12:Envelope>";

            var content = new StringContent(soapRequest, Encoding.UTF8, "application/soap+xml");

            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/soap+xml"));

            try
            {
                var response = await client.PostAsync(url, content);
                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadAsStringAsync();

                _RegisIMEIRespone.statusCode = "PASS";
                Log.Debug(responseBody);
            }
            catch (Exception ex)
            {
                _RegisIMEIRespone.statusCode = ex.Message;
                Log.Error($"Error: {ex.Message}");
            }

            return _RegisIMEIRespone;
        }
    }
}
