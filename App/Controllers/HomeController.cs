using App.Models;
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
using System.Transactions;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using static System.Net.Mime.MediaTypeNames;

namespace App.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        string strConnString, DATABASEK2, WSCANCEL, UrlEztax, UsernameEztax, PasswordEztax, ClientIdEztax, ApiKey, SGAPIESIG, SGCROSSBANK, SGCESIGNATURE, CORELOAN, SGDIRECT, strConnString3;

        public HomeController(ILogger<HomeController> logger)
        {

            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            var builder = new ConfigurationBuilder()
                        .SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile($"appsettings.{env}.json", true, false)
                        .AddJsonFile($"appsettings.json", true, false)
                        .AddEnvironmentVariables()
                        .Build();
            _logger = logger;
            strConnString = builder.GetConnectionString("strConnString");
            DATABASEK2 = builder.GetConnectionString("DATABASEK2");
            WSCANCEL = builder.GetConnectionString("WSCANCEL");
            UrlEztax = builder.GetConnectionString("UrlEztax");
            UsernameEztax = builder.GetConnectionString("UsernameEztax");
            PasswordEztax = builder.GetConnectionString("PasswordEztax");
            ClientIdEztax = builder.GetConnectionString("ClientIdEztax");
            ApiKey = builder.GetConnectionString("ApiKey");
            SGAPIESIG = builder.GetConnectionString("SGAPIESIG");
            SGCROSSBANK = builder.GetConnectionString("SGCROSSBANK");
            SGCESIGNATURE = builder.GetConnectionString("SGCESIGNATURE");
            CORELOAN = builder.GetConnectionString("CORELOAN");
            SGDIRECT = builder.GetConnectionString("SGDIRECT");
            strConnString3 = builder.GetConnectionString("strConnString3");
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


            using (var connection = new SqlConnection(strConnString3))
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
            using (var connection = new SqlConnection(strConnString3))
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

        public async Task<IActionResult> ChangePaymentDown(string ApplicationCode,string Ref4)
        {
            var EMP_CODE = HttpContext.Session.GetString("EMP_CODE");
            if (EMP_CODE == null)
            {
                return Redirect("/Login");
            }
            ViewBag.EMP_CODE = HttpContext.Session.GetString("EMP_CODE");
            ViewBag.FullName = HttpContext.Session.GetString("FullName");

            GetApplication _GetApplication = new GetApplication();
            _GetApplication.ApplicationCode = ApplicationCode;
            GetApplicationRespone _GetApplicationRespone = await GetApplication(_GetApplication);

            using (var connection = new SqlConnection(strConnString))
            {
                string CustomerID = _GetApplicationRespone.CustomerID.Replace("A", "");
                string sql = @$"
                          SELECT	
                             a.Ref4,
                             a.ApplicationCode,
                             p.AMT_SHP_PAY as getPay,
                             a.CustomerID AS getCustomerID,
                             tranf.TranferPay,
                             tranf.TranferCustomerID
                         FROM {DATABASEK2}.[Application] a WITH (NOLOCK)
                         INNER JOIN  (
 	                        SELECT trafA.Ref4,trafA.ApplicationCode,trafP.AMT_SHP_PAY as TranferPay, trafA.CustomerID AS TranferCustomerID
 	                        FROM {DATABASEK2}.[Application] trafA
 	                        LEFT JOIN {SGCROSSBANK}.[SG_PAYMENT_REALTIME] trafP WITH (NOLOCK) ON trafA.Ref4 = trafP.ref1
 	                        LEFT JOIN {DATABASEK2}.[Customer] trafCUS WITH (NOLOCK) ON trafCUS.CustomerID = trafA.CustomerID  
 	                        group by trafA.Ref4,trafA.ApplicationCode,trafP.AMT_SHP_PAY, trafA.CustomerID
                         )tranf ON a.Ref4 = tranf.Ref4
                         INNER JOIN {SGDIRECT}.[auto_sale_pos_header] h WITH (NOLOCK) ON h.apporderno = a.applicationcode
                         LEFT JOIN {DATABASEK2}.[Customer] cus WITH (NOLOCK) ON cus.CustomerID = a.CustomerID  
                         LEFT JOIN {SGCROSSBANK}.[SG_PAYMENT_REALTIME] p WITH (NOLOCK) ON a.Ref4 = p.ref1
                         WHERE a.ApplicationStatusID = 'CLOSING'
                         AND a.CustomerID = @CustomerID
                         AND p.flag_status <> 'Y' 
                         AND a.applicationCode <> @ApplicationCodeNot
                         ORDER BY a.ApplicationDate DESC";

                var ApplicationRespone = connection.Query<ApplicationResponeModel>(sql, new { CustomerID = CustomerID, ApplicationCodeNot = ApplicationCode });

                ViewBag.Ref4 = _GetApplicationRespone.ref4;
                ViewBag.ApplicationCode = ApplicationCode;
                ViewBag.getPay = _GetApplicationRespone.getPay;
                ViewBag.getCustomerID = _GetApplicationRespone.CustomerID;

                return View(ApplicationRespone);
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
            connection.ConnectionString = strConnString;
            try
            {
                SqlCommand sqlCommand;
                string strSQL = DATABASEK2 + ".[GetApplicationHistory]";
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

        [HttpPost]
        public ActionResult Search(ApplicationRq _ApplicationModel)
        {
            Log.Debug(JsonConvert.SerializeObject(_ApplicationModel));
            List<ApplicationResponeModel> _ApplicationResponeModelMaster = new List<ApplicationResponeModel>();

            try
            {

                using (var connection = new SqlConnection(strConnString))
                {
                    connection.Open();

                    var sql = @$"
                -- Step 1: Insert into temporary tables
                SELECT c.signedstatus,
                       c.statusreceived,
                       c.documentno
                INTO #contracts_temp
                FROM {SGCESIGNATURE}.[contracts] c WITH (NOLOCK)
                INNER JOIN {DATABASEK2}.[application] a WITH (NOLOCK)
                    ON a.applicationcode = c.documentno
                INNER JOIN {DATABASEK2}.[applicationextend] e WITH (NOLOCK)
                    ON e.applicationid = a.applicationid
                WHERE (CONVERT(NVARCHAR, createdat, 23) >= CONVERT(DATE, @startDate, 23) OR ISNULL(@startDate, '') = '')
                  AND (CONVERT(NVARCHAR, createdat, 23) <= CONVERT(DATE, @endDate, 23) OR ISNULL(@endDate, '') = '')
                  AND e.loantypecate = 'HP';

                SELECT COUNT(n.arm_acc_no) AS newnum,
                       n.arm_acc_no,
                       n.arm_loaded_flag
                INTO #arm_t_newsales_temp
                FROM {CORELOAN}.[arm_t_newsales] n WITH (NOLOCK)
                INNER JOIN {DATABASEK2}.[application] a WITH (NOLOCK)
                    ON a.accountno = n.arm_acc_no
                INNER JOIN {DATABASEK2}.[applicationextend] e WITH (NOLOCK)
                    ON e.applicationid = a.applicationid
                WHERE e.loantypecate = 'HP'
                  AND (CONVERT(DATE, n.created_date, 23) >= CONVERT(DATE, @startDate, 23) OR ISNULL(@startDate, '') = '')
                  AND (CONVERT(DATE, n.created_date, 23) <= CONVERT(DATE, @endDate, 23) OR ISNULL(@endDate, '') = '')
                GROUP BY n.arm_acc_no, n.arm_loaded_flag;

                SELECT COUNT(p.arm_acc_no) AS paynum,
                       p.arm_acc_no,
                       p.arm_receipt_stat
                INTO #arm_t_payment_temp
                FROM {CORELOAN}.[arm_t_payment] p WITH (NOLOCK)
                INNER JOIN {DATABASEK2}.[application] a WITH (NOLOCK)
                    ON a.accountno = p.arm_acc_no
                INNER JOIN {DATABASEK2}.[applicationextend] e WITH (NOLOCK)
                    ON e.applicationid = a.applicationid
                WHERE e.loantypecate = 'HP'
                  AND (CONVERT(DATE, p.created_date, 23) >= CONVERT(DATE, @startDate, 23) OR ISNULL(@startDate, '') = '')
                  AND (CONVERT(DATE, p.created_date, 23) <= CONVERT(DATE, @endDate, 23) OR ISNULL(@endDate, '') = '')
                GROUP BY p.arm_acc_no, p.arm_receipt_stat;

                -- Step 2: Main query
                SELECT  a.applicationid,
                        a.applicationcode,
                        a.accountno,
                        a.saledepcode,
                        a.saledepname,
                        CONVERT(NVARCHAR, a.applicationdate, 23) AS ApplicationDate,
                        CONVERT(NVARCHAR, a.applicationdate, 20) AS ApplicationDate2,
                        a.productid,
                        a.productmodelname,
                        a.customerid,
                        cus.firstname + ' ' + cus.lastname AS Cusname,
                        cus.mobileno1 AS cusMobile,
                        a.salename,
                        a.saletelephoneno,
                        CASE
                          WHEN (STUFF((SELECT ', ' + s.itemserial
                                       FROM {SGDIRECT}.[auto_sale_pos_serial] s WITH (NOLOCK)
                                       WHERE s.apporderno = a.applicationcode
                                       FOR XML PATH('')), 1, 1, '')) <> '' THEN
                           STUFF((SELECT ', ' + s.itemserial
                                  FROM {SGDIRECT}.[auto_sale_pos_serial] s WITH (NOLOCK)
                                  WHERE s.apporderno = a.applicationcode
                                  FOR XML PATH('')), 1, 1, '')
                          ELSE a.productserialno
                        END AS ProductSerialNo,
                        a.saledepcode,
                        a.saledepname,
                        a.productmodelname,
                        a.productserialno,
                        a.applicationstatusid,
                        CASE
                          WHEN con.signedstatus = 'COMP-Done' THEN 'เรียบร้อย'
                          WHEN con.signedstatus = 'Initial' THEN 'รอลงนาม'
                          WHEN ISNULL(con.signedstatus, 'NULL') = 'NULL' THEN '-'
                          ELSE con.signedstatus
                        END AS signedStatus,
                        CASE
                          WHEN ISNULL(con.statusreceived, '0') = '1' THEN 'รับสินค้าแล้ว'
                          ELSE 'ยังไม่รับสินค้า'
                        END AS statusReceived,
                        CASE
                          WHEN ISNULL(c.esig_confirm_status, '0') = '1' THEN 'เรียบร้อย'
                          ELSE 'รอลงนาม'
                        END AS ESIG_CONFIRM_STATUS,
                        CASE
                          WHEN ISNULL(c.receive_flag, '0') = '1' THEN 'รับสินค้าแล้ว'
                          ELSE 'ยังไม่รับสินค้า'
                        END AS RECEIVE_FLAG,
                        a.approveddate,
                        '-' AS numregis,
                        CASE
                          WHEN c.esig_confirm_status = '1' AND con.signedstatus = 'COMP-Done' THEN 'เรียบร้อย'
                          WHEN c.esig_confirm_status = '0' OR con.signedstatus = 'Initial' THEN 'รอลงนาม'
                          ELSE 'ลงนามไม่สำเร็จ'
                        END AS signedText,
                        CASE
                          WHEN checkcon.numdoc > 1 THEN 'พบรายการซ้ำ'
                          ELSE 'ปกติ'
                        END AS numdoc,
                        CASE
                          WHEN new.newnum = 1 AND new.arm_loaded_flag IN (0, 1) THEN 'เรียบร้อย'
                          WHEN new.newnum = 1 AND new.arm_loaded_flag = 2 THEN 'CANCELLED'
                          WHEN new.newnum > 1 THEN 'รายการซ้ำ'
                          ELSE 'ไม่พบรายการ'
                        END AS newnum,
                        CASE
                          WHEN pay.paynum = 1 AND pay.arm_receipt_stat = 'APPROVED' THEN 'เรียบร้อย'
                          WHEN pay.paynum = 1 AND pay.arm_receipt_stat = 'CANCELLED' THEN 'CANCELLED'
                          WHEN pay.paynum > 1 THEN 'รายการซ้ำ'
                          ELSE 'ไม่พบรายการ'
                        END AS paynum,
                        '' AS LINE_STATUS,
                        '' AS TRANSFER_DATE,
                        appex.refcode,
                        LEFT(appex.ou_code, 3) AS OU_Code,
                        appex.loantypecate,
                        [deliveryflag],
                        CASE
                          WHEN [deliveryflag] = 1 THEN 'จัดส่งสินค้าเรียบร้อย'
                          ELSE 'อยู่ระหว่างการจัดส่งสินค้า'
                        END AS DeliveryFlag,
                        CONVERT(NVARCHAR, [deliverydate], 20) AS [DeliveryDate],
                        ISNULL(bank.ref4,'') AS Ref4,
                        a.applicationcode,
                        ISNULL(h.InvoiceNo,'') AS InvoiceNo,
                        ISNULL(bank.flag_status,'') as flag_status
                FROM {DATABASEK2}.[application] a WITH (NOLOCK)
                INNER JOIN {SGDIRECT}.[auto_sale_pos_header] h WITH (NOLOCK)
                    ON h.apporderno = a.applicationcode
                INNER JOIN {DATABASEK2}.[applicationextend] appex WITH (NOLOCK)
                    ON appex.applicationid = a.applicationid
                LEFT JOIN {DATABASEK2}.[customer] cus WITH (NOLOCK)
                    ON cus.customerid = a.customerid
                LEFT JOIN {DATABASEK2}.[application_esig_status] c WITH (NOLOCK)
                    ON a.applicationcode = c.application_code
                LEFT JOIN #contracts_temp con WITH (NOLOCK)
                    ON a.applicationcode = con.documentno
                LEFT JOIN (SELECT COUNT(documentno) AS numdoc,
                                  documentno
                           FROM #contracts_temp WITH (NOLOCK)
                           GROUP BY documentno) checkcon
                    ON checkcon.documentno = a.applicationcode
                LEFT JOIN (SELECT COUNT(arm_acc_no) AS newnum,
                                  arm_acc_no,
                                  arm_loaded_flag
                           FROM #arm_t_newsales_temp WITH (NOLOCK)
                           GROUP BY arm_acc_no, arm_loaded_flag) new
                    ON new.arm_acc_no = a.accountno
                LEFT JOIN (SELECT COUNT(arm_acc_no) AS paynum,
                                  arm_acc_no,
                                  arm_receipt_stat
                           FROM #arm_t_payment_temp WITH (NOLOCK)
                           GROUP BY arm_acc_no, arm_receipt_stat) pay
                    ON pay.arm_acc_no = a.accountno
                LEFT JOIN (SELECT a.applicationid,
                                  a.applicationcode,
                                  a.ref4,
                                  p.amt_shp_pay,
                                  p.amt_paid,
                                    p.flag_status
                           FROM {DATABASEK2}.[application] a WITH (NOLOCK)
                           INNER JOIN {DATABASEK2}.[applicationextend] e WITH (NOLOCK)
                               ON a.applicationid = e.applicationid
                           INNER JOIN {SGCROSSBANK}.[sg_payment_realtime] p WITH (NOLOCK)
                               ON a.ref4 = p.ref1
                           WHERE p.flag_status = 'Y' AND ISNULL(a.ref4, '') <> '') bank
                    ON bank.applicationcode = a.applicationcode
                WHERE a.applicationstatusid NOT IN ('REVISING')
                AND (ISNULL(@status, '') = '' OR a.applicationstatusid = @status)
                AND CONVERT(DATE, a.applicationdate, 23) >= '2024-05-01'
                AND (ISNULL(@AccountNo, '') = '' OR a.accountno = @AccountNo)
                AND (ISNULL(@ApplicationCode, '') = '' OR a.applicationcode = @ApplicationCode)
                AND appex.loantypecate = 'HP'
                AND (ISNULL(@ProductSerialNo, '') = '' OR a.productserialno = @ProductSerialNo)
                AND (ISNULL(@area, '') = '' OR a.AreaID = @area)
                AND (ISNULL(@department, '') = '' OR a.DepartmentID = @department)
                AND (ISNULL(@CustomerID, '') = '' OR a.customerid = @CustomerID)
                AND (CONVERT(DATE, a.applicationdate, 23) >= CONVERT(DATE, @startDate, 23) OR ISNULL(@startDate, '') = '')
                AND (CONVERT(DATE, a.applicationdate, 23) <= CONVERT(DATE, @endDate, 23) OR ISNULL(@endDate, '') = '')
                AND (ISNULL(@CustomerName, '') = '' OR cus.firstname + ' ' + cus.lastname LIKE '%' + @CustomerName + '%')
                AND LEFT(appex.ou_code, 3) = 'STL'
                ORDER BY a.applicationdate DESC;

                -- Drop temporary tables
                DROP TABLE #contracts_temp;
                DROP TABLE #arm_t_newsales_temp;
                DROP TABLE #arm_t_payment_temp;";


                    if(null != _ApplicationModel.department)
                    {
                        _ApplicationModel.department = _ApplicationModel.department + '0';
                    }
                    
                    var parameters = new
                    {
                        startdate = _ApplicationModel.startdate,
                        enddate = _ApplicationModel.enddate,
                        status = _ApplicationModel.status,
                        AccountNo = _ApplicationModel.AccountNo,
                        ApplicationCode = _ApplicationModel.ApplicationCode,
                        ProductSerialNo = _ApplicationModel.ProductSerialNo,
                        CustomerID = _ApplicationModel.CustomerID,
                        CustomerName = _ApplicationModel.CustomerName,
                        area  = _ApplicationModel.area,
                        department = _ApplicationModel.department

                    };

                    var applications = connection.Query<ApplicationResponeModel>(sql, parameters);
                    foreach (var application in applications)
                    {
                        string datenowText = DateTime.Now.ToString("yyyy-MM-dd", new CultureInfo("en-US"));
                        if (application.ApplicationDate.ToString() == datenowText)
                        {
                            application.datenowcheck = "1";
                        }
                        else
                        {
                            application.datenowcheck = "0";
                        }
                        _ApplicationResponeModelMaster.Add(application);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex.Message);
            }

            return PartialView("_SearchResults", _ApplicationResponeModelMaster);
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
                    connection.ConnectionString = strConnString;
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
                    string strSQL = DATABASEK2 + ".[CancelApplication]";
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

                            client.DefaultRequestHeaders.Add("apikey", ApiKey);
                            client.DefaultRequestHeaders.Add("user", "DEV");

                            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                            HttpResponseMessage responseDevice = await client.PostAsync(SGAPIESIG + "/sgesig/Service/C100_Status", content);
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

        protected HttpWebRequest CreateWebRequest(string url)
        {

            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(url);
            webRequest.Headers.Add(@"SOAP:Action");

            webRequest.ContentType = "text/xml;charset=\"utf-8\"";
            webRequest.Accept = "text/xml";
            webRequest.Method = "POST";
            return webRequest;
        }

        [HttpPost]
        public async Task<MessageModel> CCOWebService(CCOWebServiceModel _CCOWebService)
        {
            string result = "";
            MessageModel _MessageModel = new MessageModel();
            Log.Debug(JsonConvert.SerializeObject(_CCOWebService.id));

            try
            {

                HttpWebRequest request = CreateWebRequest(WSCANCEL);

                XmlDocument soapEnvelopeXml = new XmlDocument();
                soapEnvelopeXml.LoadXml(@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap12:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap12=""http://www.w3.org/2003/05/soap-envelope"">
  <soap12:Body>
    <WorkflowGoToCancelRequest xmlns=""http://tempuri.org/"">
      <id>" + _CCOWebService.id + @"</id>
    </WorkflowGoToCancelRequest>
  </soap12:Body>
</soap12:Envelope>");

                using (Stream stream = request.GetRequestStream())
                {
                    soapEnvelopeXml.Save(stream);
                }

                using (WebResponse response = request.GetResponse())
                {
                    using (StreamReader rd = new StreamReader(response.GetResponseStream()))
                    {
                        string soapResult = rd.ReadToEnd();
                        XmlDocument xmlDocument = new XmlDocument();
                        xmlDocument.LoadXml(soapResult);
                        result = xmlDocument.InnerText;
                        if ("TRUE" == result.ToUpper())
                        {
                            _MessageModel.StatusCode = "200";
                            _MessageModel.Message = "Success";
                            Log.Debug("WorkflowGoToCancelRequest Complete");
                        }
                        else
                        {
                            _MessageModel.StatusCode = "400";
                            _MessageModel.Message = result;
                            Log.Error("WorkflowGoToCancelRequest Fail : " + result);
                        }
                    }
                }
                return _MessageModel;
            }
            catch (Exception ex)
            {
                _MessageModel.StatusCode = "500";
                _MessageModel.Message = ex.Message;
                Log.Error("WorkflowGoToCancelRequest Fail : " + ex.Message);
                return _MessageModel;
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
                var client = new RestClient(UrlEztax+ "/api/auth"); 
                client.Timeout = 60000;
                var request = new RestRequest(Method.POST);
                var Arr_Body = new
                {
                    username = UsernameEztax,
                    password = PasswordEztax,
                    client_id = ClientIdEztax
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

                using (var connection = new SqlConnection(strConnString))
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
                                FROM {DATABASEK2}.[application] app WITH (NOLOCK)
                                LEFT JOIN {DATABASEK2}.[customer] cus WITH (NOLOCK) ON cus.customerid = app.customerid
                                LEFT JOIN {SGCROSSBANK}.[SG_PAYMENT_REALTIME] p WITH (NOLOCK) ON app.Ref4 = p.ref1
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
        public async Task<GetApplicationRespone> getDataRef2([FromBody] getDataRef2 _getDataRef2)
        {
            Log.Debug("By " + HttpContext.Session.GetString("EMP_CODE") + " | " + HttpContext.Session.GetString("FullName") + " : " + JsonConvert.SerializeObject(_getDataRef2));
            Log.Debug(JsonConvert.SerializeObject(_getDataRef2));
            string? ApplicationCode = _getDataRef2.ApplicationCode;
            GetApplication _GetApplication = new GetApplication();
            _GetApplication.ApplicationCode = ApplicationCode;
            GetApplicationRespone _GetApplicationRespone = await GetApplication(_GetApplication);
            return _GetApplicationRespone;
        }

        [HttpPost]
        public async Task<MessageReturn> GetStatusClosedSGFinance([FromBody] C100StatusRq _C100StatusRq)
        {
            Log.Debug("By " + HttpContext.Session.GetString("EMP_CODE") + " | " + HttpContext.Session.GetString("FullName") + " : " + JsonConvert.SerializeObject(_C100StatusRq));
            Log.Debug(JsonConvert.SerializeObject(_C100StatusRq));
            string ResultDescription = "";
            MessageReturn _MessageReturn = new MessageReturn();
            try
            {

                SqlConnection connection = new SqlConnection();
                connection.ConnectionString = strConnString;
                connection.Open();
                SqlCommand sqlCommand;
                string strSQL = DATABASEK2 + ".[GetStatusClosedSGFinance]";
                sqlCommand = new SqlCommand(strSQL, connection);
                sqlCommand.CommandType = CommandType.StoredProcedure;
                sqlCommand.Parameters.AddWithValue("ApplicationCode", _C100StatusRq.ApplicationCode);

                SqlDataAdapter dtAdapter = new SqlDataAdapter();
                dtAdapter.SelectCommand = sqlCommand;
                DataTable dt = new DataTable();
                dtAdapter.Fill(dt);
                connection.Close();
                sqlCommand.Parameters.Clear();

                if (dt.Rows.Count > 0)
                {
                    Log.Debug(JsonConvert.SerializeObject(dt));

                    requestBodyValue _requestBodyValue = JsonConvert.DeserializeObject<requestBodyValue>(dt.Rows[0]["StatusDesc"].ToString());

                    var requestBody = new
                    {
                        applicationCode = _requestBodyValue.applicationCode,
                        applicationStatus = _requestBodyValue.applicationStatus,
                        approvalStatus = _requestBodyValue.approvalStatus,
                        approvalDatetime = _requestBodyValue.approvalDatetime,
                        remark = _requestBodyValue.remark
                    };

                    Log.Debug("API BODY REQUEST : " + JsonConvert.SerializeObject(requestBody));

                    using (HttpClient client = new HttpClient())
                    {
                        string jsonBody = JsonConvert.SerializeObject(requestBody);

                        client.DefaultRequestHeaders.Add("apikey", ApiKey);
                        client.DefaultRequestHeaders.Add("user", "DEV");

                        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                        HttpResponseMessage responseDevice = await client.PostAsync(SGAPIESIG+ "/sgesig/Service/C100_Status", content);
                        int DeviceStatusCode = (int)responseDevice.StatusCode;

                        Log.Debug("API BODY RESPONE : " + JsonConvert.SerializeObject(responseDevice.Content.ReadAsStringAsync()));

                        if (responseDevice.IsSuccessStatusCode)
                        {
                            var jsonResponseDevice = await responseDevice.Content.ReadAsStringAsync();

                            _MessageReturn = JsonConvert.DeserializeObject<MessageReturn>(jsonResponseDevice);
                        }
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

        [HttpPost]
        public async Task<MessageReturn> GenEsignature([FromBody] C100StatusRq _C100StatusRq)
        {
            Log.Debug("By " + HttpContext.Session.GetString("EMP_CODE") + " | " + HttpContext.Session.GetString("FullName") + " : " + JsonConvert.SerializeObject(_C100StatusRq));
            MessageReturn _MessageReturn = new MessageReturn();
            try
            {

                var requestBody = new
                {
                    APPLICATION_CODE = _C100StatusRq.ApplicationCode
                };

                using (HttpClient client = new HttpClient())
                {
                    string jsonBody = JsonConvert.SerializeObject(requestBody);

                    client.DefaultRequestHeaders.Add("apikey", ApiKey);
                    client.DefaultRequestHeaders.Add("user", "DEV");

                    var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                    HttpResponseMessage responseDevice = await client.PostAsync(SGAPIESIG + "/sgesig/api/v2/GenEsignature", content);
                    int DeviceStatusCode = (int)responseDevice.StatusCode;

                    Log.Debug("API RESPONE : " + JsonConvert.SerializeObject(responseDevice.Content.ReadAsStringAsync()));

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

        [HttpPost]
        public async Task<MessageReturn> GetAddTNewSalesNewSGFinance([FromBody] C100StatusRq _C100StatusRq)
        {
            Log.Debug("By " + HttpContext.Session.GetString("EMP_CODE") + " | " + HttpContext.Session.GetString("FullName") + " : " + JsonConvert.SerializeObject(_C100StatusRq));
            MessageReturn _MessageReturn = new MessageReturn();
            GetOuCodeRespone _GetOuCodeRespone = new GetOuCodeRespone();
            try
            {

                    var requestBody = new
                    {
                        APPLICATION_CODE = _C100StatusRq.ApplicationCode
                    };

                    Log.Debug("API REQUEST : " + JsonConvert.SerializeObject(requestBody));


                    using (HttpClient client = new HttpClient())
                    {
                        string jsonBody = JsonConvert.SerializeObject(requestBody);

                        client.DefaultRequestHeaders.Add("apikey", ApiKey);
                        client.DefaultRequestHeaders.Add("user", "DEV");

                        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                        HttpResponseMessage responseDevice;

                        responseDevice = await client.PostAsync(SGAPIESIG + "/sgesig/SubmitSale", content);


                        int DeviceStatusCode = (int)responseDevice.StatusCode;

                        Log.Debug("API RESPONE : " + JsonConvert.SerializeObject(responseDevice.Content.ReadAsStringAsync()));

                        if (responseDevice.IsSuccessStatusCode)
                        {
                            var jsonResponseDevice = await responseDevice.Content.ReadAsStringAsync();

                            _MessageReturn = JsonConvert.DeserializeObject<MessageReturn>(jsonResponseDevice);
                        }
                    }
                //}
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

                    client.DefaultRequestHeaders.Add("apikey", ApiKey);
                    client.DefaultRequestHeaders.Add("user", "DEV");

                    var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                    HttpResponseMessage responseDevice = await client.PostAsync(SGAPIESIG + "/sgesig/Service/RegisIMEI", content);
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

        [HttpPost]
        public async Task<string> ApiChangePayment(ApiChangePayment _ApiChangePayment)
        {
            Log.Debug("By " + HttpContext.Session.GetString("EMP_CODE") + " | " + HttpContext.Session.GetString("FullName") + " : " + JsonConvert.SerializeObject(_ApiChangePayment));
            string result = "";
            try
            {
                Log.Debug("REQUEST : " + JsonConvert.SerializeObject(_ApiChangePayment));

                GetApplication getApplication1 = new GetApplication();
                getApplication1.ApplicationCode = _ApiChangePayment.ApplicationCode1;
                GetApplicationRespone _GetApplicationRespone = await GetApplication(getApplication1);

                GetApplication getApplication2 = new GetApplication();
                getApplication2.ApplicationCode = _ApiChangePayment.ApplicationCode1;
                GetApplicationRespone _GetApplicationRespone2 = await GetApplication(getApplication2);

                var requestBody = new
                {
                    ref1 = _GetApplicationRespone.ref4,
                    ref2 = _GetApplicationRespone2.ref4,
                    amount = "0.00"
                };

                using (HttpClient client = new HttpClient())
                {
                    string jsonBody = JsonConvert.SerializeObject(requestBody);

                    client.DefaultRequestHeaders.Add("apikey", ApiKey);

                    var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                    HttpResponseMessage responseDevice = await client.PostAsync(SGAPIESIG + "/c100/v2/SgFinance/PaymentAdjust", content);
                    int DeviceStatusCode = (int)responseDevice.StatusCode;
                    Log.Debug("API RETURN : " + JsonConvert.SerializeObject(responseDevice.Content.ReadAsStringAsync()));
                    if (!responseDevice.IsSuccessStatusCode)
                    {
                        result = await responseDevice.Content.ReadAsStringAsync();
                    }
                }

                Log.Debug("RETURN : " + result);
                return result;
            }
            catch (Exception ex)
            {
                result = ex.Message;
                Log.Debug("RETURN : " + result);
                return result;
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
