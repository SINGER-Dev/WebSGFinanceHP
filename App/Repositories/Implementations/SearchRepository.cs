using App.Model;
using App.Repositories.Interfaces;
using Microsoft.Data.SqlClient;
using System.Data;
using Dapper;
using System.Globalization;
namespace App.Repositories.Implementations
{
    public class SearchRepository : ISearchRepository
    {
        private readonly AppSettings _appSettings;

        public SearchRepository(AppSettings appSettings)
        {
            _appSettings = appSettings;
        }

        public async Task<List<ApplicationResponeModel>> Search(ApplicationRq applicationRq)
        {
            List<ApplicationResponeModel> _ApplicationResponeModelMaster = new List<ApplicationResponeModel>();

            var sql = @$"
DECLARE 
    @TodayStart DATETIME = CAST(@startDate AS DATE),
    @TomorrowStart DATETIME = DATEADD(DAY, 1, CAST(@endDate AS DATE));

-- STEP 0: ดึง application เฉพาะของ STL ที่อยู่ในช่วงวันนี้
SELECT 
    a.applicationid, 
    a.applicationcode, 
    a.applicationdate,
    h.deliveryflag,
    h.deliverydate,
    h.InvoiceNo,
	ae.refcode,
    LEFT(ae.ou_code, 3) AS OU_Code,
    ae.loantypecate
INTO #base_app
FROM {_appSettings.DATABASEK2}.[application] a WITH (NOLOCK)
JOIN {_appSettings.DATABASEK2}.[applicationextend] ae WITH (NOLOCK) ON ae.applicationid = a.applicationid
JOIN {_appSettings.SGDIRECT}.[auto_sale_pos_header] h WITH (NOLOCK) ON h.apporderno = a.applicationcode
WHERE a.applicationdate >= @TodayStart AND a.applicationdate < @TomorrowStart
AND ae.ou_code LIKE 'STL%';

-- STEP 1: สร้าง Temp Contract
SELECT c.documentno, c.signedstatus, c.statusreceived
INTO #contracts_temp
FROM {_appSettings.SGCESIGNATURE}.contracts c WITH (NOLOCK)
JOIN #base_app b ON c.documentno = b.applicationcode
WHERE c.createdat >= @TodayStart AND c.createdat < @TomorrowStart;

-- STEP 2: Serial แบบรวม
SELECT s.apporderno,
       serials = STRING_AGG(s.itemserial, ', ') WITHIN GROUP (ORDER BY s.itemserial)
INTO #serials
FROM  {_appSettings.SGDIRECT}.[auto_sale_pos_serial] s WITH (NOLOCK)
JOIN #base_app b ON s.apporderno = b.applicationcode
GROUP BY s.apporderno;

-- STEP 3: Payment แบบมี ref1
SELECT a.applicationcode,
       p.ref1,
       p.flag_status,
       p.amt_shp_pay,
	   bank.SumAmount
INTO #payment
FROM {_appSettings.DATABASEK2}.[application] a WITH (NOLOCK)
JOIN #base_app b ON a.applicationcode = b.applicationcode
JOIN {_appSettings.SGCROSSBANK}.[sg_payment_realtime] p WITH (NOLOCK) ON a.ref4 = p.ref1
LEFT JOIN (
    SELECT Ref1, SUM(TRY_CAST(Amount AS DECIMAL(18, 2))) AS SumAmount
    FROM {_appSettings.SGCROSSBANK}.[BANK_TRANSACTION] WITH (NOLOCK)
    GROUP BY Ref1
) bank ON bank.Ref1 = p.ref1
WHERE p.flag_status = 'Y' AND ISNULL(a.ref4, '') <> '';

-- STEP 4: MAIN QUERY
SELECT
    a.applicationid,
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
    ISNULL(serials.serials, '') AS ProductSerialNo,
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
        WHEN (ISNULL(c.esig_confirm_status, '0') = '1' AND con.signedstatus = 'COMP-Done') THEN 'เรียบร้อย'
        WHEN (c.esig_confirm_status = '0' OR con.signedstatus = 'Initial') THEN 'รอลงนาม'
        ELSE 'ลงนามไม่สำเร็จ'
    END AS signedText,
    CASE
        WHEN checkcon.numdoc > 1 THEN 'พบรายการซ้ำ'
        ELSE 'ปกติ'
    END AS numdoc,
    b.refcode,
    LEFT(b.ou_code, 3) AS OU_Code,
    b.loantypecate,
    b.deliveryflag,
    CASE
        WHEN b.deliveryflag = 1 THEN 'จัดส่งสินค้าเรียบร้อย'
        ELSE 'อยู่ระหว่างการจัดส่งสินค้า'
    END AS DeliveryFlag,
    CONVERT(NVARCHAR, b.deliverydate, 20) AS DeliveryDate,
    ISNULL(p.ref1,'') AS Ref4,
    ISNULL(b.InvoiceNo,'') AS InvoiceNo,
    ISNULL(p.flag_status,'') as flag_status,
    ISNULL(p.AMT_SHP_PAY,'') as AMT_SHP_PAY,
	ISNULL(p.SumAmount,'') as SumAmount
FROM #base_app b
JOIN {_appSettings.DATABASEK2}.[application] a WITH (NOLOCK) ON a.applicationid = b.applicationid
JOIN {_appSettings.DATABASEK2}.[customer] cus WITH (NOLOCK) ON cus.customerid = a.customerid
LEFT JOIN {_appSettings.DATABASEK2}.[application_esig_status] c WITH (NOLOCK) ON c.application_code = a.applicationcode
LEFT JOIN #contracts_temp con ON con.documentno = a.applicationcode
LEFT JOIN (
    SELECT documentno, COUNT(*) AS numdoc
    FROM #contracts_temp
    GROUP BY documentno
) checkcon ON checkcon.documentno = a.applicationcode
LEFT JOIN #serials serials ON serials.apporderno = a.applicationcode
LEFT JOIN #payment p ON p.applicationcode = a.applicationcode
WHERE a.applicationstatusid NOT IN ('REVISING')
AND (ISNULL(@status, '') = '' OR a.applicationstatusid = @status)
AND CONVERT(DATE, a.applicationdate, 23) >= '2024-05-01'
AND (ISNULL(@AccountNo, '') = '' OR a.accountno = @AccountNo)
AND (ISNULL(@ApplicationCode, '') = '' OR a.applicationcode = @ApplicationCode)
AND (ISNULL(@ProductSerialNo, '') = '' OR a.productserialno = @ProductSerialNo)
AND (ISNULL(@area, '') = '' OR a.AreaID = @area)
AND (ISNULL(@department, '') = '' OR a.DepartmentID = @department)
AND (ISNULL(@CustomerID, '') = '' OR a.customerid = @CustomerID)
AND (ISNULL(@CustomerName, '') = '' OR cus.firstname + ' ' + cus.lastname LIKE '%' + @CustomerName + '%')
ORDER BY a.applicationdate DESC;

-- ล้าง temp tables
DROP TABLE #contracts_temp;
DROP TABLE #serials;
DROP TABLE #payment;
DROP TABLE #base_app;";

            using var connection = new SqlConnection(_appSettings.strConnString);
            var parameters = new
            {
                startdate = applicationRq.startdate,
                enddate = applicationRq.enddate,
                status = applicationRq.status,
                AccountNo = applicationRq.AccountNo,
                ApplicationCode = applicationRq.ApplicationCode,
                ProductSerialNo = applicationRq.ProductSerialNo,
                CustomerID = applicationRq.CustomerID,
                CustomerName = applicationRq.CustomerName,
                department = applicationRq.department,
                area = applicationRq.area
            };

            var result = (await connection.QueryAsync<ApplicationResponeModel>(sql, parameters, commandTimeout: 60)).ToList();

            return result;
        }
    }

}
