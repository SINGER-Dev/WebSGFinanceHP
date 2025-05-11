using App.Model;
using Azure.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using Serilog;
using System.Data;
using System.Diagnostics.Contracts;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace App.Services.Implementations
{
    public class GenEsignatureService : IGenEsignatureService
    {
        private readonly HttpClient _httpClient;
        private readonly AppConfiguration _appSettings;
        private readonly IHttpContextAccessor _httpContextAccessor; // Add IHttpContextAccessor
        private readonly IGenEsignatureRepository _repository;
        public GenEsignatureService(AppConfiguration appSettings, IHttpContextAccessor httpContextAccessor, IGenEsignatureRepository repository, IHttpClientFactory httpClientFactory)
        {
            _repository = repository;
            _appSettings = appSettings;
            _httpContextAccessor = httpContextAccessor; // Initialize IHttpContextAccessor
            _httpClient = httpClientFactory.CreateClient("RetryClient");
        }

        public async Task<MessageReturn> ValidateGenEsignature([FromBody] GenEsignatureRq genEsignatureRq)
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            Log.Debug("OrderID: {OrderID} | Status: REQUEST | Desc: {Desc} | Type: {Type}", genEsignatureRq.ApplicationCode, "By " + session.GetString("EMP_CODE") + " | " + session.GetString("FullName") + JsonConvert.SerializeObject(genEsignatureRq), "ValidateGenEsignature");

            MessageReturn result = new MessageReturn();
            try
            {
                //เช็คยอดเงิน
                int CheckPaymentNum = await _repository.CheckPayment(genEsignatureRq);
                if (CheckPaymentNum <= 0)
                {
                    result.StatusCode = "500";
                    result.Message = "การจ่ายเงินยังไม่สมบูรณ์";
                    return result;
                }

                //เช็ค AUTO_SALE_POS_HEADER มีข้่อมูลหรือยัง
                CheckDataHeaderRp checkDataHeaderRp = await _repository.CheckDataHeader(genEsignatureRq);
                if(checkDataHeaderRp.IsExist <= 0)
                {
                    //หากจ่ายเงินแล้ว แต่ยังไม่มีข้อมูลใน AUTO_SALE_POS_HEADER ให้ทำการ Gen ข้อมูลก่อน
                    await GenAutoSaleHeader(genEsignatureRq);
                }

                //เช็คว่าสร้างสัญญาหรือยัง
                ContractRp contractRp = await _repository.Contract(genEsignatureRq);
                if (contractRp.IsExist <= 0)
                {
                    await _repository.GenContract(genEsignatureRq);
                }

                //เช็ค ACCOUNT CONTRACTNO ที่ HEADER ยังไม่มีข้อมูลให้อัพเดท
                if (checkDataHeaderRp.AccountNo == "")
                {
                    //ให้ทำการ Update Contract และ AccountNo ที่ AUTO_SALE_HEADERR
                    UpDateContractHeaderRq upDateContractHeaderRq = new UpDateContractHeaderRq();
                    upDateContractHeaderRq.AccountNo = contractRp.AccountNo;
                    upDateContractHeaderRq.ApplicationCode = genEsignatureRq.ApplicationCode;
                    await _repository.UpDateContractHeader(upDateContractHeaderRq);
                    await GenAutoSaleHeader(genEsignatureRq);
                }

                //หากยังไม่ได้จอง
                if (checkDataHeaderRp.PosTrackNumber == "")
                {
                    //ให้ทำการ Update Contract และ AccountNo ที่ AUTO_SALE_HEADERR
                    TransferStockRq transferStockRq = new TransferStockRq();
                    transferStockRq.AppOrderNo = genEsignatureRq.ApplicationCode;
                    MessageReturn messageReturn = await TransferStock(transferStockRq);
                    if(messageReturn.StatusCode != "200")
                    {
                        result.StatusCode = "500";
                        result.Message = messageReturn.Message;
                        return result;
                    }
                }

                //กรณีไม่ได้รับลิงค์สัญญาจริงๆ แต่ข้อมูล Gen ครบหมดแล้ว แต่ถ้าไม่ใช่ STL เช็๕ว่ามีการสร้างลิงค์ยัง สร้างแล้วแต่ลงนามไม่ได้ให้ gencontract แล้ว แจ้งเขาได้เลย 
                result = await GenEsignature(genEsignatureRq);
                if (result.Message?.ToUpper() != "SUCCESS.")
                {
                    result.StatusCode = "500";
                    result.Message = result.Message;
                    return result;
                }

                result.StatusCode = "200";
                result.Message = "ส่งค์ลิงค์ลงนามเรียบร้อย โปรดตรวจสอบลิงค์ลงนามอีกครั้ง";
            }
            catch (SqlException ex)
            {
                result.StatusCode = "500";
                result.Message = "DB Error";
                Log.Error(ex.Message,"OrderID: {OrderID} | Status: ERROR | Desc: {Desc} | Type: {Type}", genEsignatureRq.ApplicationCode, "By " + session.GetString("EMP_CODE") + " | " + session.GetString("FullName") + ex.Message, "ValidateGenEsignature");

            }
            catch (Exception ex)
            {
                result.StatusCode = "500";
                result.Message = ex.Message;
                Log.Error(ex.Message,"OrderID: {OrderID} | Status: ERROR | Desc: {Desc} | Type: {Type}", genEsignatureRq.ApplicationCode, "By " + session.GetString("EMP_CODE") + " | " + session.GetString("FullName") + ex.Message, "ValidateGenEsignature");
            }

            Log.Information("OrderID: {OrderID} | Status: RESPONE | Desc: {Desc} | Type: {Type}", genEsignatureRq.ApplicationCode, "By " + session.GetString("EMP_CODE") + " | " + session.GetString("FullName") + JsonConvert.SerializeObject(result), "ValidateGenEsignature");

            return result;
        }
        public async Task<MessageReturn> ValidatePOS(ValidatePOSRq validatePOSRq)
        {
            MessageReturn result = new MessageReturn();
            if (validatePOSRq.MessageError.Contains("Contract NO. : is invalid", StringComparison.OrdinalIgnoreCase))
            {
                //ให้ทำการ Update Contract และ AccountNo ที่ AUTO_SALE_HEADERR
                UpDateContractHeaderRq upDateContractHeaderRq = new UpDateContractHeaderRq();
                upDateContractHeaderRq.AccountNo = validatePOSRq.AccountNo;
                upDateContractHeaderRq.ApplicationCode = validatePOSRq.ApplicationCode;
                await _repository.UpDateContractHeader(upDateContractHeaderRq);
            }

            return result;
        }
        private async Task<MessageReturn> TryGenEsignatureWithFix(GenEsignatureRq rq, ContractRp contractRp, string previousError)
        {
            var validatePOSRq = new ValidatePOSRq
            {
                ApplicationCode = rq.ApplicationCode,
                AccountNo = contractRp.AccountNo,
                MessageError = previousError
            };
            await ValidatePOS(validatePOSRq);

            var result = await GenEsignature(rq);
            if (result.Message?.ToUpper() == "SUCCESS.")
            {
                result.Message = "ส่งค์ลิงค์ลงนามเรียบร้อย โปรดตรวจสอบลิงค์ลงนามอีกครั้ง";
            }

            return result;
        }
        public async Task<MessageReturn> GenEsignature(GenEsignatureRq genEsignatureRq)
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            var empCode = session?.GetString("EMP_CODE") ?? "UNKNOWN";
            var fullName = session?.GetString("FullName") ?? "UNKNOWN";

            Log.Debug("OrderID: {OrderID} | Status: REQUEST | Desc: {Desc} | Type: {Type}",
                genEsignatureRq.ApplicationCode,
                $"By {empCode} | {fullName} | {JsonConvert.SerializeObject(genEsignatureRq)}",
                "GenEsignature");

            var result = new MessageReturn();

            try
            {
                var requestBody = new
                {
                    APPLICATION_CODE = genEsignatureRq.ApplicationCode
                };

                string jsonBody = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Remove("apikey"); // กันค่าเดิมซ้ำซ้อน
                _httpClient.DefaultRequestHeaders.Add("apikey", _appSettings.Apikey);

                var url = $"{_appSettings.SGAPIESIG}/sgesig/api/v2/GenEsignature";
                var response = await _httpClient.PostAsync(url, content);

                var responseContent = await response.Content.ReadAsStringAsync();
                Log.Debug("API RESPONSE : {Response}", responseContent);

                if (response.IsSuccessStatusCode)
                {
                    result = JsonConvert.DeserializeObject<MessageReturn>(responseContent) ?? new MessageReturn();
                }
                else
                {
                    result.StatusCode = ((int)response.StatusCode).ToString();
                    result.Message = "Error calling GenEsignature API";
                }
            }
            catch (Exception ex)
            {
                result.StatusCode = "500";
                result.Message = ex.Message;
                Log.Error(ex, "GenEsignature Exception");
            }

            Log.Debug("RETURN : {Result}", JsonConvert.SerializeObject(result));
            return result;
        }
        public async Task<MessageReturn> TransferStock(TransferStockRq transferStockRq)
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            var empCode = session?.GetString("EMP_CODE") ?? "UNKNOWN";
            var fullName = session?.GetString("FullName") ?? "UNKNOWN";

            Log.Debug("OrderID: {OrderID} | Status: REQUEST | Desc: {Desc} | Type: {Type}",
                transferStockRq.AppOrderNo,
                $"By {empCode} | {fullName} | {JsonConvert.SerializeObject(transferStockRq)}",
                "GenEsignature");

            var result = new MessageReturn();

            try
            {

                string jsonBody = JsonConvert.SerializeObject(transferStockRq);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Remove("apikey"); // กันค่าเดิมซ้ำซ้อน
                _httpClient.DefaultRequestHeaders.Add("apikey", _appSettings.WSAUTOSALE_KEY);

                var url = $"{_appSettings.WSAUTOSALE}/autosale/v1/TransferStock";
                var response = await _httpClient.PostAsync(url, content);

                var responseContent = await response.Content.ReadAsStringAsync();
                Log.Debug("API RESPONSE : {Response}", responseContent);

                if (response.IsSuccessStatusCode)
                {
                    result = JsonConvert.DeserializeObject<MessageReturn>(responseContent) ?? new MessageReturn();
                }
                else
                {
                    result.StatusCode = ((int)response.StatusCode).ToString();
                    result.Message = "Error calling GenEsignature API";
                }
            }
            catch (Exception ex)
            {
                result.StatusCode = "500";
                result.Message = ex.Message;
                Log.Error(ex, "GenEsignature Exception");
            }

            Log.Debug("RETURN : {Result}", JsonConvert.SerializeObject(result));
            return result;
        }
        public async Task<MessageReturn> GenAutoSaleHeader(GenEsignatureRq genEsignatureRq)
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            var empCode = session?.GetString("EMP_CODE") ?? "UNKNOWN";
            var fullName = session?.GetString("FullName") ?? "UNKNOWN";

            Log.Debug("OrderID: {OrderID} | Status: REQUEST | Desc: {Desc} | Type: {Type}",
                genEsignatureRq.ApplicationCode,
                $"By {empCode} | {fullName} | {JsonConvert.SerializeObject(genEsignatureRq)}",
                "GenAutoSaleHeader");

            var result = new MessageReturn();

            try
            {
                var requestBody = new
                {
                    applicationCode = genEsignatureRq.ApplicationCode
                };

                string jsonBody = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Remove("apikey"); // กันค่าเดิมซ้ำซ้อน
                _httpClient.DefaultRequestHeaders.Add("apikey", _appSettings.Apikey);

                var url = $"{_appSettings.WsLos}/v1/LOS/SGF_ReCreateHeader'";
                var response = await _httpClient.PostAsync(url, content);

                var responseContent = await response.Content.ReadAsStringAsync();
                Log.Debug("API RESPONSE : {Response}", responseContent);

                if (response.IsSuccessStatusCode)
                {
                    result.StatusCode = "200";
                    result.Message = "PASS";
                    Log.Information("OrderID: {OrderID} | Status: SUCCESS | Desc: {Desc} | Type: {Type}", genEsignatureRq.ApplicationCode, $"By {empCode} | {fullName} | {JsonConvert.SerializeObject(responseContent)}", "GenAutoSaleHeader");
                }
                else
                {
                    result.StatusCode = "500";
                    result.Message = "SMS_SEND_FAILED";
                    Log.Error("OrderID: {OrderID} | Status: ERROR | Desc: {Desc} | Type: {Type}", genEsignatureRq.ApplicationCode, $"By {empCode} | {fullName} | {JsonConvert.SerializeObject(responseContent)}", "GenAutoSaleHeader");
                }
            }
            catch (Exception ex)
            {
                result.StatusCode = "500";
                result.Message = ex.Message;
                Log.Error(ex, "GenAutoSaleHeader Exception");
            }

            Log.Debug("RETURN : {Result}", JsonConvert.SerializeObject(result));
            return result;
        }
    }
}
