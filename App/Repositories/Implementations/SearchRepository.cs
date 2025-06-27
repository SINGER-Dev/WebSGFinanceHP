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

-- เพิ่ม indexes ที่แนะนำ (ถ้ายังไม่มี):
-- CREATE INDEX IX_application_applicationdate_statusid ON application(applicationdate, applicationstatusid) INCLUDE (applicationcode, accountno, refcode)
-- CREATE INDEX IX_applicationextend_applicationid_oucode ON applicationextend(applicationid, ou_code)
-- CREATE INDEX IX_contracts_documentno_createdat ON contracts(documentno, createdat)
-- CREATE INDEX IX_payment_ref1_flagstatus ON sg_payment_realtime(ref1, flag_status)

-- STEP 0: ปรับให้มีการ filter ที่มีประสิทธิภาพมากขึ้น
SELECT 
    a.applicationid,
    a.applicationcode,
    a.accountno,
    a.saledepcode,
    a.AreaID,
    a.DepartmentID,
    a.productserialno,
    a.saledepname,
    a.applicationdate,
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
    ae.loantypecate,
    l.SHORT_URL,
    a.Ref4
INTO #base_app
FROM {_appSettings.DATABASEK2}.[application] a WITH (NOLOCK)
JOIN {_appSettings.DATABASEK2}.[applicationextend] ae WITH (NOLOCK) ON ae.applicationid = a.applicationid
LEFT JOIN {_appSettings.SGDIRECT}.[auto_sale_pos_header] h WITH (NOLOCK) ON h.apporderno = a.applicationcode
OUTER APPLY (
    SELECT TOP 1 SHORT_URL 
    FROM {_appSettings.SGDIRECT}.[GEN_SHORT_LINK] ll WITH (NOLOCK)
    WHERE ll.REF_ID = a.applicationcode
) L 
WHERE a.applicationdate >= @TodayStart 
    AND a.applicationdate < @TomorrowStart
    AND a.applicationdate >= '2024-05-01'  -- ย้าย filter มาไว้ที่นี่
    AND ae.ou_code LIKE 'STL%'
    AND a.applicationstatusid NOT IN ('REVISING')  -- ย้าย filter มาไว้ที่นี่
    AND (ISNULL(@status, '') = '' OR a.applicationstatusid = @status)
    AND (ISNULL(@AccountNo, '') = '' OR a.accountno = @AccountNo)
    AND (@ApplicationCode IS NULL OR a.ApplicationCode = @ApplicationCode OR ae.refcode = @ApplicationCode)
    AND (ISNULL(@ProductSerialNo, '') = '' OR a.productserialno = @ProductSerialNo)
    AND (ISNULL(@area, '') = '' OR a.AreaID = @area)
    AND (ISNULL(@department, '') = '' OR a.DepartmentID = @department)
    AND (ISNULL(@CustomerID, '') = '' OR a.customerid = @CustomerID);

-- สร้าง index บน temp table
CREATE CLUSTERED INDEX IX_base_app_applicationcode ON #base_app(applicationcode);
CREATE INDEX IX_base_app_customerid ON #base_app(customerid);


-- STEP 1: ปรับให้ JOIN กับ base_app ก่อน
SELECT c.documentno, c.signedstatus, c.statusreceived, mp.URL, mp.PinCode
INTO #contracts_temp
FROM #base_app b
JOIN {_appSettings.SGCESIGNATURE}.contracts c WITH (NOLOCK) ON c.documentno = b.applicationcode
JOIN {_appSettings.SGCESIGNATURE}.MapingOrderAccount mp WITH (NOLOCK) ON c.documentno = mp.ApplicationCode
WHERE c.createdat >= @TodayStart AND c.createdat < @TomorrowStart;

-- สร้าง index บน contracts temp table
CREATE CLUSTERED INDEX IX_contracts_temp_documentno ON #contracts_temp(documentno);

-- STEP 2: Serial แบบรวม - ใช้ FOR XML PATH แทน STRING_AGG (ถ้า SQL Server เวอร์ชันเก่า)
SELECT s.apporderno,
       STUFF((SELECT ', ' + s2.itemserial 
              FROM {_appSettings.SGDIRECT}.[auto_sale_pos_serial] s2 WITH (NOLOCK)
              WHERE s2.apporderno = s.apporderno
              ORDER BY s2.itemserial
              FOR XML PATH('')), 1, 2, '') AS serials
INTO #serials
FROM #base_app b
JOIN {_appSettings.SGDIRECT}.[auto_sale_pos_serial] s WITH (NOLOCK) ON s.apporderno = b.applicationcode
GROUP BY s.apporderno;

-- หรือใช้ STRING_AGG ถ้า SQL Server 2017+
-- SELECT s.apporderno,
--        STRING_AGG(s.itemserial, ', ') WITHIN GROUP (ORDER BY s.itemserial) as serials
-- INTO #serials
-- FROM #base_app b
-- JOIN {_appSettings.SGDIRECT}.[auto_sale_pos_serial] s WITH (NOLOCK) ON s.apporderno = b.applicationcode
-- GROUP BY s.apporderno;

CREATE CLUSTERED INDEX IX_serials_apporderno ON #serials(apporderno);

-- STEP 3: Payment - ใช้ EXISTS แทน JOIN เพื่อประสิทธิภาพ
SELECT b.applicationcode,
       p.ref1,
       p.flag_status,
       p.amt_shp_pay,
       bank.SumAmount
INTO #payment
FROM #base_app b
JOIN {_appSettings.SGCROSSBANK}.[sg_payment_realtime] p WITH (NOLOCK) ON b.ref4 = p.ref1
LEFT JOIN (
    SELECT Ref1, SUM(TRY_CAST(Amount AS DECIMAL(18, 2))) AS SumAmount
    FROM {_appSettings.SGCROSSBANK}.[BANK_TRANSACTION] WITH (NOLOCK)
    WHERE ISNUMERIC(Amount) = 1
    GROUP BY Ref1
) bank ON bank.Ref1 = p.ref1
WHERE p.flag_status = 'Y' 
    AND b.ref4 IS NOT NULL 
    AND p.Customername IS NOT NULL 
    AND b.ref4 <> '';

CREATE CLUSTERED INDEX IX_payment_applicationcode ON #payment(applicationcode);

-- STEP 4: MAIN QUERY - ใช้ CTE สำหรับ customer filter
WITH FilteredCustomers AS (
    SELECT customerid, firstname + ' ' + lastname AS Cusname, mobileno1 AS cusMobile
    FROM {_appSettings.DATABASEK2}.[customer] WITH (NOLOCK)
    WHERE (ISNULL(@CustomerName, '') = '' OR firstname + ' ' + lastname LIKE '%' + @CustomerName + '%')
)
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
    cus.Cusname,
    cus.cusMobile,
    ISNULL(serials.serials, '') AS ProductSerialNo,
    CASE
        WHEN con.signedstatus = 'COMP-Done' THEN 'เรียบร้อย'
        WHEN con.signedstatus = 'Initial' THEN 'รอลงนาม'
        WHEN con.signedstatus IS NULL THEN '-'
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
        WHEN checkcon.numdoc > 1 THEN 'พบรายการซ้ำ'
        ELSE 'ปกติ'
    END AS numdoc,
    b.refcode,
    b.OU_Code,
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
    ISNULL(p.SumAmount,0) as SumAmount,
    ISNULL(con.URL,'') as URL,
    ISNULL(con.PinCode,'') as PinCode,
    ISNULL(b.SHORT_URL,'') as SHORT_URL

FROM #base_app b
JOIN FilteredCustomers cus ON cus.customerid = b.customerid
LEFT JOIN {_appSettings.DATABASEK2}.[application_esig_status] c WITH (NOLOCK) ON c.application_code = b.applicationcode
LEFT JOIN #contracts_temp con ON con.documentno = b.applicationcode
LEFT JOIN (
    SELECT documentno, COUNT(*) AS numdoc
    FROM #contracts_temp
    GROUP BY documentno
    HAVING COUNT(*) > 1  -- เอาเฉพาะที่มีมากกว่า 1
) checkcon ON checkcon.documentno = b.applicationcode
LEFT JOIN #serials serials ON serials.apporderno = b.applicationcode
LEFT JOIN #payment p ON p.applicationcode = b.applicationcode
ORDER BY b.applicationdate DESC;

-- ล้าง temp tables
DROP TABLE #contracts_temp;
DROP TABLE #serials;
DROP TABLE #payment;
DROP TABLE #base_app;
";

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
