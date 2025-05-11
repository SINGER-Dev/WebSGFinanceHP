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
        private readonly AppConfiguration _appSettings;
        private readonly ConnectionStrings _connectionStrings;

        public SearchRepository(AppConfiguration appSettings, ConnectionStrings connectionStrings)
        {
            _appSettings = appSettings;
            _connectionStrings = connectionStrings;
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
	a.accountno,
	a.saledepcode,
    a.AreaID,
    a.DepartmentID,
    a.productserialno,
	a.saledepname,
	ISNULL(CONVERT(NVARCHAR, a.applicationdate, 23),'') AS ApplicationDate,
	ISNULL(CONVERT(NVARCHAR, a.applicationdate, 20),'') AS ApplicationDate2,
	a.productid,
	a.productmodelname,
	a.customerid,
	a.salename,
	a.saletelephoneno,
	a.approveddate,
	a.applicationstatusid,
    h.deliveryflag,
    h.deliverydate,
    h.InvoiceNo,
	ae.refcode,
    LEFT(ae.ou_code, 3) AS OU_Code,
    ae.loantypecate
INTO #base_app
FROM {_appSettings.DATABASEK2}.[application] a WITH (NOLOCK)
JOIN {_appSettings.DATABASEK2}.[applicationextend] ae WITH (NOLOCK) ON ae.applicationid = a.applicationid
LEFT JOIN {_appSettings.SGDIRECT}.[auto_sale_pos_header] h WITH (NOLOCK) ON h.apporderno = a.applicationcode
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
    WHERE ISNUMERIC(Amount) = 1
    GROUP BY Ref1
) bank ON bank.Ref1 = p.ref1
WHERE p.flag_status = 'Y' AND ISNULL(a.ref4, '') <> '';

-- STEP 4: MAIN QUERY
SELECT
    b.applicationid,
	b.applicationcode,
	b.accountno,
	b.saledepcode,
	b.saledepname,
	ISNULL(CONVERT(NVARCHAR, b.applicationdate, 23),'') AS ApplicationDate,
	ISNULL(CONVERT(NVARCHAR, b.applicationdate, 20),'') AS ApplicationDate2,
	b.productid,
	b.productmodelname,
	b.customerid,
	b.salename,
	b.saletelephoneno,
	b.approveddate,
	b.applicationstatusid,
    cus.firstname + ' ' + cus.lastname AS Cusname,
    cus.mobileno1 AS cusMobile,
    ISNULL(serials.serials, '') AS ProductSerialNo,
    CASE
        WHEN ISNULL(con.signedstatus,'') = 'COMP-Done' THEN 'เรียบร้อย'
        WHEN ISNULL(con.signedstatus,'') = 'Initial' THEN 'รอลงนาม'
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
    '-' AS numregis,
    CASE
        WHEN (ISNULL(c.esig_confirm_status, '0') = '1' AND con.signedstatus = 'COMP-Done') THEN 'เรียบร้อย'
        WHEN (ISNULL(c.esig_confirm_status, '0') = '0' OR con.signedstatus = 'Initial') THEN 'รอลงนาม'
        ELSE 'ลงนามไม่สำเร็จ'
    END AS signedText,
    CASE
        WHEN ISNULL(checkcon.numdoc,0) > 1 THEN 'พบรายการซ้ำ'
        ELSE 'ปกติ'
    END AS numdoc,
    b.refcode,
    LEFT(b.ou_code, 3) AS OU_Code,
    b.loantypecate,
    b.deliveryflag,
    CASE
        WHEN ISNULL(b.deliveryflag,0) = 1 THEN 'จัดส่งสินค้าเรียบร้อย'
        ELSE 'อยู่ระหว่างการจัดส่งสินค้า'
    END AS DeliveryFlag,
    CONVERT(NVARCHAR, b.deliverydate, 20) AS DeliveryDate,
    ISNULL(p.ref1,'') AS Ref4,
    ISNULL(b.InvoiceNo,'') AS InvoiceNo,
    ISNULL(p.flag_status,'') as flag_status,
    ISNULL(p.AMT_SHP_PAY,0) as AMT_SHP_PAY,
	ISNULL(p.SumAmount,0) as SumAmount
FROM #base_app b
JOIN {_appSettings.DATABASEK2}.[customer] cus WITH (NOLOCK) ON cus.customerid = b.customerid
LEFT JOIN {_appSettings.DATABASEK2}.[application_esig_status] c WITH (NOLOCK) ON c.application_code = b.applicationcode
LEFT JOIN #contracts_temp con ON con.documentno = b.applicationcode
LEFT JOIN (
    SELECT documentno, COUNT(*) AS numdoc
    FROM #contracts_temp
    GROUP BY documentno
) checkcon ON checkcon.documentno = b.applicationcode
LEFT JOIN #serials serials ON serials.apporderno = b.applicationcode
LEFT JOIN #payment p ON p.applicationcode = b.applicationcode
WHERE b.applicationstatusid NOT IN ('REVISING')
AND (ISNULL(@status, '') = '' OR b.applicationstatusid = @status)
AND b.applicationdate >= '2024-05-01'
AND (ISNULL(@AccountNo, '') = '' OR b.accountno = @AccountNo)
AND (@ApplicationCode IS NULL OR b.ApplicationCode = @ApplicationCode OR b.refcode = @ApplicationCode)
AND (ISNULL(@ProductSerialNo, '') = '' OR b.productserialno = @ProductSerialNo)
AND (ISNULL(@area, '') = '' OR b.AreaID = @area)
AND (ISNULL(@department, '') = '' OR b.DepartmentID = @department)
AND (ISNULL(@CustomerID, '') = '' OR b.customerid = @CustomerID)
AND (ISNULL(@CustomerName, '') = '' OR cus.firstname + ' ' + cus.lastname LIKE '%' + @CustomerName + '%')
ORDER BY b.applicationdate DESC;

-- ล้าง temp tables
DROP TABLE #contracts_temp;
DROP TABLE #serials;
DROP TABLE #payment;
DROP TABLE #base_app;";

            using var connection = new SqlConnection(_connectionStrings.strConnString);
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
