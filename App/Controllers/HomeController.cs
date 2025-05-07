using App.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Diagnostics;
using System.Net;
using System.Xml;
using Serilog;
using Newtonsoft.Json;
using System.Text;
using RestSharp;
using System.Globalization;
using Dapper;


namespace App.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppSettings _appSettings;
      
        public HomeController(ILogger<HomeController> logger, AppSettings appSettings)
        {
            _appSettings = appSettings;
        }

        public IActionResult Index()
        {
            var EMP_CODE = HttpContext.Session.GetString("EMP_CODE");
            if (EMP_CODE == null)
            {
                return Redirect("/Login");
            }
            ViewBag.EMP_CODE = HttpContext.Session.GetString("EMP_CODE");
            ViewBag.FullName = HttpContext.Session.GetString("FullName");


            using (var connection = new SqlConnection(_appSettings.strConnString3))
            {

                string sql = @"
                SELECT [AREA_CODE]
                     ,[AREA_NAME_THA]
                     ,[AREA_NAME_ENG]
                FROM [SG-MASTER].[dbo].[MS_AREA]
                WHERE ISNULL(AREA_NAME_THA,'') <> ''
                ORDER BY AREA_NAME_THA ASC";

                var MsAreas = connection.Query<MsArea>(sql);
                return View(MsAreas);
            }

        }

        [HttpPost]
        public async Task<MsDepartmentViewModel> GetMsDepartment([FromBody] GetMsDepartment _GetMsDepartment)
        {
            using (var connection = new SqlConnection(_appSettings.strConnString3))
            {
                string sql = @" 
                SELECT [DEP_CODE]
                  ,[DEP_NAME_THA]
                FROM [SG-MASTER].[dbo].[MS_DEPARTMENT]
                WHERE AREA_CODE = @area
                AND STATUS_OPEN = 0
                ORDER BY DEP_NAME_THA ASC";

                var parameters = new
                {
                    area = _GetMsDepartment.area
                };

                var msMsDepartment = connection.Query<MsDepartment>(sql, parameters);

                var viewModel = new MsDepartmentViewModel
                {
                    MsDepartment = msMsDepartment.ToList()
                };

                return viewModel;
            }

        }

        
        
        public IActionResult GetApplicationHistory()
        {
            var EMP_CODE = HttpContext.Session.GetString("EMP_CODE");
            if (EMP_CODE == null)
            {
                return Redirect("/Login");
            }

            ViewBag.EMP_CODE = HttpContext.Session.GetString("EMP_CODE");
            ViewBag.FullName = HttpContext.Session.GetString("FullName");
            return View();
        }

        public async Task<IActionResult> FormCancel(string ApplicationCode)
        {
            var EMP_CODE = HttpContext.Session.GetString("EMP_CODE");
            if (EMP_CODE == null)
            {
                return Redirect("/Login");
            }

            ViewBag.EMP_CODE = HttpContext.Session.GetString("EMP_CODE");
            ViewBag.FullName = HttpContext.Session.GetString("FullName");

            FormCancelModel formCancelModel = new FormCancelModel();
            formCancelModel.ApplicationCode = ApplicationCode;

            GetApplication _GetApplication = new GetApplication();
            _GetApplication.ApplicationCode = ApplicationCode;
            GetApplicationRespone _GetApplicationRespone = await GetApplication(_GetApplication);


            formCancelModel.AccountNo = _GetApplicationRespone.AccountNo;
            formCancelModel.ApplicationCode = _GetApplicationRespone.ApplicationCode;
            formCancelModel.SaleDepCode = _GetApplicationRespone.SaleDepCode;
            formCancelModel.SaleDepName = _GetApplicationRespone.SaleDepName;
            formCancelModel.ProductModelName = _GetApplicationRespone.ProductModelName;
            formCancelModel.ProductSerialNo = _GetApplicationRespone.ProductSerialNo;
            formCancelModel.ApplicationStatusID = _GetApplicationRespone.ApplicationStatusID;

            formCancelModel.CustomerID = _GetApplicationRespone.CustomerID;
            formCancelModel.Cusname = _GetApplicationRespone.Cusname;
            formCancelModel.cusMobile = _GetApplicationRespone.cusMobile;
            formCancelModel.SaleName = _GetApplicationRespone.SaleName;
            formCancelModel.SaleTelephoneNo = _GetApplicationRespone.SaleTelephoneNo;

            return View(formCancelModel);
        }

        [HttpPost]
        public ActionResult SearchGetApplicationHistory(SearchGetApplicationHistory _SearchGetApplicationHistory)
        {
            Log.Debug(JsonConvert.SerializeObject(_SearchGetApplicationHistory));

            List<SearchGetApplicationHistoryRespone> _SearchGetApplicationHistoryResponeMaster = new List<SearchGetApplicationHistoryRespone>();
            SqlConnection connection = new SqlConnection();
            connection.ConnectionString = _appSettings.strConnString;
            try
            {
                SqlCommand sqlCommand;
                string strSQL = _appSettings.DATABASEK2 + ".[GetApplicationHistory]";
                sqlCommand = new SqlCommand(strSQL, connection);
                sqlCommand.CommandType = CommandType.StoredProcedure;
                sqlCommand.Parameters.AddWithValue("AccountNo", _SearchGetApplicationHistory.AccountNo);
                sqlCommand.Parameters.AddWithValue("ApplicationCode", _SearchGetApplicationHistory.ApplicationCode);
                sqlCommand.Parameters.AddWithValue("startdate", _SearchGetApplicationHistory.startdate);
                sqlCommand.Parameters.AddWithValue("enddate", _SearchGetApplicationHistory.enddate);

                SqlDataAdapter dtAdapter = new SqlDataAdapter();
                dtAdapter.SelectCommand = sqlCommand;
                DataTable dt = new DataTable();
                dtAdapter.Fill(dt);
                connection.Close();
                if (dt.Rows.Count > 0)
                {

                    foreach (DataRow row in dt.Rows)
                    {
                        SearchGetApplicationHistoryRespone _SearchGetApplicationHistoryRespone = new SearchGetApplicationHistoryRespone();

                        _SearchGetApplicationHistoryRespone.ApplicationCode = row["ApplicationCode"].ToString();
                        _SearchGetApplicationHistoryRespone.AccountNo = row["AccountNo"].ToString();
                        _SearchGetApplicationHistoryRespone.ProductSerialNo = row["ProductSerialNo"].ToString();
                        _SearchGetApplicationHistoryRespone.ProductModelName = row["ProductModelName"].ToString();
                        _SearchGetApplicationHistoryRespone.ApplicationRemark = row["ApplicationRemark"].ToString();
                        _SearchGetApplicationHistoryRespone.CreateDate = row["CreateDate"].ToString();
                        _SearchGetApplicationHistoryRespone.CreateBy = row["CreateBy"].ToString();
                        _SearchGetApplicationHistoryRespone.SaleDepName = row["SaleDepName"].ToString();
                        _SearchGetApplicationHistoryRespone.SaleDepCode = row["SaleDepCode"].ToString();
                        _SearchGetApplicationHistoryRespone.CustomerID = row["CustomerID"].ToString();
                        _SearchGetApplicationHistoryRespone.cusMobile = row["cusMobile"].ToString();
                        _SearchGetApplicationHistoryRespone.Cusname = row["Cusname"].ToString();
                        _SearchGetApplicationHistoryRespone.ApplicationStatusID = row["ApplicationStatusID"].ToString();
                        _SearchGetApplicationHistoryResponeMaster.Add(_SearchGetApplicationHistoryRespone);
                    }
                }

                Log.Debug(JsonConvert.SerializeObject(_SearchGetApplicationHistoryResponeMaster));

                sqlCommand.Parameters.Clear();
                
            }
            catch (Exception ex)
            {
                Log.Debug(ex.Message);
            }
            return PartialView("_SearchGetApplicationHistory", _SearchGetApplicationHistoryResponeMaster);
        }

        
        // Dummy method to simulate search operation
        [HttpPost]
        public async Task<string> UpdateDataCancel(FormConfirmModel _FormConfirmModel)
        {
            string ResultDescription = "";
            try
            {


                // Define the start and end times for the period (8:00 AM - 10:00 PM)
                TimeSpan periodStart = new TimeSpan(8, 0, 0); // 8:00 AM
                TimeSpan periodEnd = new TimeSpan(22, 0, 0); // 10:00 PM

                // Example time to check
                DateTime now = DateTime.Now;
                TimeSpan currentTime = now.TimeOfDay;

                // Check if the current time is within the period
                bool isWithinPeriod = currentTime >= periodStart && currentTime <= periodEnd;

                //if (isWithinPeriod)
                //{
                    SqlConnection connection = new SqlConnection();
                    connection.ConnectionString = _appSettings.strConnString;
                    connection.Open();

                    GetApplication _GetApplication = new GetApplication();
                    _GetApplication.ApplicationCode = _FormConfirmModel.ApplicationCode;
                    GetApplicationRespone _GetApplicationRespone = await GetApplication(_GetApplication);

                    //Cancel Application
                    //CCOWebServiceModel _CCOWebService = new CCOWebServiceModel();
                    //_CCOWebService.id = _GetApplicationRespone.ApplicationID;
                    //MessageModel _MessageModel = await CCOWebService(_CCOWebService);

                    //Cancel EZ Tax
                    //GetTokenEZTaxRp _GetTokenEZTaxRp = await GetTokenEZTax();

                    //Cancel econtract


                    SqlCommand sqlCommand;
                    string strSQL = _appSettings.DATABASEK2 + ".[CancelApplication]";
                    sqlCommand = new SqlCommand(strSQL, connection);
                    sqlCommand.CommandType = CommandType.StoredProcedure;
                    sqlCommand.Parameters.AddWithValue("ApplicationCode", _GetApplicationRespone.ApplicationCode);
                    sqlCommand.Parameters.AddWithValue("Remark", _FormConfirmModel.Remark + "" + _FormConfirmModel.Other);
                    sqlCommand.Parameters.AddWithValue("CANCEL_USER", HttpContext.Session.GetString("EMP_CODE"));
                    sqlCommand.Parameters.AddWithValue("Except_IMEI", _FormConfirmModel.ExceptIMEI);
                    sqlCommand.Parameters.AddWithValue("Except_CUST", _FormConfirmModel.ExceptCus);

                    SqlDataAdapter dtAdapter = new SqlDataAdapter();
                    dtAdapter.SelectCommand = sqlCommand;
                    DataTable dt = new DataTable();
                    dtAdapter.Fill(dt);
                    connection.Close();
                    if (dt.Rows.Count > 0)
                    {
                        if ("SUCCESS" != dt.Rows[0]["Result"].ToString().ToUpper())
                        {
                            ResultDescription += _GetApplicationRespone.AccountNo + " " + dt.Rows[0]["ResultDescription"].ToString();
                        }
                    }
                    sqlCommand.Parameters.Clear();

                    if (ResultDescription == "")
                    {
                        string currentDateTime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                        var requestBody = new
                        {
                            applicationCode = _GetApplicationRespone.ApplicationCode,
                            applicationStatus = "CANCELLED",
                            approvalStatus = "CANCELLED",
                            approvalDatetime = currentDateTime,
                            remark = _FormConfirmModel.Remark + "" + _FormConfirmModel.Other
                        };

                        Log.Debug("API BODY REQUEST : " + JsonConvert.SerializeObject(requestBody));

                        using (HttpClient client = new HttpClient())
                        {
                            string jsonBody = JsonConvert.SerializeObject(requestBody);

                            client.DefaultRequestHeaders.Add("apikey", _appSettings.Apikey);
                            client.DefaultRequestHeaders.Add("user", "DEV");

                            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                            HttpResponseMessage responseDevice = await client.PostAsync(_appSettings.SGAPIESIG + "/sgesig/Service/C100_Status", content);
                            int DeviceStatusCode = (int)responseDevice.StatusCode;

                            Log.Debug("API BODY RESPONE : " + JsonConvert.SerializeObject(responseDevice.Content.ReadAsStringAsync()));

                            if (responseDevice.IsSuccessStatusCode)
                            {
                                var jsonResponseDevice = await responseDevice.Content.ReadAsStringAsync();

                            }
                        }
                    }
                //}
                //else
                //{
                //    ResultDescription = "ไม่สามารถยกเลิกรายการได้ เนื่องจากเลยกำหนดเวลาการยกเลิกแล้ว";
                //}
                return ResultDescription;
            }
            catch(Exception ex)
            {
                ResultDescription = ex.Message;
                return ResultDescription;
            }

            
        }

        [HttpPost]
        public async Task<GetTokenEZTaxRp> GetTokenEZTax()
        {

            GetTokenEZTaxRp _GetTokenEZTaxRp = new GetTokenEZTaxRp();
            int i = 1;
            try
            {

                var body = "";
                ServicePointManager.Expect100Continue = true;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                var client = new RestClient(_appSettings.UrlEztax + "/api/auth"); 
                client.Timeout = 60000;
                var request = new RestRequest(Method.POST);
                var Arr_Body = new
                {
                    username = _appSettings.UsernameEztax,
                    password = _appSettings.PasswordEztax,
                    client_id = _appSettings.ClientIdEztax
                };
                body = JsonConvert.SerializeObject(Arr_Body);
                request.AddParameter("application/json", body, ParameterType.RequestBody);
                IRestResponse response = client.Execute(request);
                if ("OK" == response.StatusCode.ToString().ToUpper())
                {

                    _GetTokenEZTaxRp = JsonConvert.DeserializeObject<GetTokenEZTaxRp>(response.Content);
                    _GetTokenEZTaxRp.StatusCode = "PASS";
                    Log.Debug(JsonConvert.SerializeObject(_GetTokenEZTaxRp));
                }
                else
                {
                    _GetTokenEZTaxRp.StatusCode = response.StatusCode.ToString();

                    Log.Debug(JsonConvert.SerializeObject(_GetTokenEZTaxRp));
                }
                return _GetTokenEZTaxRp;
            }
            catch (Exception ex)
            {
                _GetTokenEZTaxRp.StatusCode = ex.Message;
                Log.Debug(JsonConvert.SerializeObject(_GetTokenEZTaxRp));
                return _GetTokenEZTaxRp;
            }

        }
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpPost]
        [Route("GetApplication")]
        public async Task<GetApplicationRespone> GetApplication(GetApplication _GetApplication)
        {
            GetApplicationRespone _GetApplicationRespone = new GetApplicationRespone();
            DataTable dt = new DataTable();
            try
            {
                Log.Debug(JsonConvert.SerializeObject(_GetApplication));
                System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls11;

                using (var connection = new SqlConnection(_appSettings.strConnString))
                {
                    string sql = @$"SELECT app.applicationid,
                                       app.accountno,
                                       app.applicationstatusid,
                                       app.customerid,
                                       app.applicationcode,
                                       app.productid,
                                       cus.firstname + ' ' + cus.lastname AS Cusname,
                                       cus.mobileno1 AS cusMobile,
                                       app.salename,
                                       app.saletelephoneno,
                                       app.productmodelname,
                                       app.productserialno,
                                       app.productbrandname,
                                       app.saledepcode,
                                       app.saledepname,
                                       app.ref4,
                                       p.AMT_SHP_PAY as getPay,
                                       CASE WHEN p.flag_status = 'Y' THEN p.AMT_SHP_PAY
                                            ELSE '0'
                                        END as getPaid,
                                        (app.FirstPaymentAmount + FeeRate) as FirstPaymentAmount
                                FROM {_appSettings.DATABASEK2}.[application] app WITH (NOLOCK)
                                LEFT JOIN {_appSettings.DATABASEK2}.[customer] cus WITH (NOLOCK) ON cus.customerid = app.customerid
                                LEFT JOIN {_appSettings.SGCROSSBANK}.[SG_PAYMENT_REALTIME] p WITH (NOLOCK) ON app.Ref4 = p.ref1
                                WHERE app.applicationcode = @ApplicationCode ";

                    _GetApplicationRespone = await connection.QuerySingleOrDefaultAsync<GetApplicationRespone>(sql, new { ApplicationCode = _GetApplication.ApplicationCode });
                    if(_GetApplicationRespone != null)
                    {
                        _GetApplicationRespone.statusCode = "PASS";
                    }
                    else
                    {
                        _GetApplicationRespone.statusCode = "Not Found";
                    }

                    Log.Debug("RETURN : " + JsonConvert.SerializeObject(_GetApplicationRespone));
                    return _GetApplicationRespone;
                }
            }
            catch (Exception ex)
            {
                _GetApplicationRespone.statusCode = "FAIL";
                Log.Debug("RETURN : " + ex.Message);
                return _GetApplicationRespone;
            }

        }



        [HttpPost]
        public async Task<MessageReturn> GetStatusClosedSGFinance([FromBody] C100StatusRq _C100StatusRq)
        {
            Log.Debug("By " + HttpContext.Session.GetString("EMP_CODE") + " | " + HttpContext.Session.GetString("FullName") + " : " + JsonConvert.SerializeObject(_C100StatusRq));

            MessageReturn _MessageReturn = new MessageReturn();
            try
            {
                string currentDateTime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

                var requestBody = new
                {
                    applicationCode = _C100StatusRq.ApplicationCode,
                    applicationStatus = "CLOSED",
                    approvalStatus = "CLOSED",
                    approvalDatetime = currentDateTime.ToString(),
                    remark = ""
                };

                using (HttpClient client = new HttpClient())
                {
                    string jsonBody = JsonConvert.SerializeObject(requestBody);

                    client.DefaultRequestHeaders.Add("apikey", _appSettings.Apikey);
                    client.DefaultRequestHeaders.Add("user", "DEV");

                    var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                    HttpResponseMessage responseDevice = await client.PostAsync(_appSettings.SGAPIESIG + "/sgesig/Service/C100_Status", content);
                    int DeviceStatusCode = (int)responseDevice.StatusCode;

                    Log.Debug("API BODY RESPONE : " + JsonConvert.SerializeObject(responseDevice.Content.ReadAsStringAsync()));

                    if (responseDevice.IsSuccessStatusCode)
                    {
                        var jsonResponseDevice = await responseDevice.Content.ReadAsStringAsync();

                        _MessageReturn = JsonConvert.DeserializeObject<MessageReturn>(jsonResponseDevice);
                    }
                }
         
                Log.Debug("RETURN : " + JsonConvert.SerializeObject(_MessageReturn));
                return _MessageReturn;
            }
            catch (Exception ex)
            {
                _MessageReturn.StatusCode = "500";
                _MessageReturn.Message = ex.Message;

                Log.Debug("RETURN : " + JsonConvert.SerializeObject(_MessageReturn));

                return _MessageReturn;
            }
        }
        public async Task<RegisIMEIRespone> RegisIMEI([FromBody] GetApplication _GetApplication)
        {
            Log.Debug("By " + HttpContext.Session.GetString("EMP_CODE") + " | " + HttpContext.Session.GetString("FullName") + " : " + JsonConvert.SerializeObject(_GetApplication));
            RegisIMEIRespone _RegisIMEIRespone = new RegisIMEIRespone();
            try
            {
                GetApplicationRespone _GetApplicationRespone = await GetApplication(_GetApplication);

                var requestBody = new
                {
                    SerrialNo = _GetApplicationRespone.ProductSerialNo,
                    APPLICATION_CODE = _GetApplicationRespone.ApplicationCode,
                    Brand = _GetApplicationRespone.ProductBrandName
                };

                using (HttpClient client = new HttpClient())
                {
                    string jsonBody = JsonConvert.SerializeObject(requestBody);

                    client.DefaultRequestHeaders.Add("apikey", _appSettings.Apikey);
                    client.DefaultRequestHeaders.Add("user", "DEV");

                    var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                    HttpResponseMessage responseDevice = await client.PostAsync(_appSettings.SGAPIESIG + "/sgesig/Service/RegisIMEI", content);
                    int DeviceStatusCode = (int)responseDevice.StatusCode;
                    Log.Debug("API RETURN : " + JsonConvert.SerializeObject(responseDevice.Content.ReadAsStringAsync()));
                    if (responseDevice.IsSuccessStatusCode)
                    {
                        var jsonResponseDevice = await responseDevice.Content.ReadAsStringAsync();

                        _RegisIMEIRespone = JsonConvert.DeserializeObject<RegisIMEIRespone>(jsonResponseDevice);
                    }
                }

                Log.Debug("RETURN : " + JsonConvert.SerializeObject(_RegisIMEIRespone));
                return _RegisIMEIRespone;
            }
            catch (Exception ex)
            {
                _RegisIMEIRespone.statusCode = ex.Message;
                Log.Debug("RETURN : " + JsonConvert.SerializeObject(_RegisIMEIRespone));
                return _RegisIMEIRespone;
            }
        }


        [HttpGet("CheckSession")]
        public IActionResult CheckSession()
        {
            if (HttpContext.Session.GetString("EMP_CODE") == null)
            {
                return Unauthorized();
            }

            return Ok();
        }
        public class FormData
        {
            public string ApplicationID { get; set; }
            public string Remark { get; set; }
            public string CANCEL_USER { get; set; }
        }
    }
}
