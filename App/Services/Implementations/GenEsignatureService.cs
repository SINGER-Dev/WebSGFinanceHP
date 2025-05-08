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
        private readonly AppSettings _appSettings;
        private readonly IHttpContextAccessor _httpContextAccessor; // Add IHttpContextAccessor
        private readonly IGenEsignatureRepository _repository;
        public GenEsignatureService(AppSettings appSettings, IHttpContextAccessor httpContextAccessor, IGenEsignatureRepository repository, IHttpClientFactory httpClientFactory)
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
                int CheckDataHeaderNum = await _repository.CheckDataHeader(genEsignatureRq);
                if(CheckDataHeaderNum <= 0)
                {
                    //หากจ่ายเงินแล้ว แต่ยังไม่มีข้อมูลใน AUTO_SALE_POS_HEADER ให้ทำการ Gen ข้อมูลก่อน
                    await GenAutoSaleHeader(genEsignatureRq);
                }

                int MapingOrderAccountNum = await _repository.MapingOrderAccount(genEsignatureRq);
                if (MapingOrderAccountNum > 0)
                {
                    ContractRp contractRp = await _repository.Contract(genEsignatureRq);
                    if(contractRp.IsExist > 0)
                    {
                        //กรณีไม่ได้รับลิงค์สัญญาจริงๆ แต่ข้อมูล Gen ครบหมดแล้ว
                        result = await GenEsignature(genEsignatureRq);
                        if(result.Message?.ToUpper() == "SUCCESS.")
                        {
                            result.Message = "ส่งค์ลิงค์ลงนามเรียบร้อย โปรดตรวจสอบลิงค์ลงนามอีกครั้ง";
                        }
                        else
                        {
                            //กรณีจองรายยการไม่สำเร็จ จะไม่ส่งลิงค์ลงนามให้ทำการแก้ไขก่อน
                            //มีมัญญาแล้วแต่ SMS ไม่ส่ง เพื่อทำการแก้ไข หลังจากนั้นส่ง Esig อีกครั้ง
                            await TryGenEsignatureWithFix(genEsignatureRq, contractRp, result.Message);

                        }
                    }
                    else
                    {
                        //กรณีได้รับลิงค์สัญญา แต่ข้อมูล Gen ไม่ครบ
                        await _repository.GenContract(genEsignatureRq);
                        result.StatusCode = "200";
                        result.Message = "แจ้งลูกค้าให้ทำการ Refresh หน้าทำสัญญาอีกครั้ง เพื่อลงนาม";
                    }   
                
                }
                else
                {
                    //กรณีไม่ได้รับทั้งลิงค์สัญญาและ ยังไม่ได้ Gen ข้อมูล Contract
                    result = await GenEsignature(genEsignatureRq);
                    if (result.Message?.ToUpper() == "SUCCESS.")
                    {
                        result.Message = "ส่งค์ลิงค์ลงนามเรียบร้อย โปรดตรวจสอบลิงค์ลงนามอีกครั้ง";
                    }
                    else
                    {
                        //หากได้รับ respone กลับมา error จะมีสัญญาแล้ว แต่ ให้มาเช็คปัญหาอีกรอบ เพื่อทำการแก้ไขอัตโนมัติ
                        ContractRp contractRp = await _repository.Contract(genEsignatureRq);
                        if (contractRp.IsExist > 0)
                        {
                            //กรณีไม่ได้รับลิงค์สัญญาจริงๆ แต่ข้อมูล Gen ครบหมดแล้ว
                            result = await GenEsignature(genEsignatureRq);
                            if (result.Message?.ToUpper() == "SUCCESS.")
                            {
                                result.Message = "ส่งค์ลิงค์ลงนามเรียบร้อย โปรดตรวจสอบลิงค์ลงนามอีกครั้ง";
                            }
                            else
                            {
                                //กรณีจองรายยการไม่สำเร็จ จะไม่ส่งลิงค์ลงนามให้ทำการแก้ไขก่อน
                                //มีมัญญาแล้วแต่ SMS ไม่ส่ง เพื่อทำการแก้ไข หลังจากนั้นส่ง Esig อีกครั้ง
                                await TryGenEsignatureWithFix(genEsignatureRq, contractRp, result.Message);
                            }
                        }
                        else
                        {
                            //กรณีได้รับลิงค์สัญญา แต่ข้อมูล Gen ไม่ครบ
                            await _repository.GenContract(genEsignatureRq);
                            result.StatusCode = "200";
                            result.Message = "แจ้งลูกค้าให้ทำการ Refresh หน้าทำสัญญาอีกครั้ง เพื่อลงนาม";
                        }

                    }
                }
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
