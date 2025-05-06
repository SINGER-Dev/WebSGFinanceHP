﻿namespace App.Model
{
    public class requestBodyValue
    {
        public string applicationCode { get; set; }
        public string applicationStatus { get; set; }
        public string approvalStatus { get; set; }
        public string approvalDatetime { get; set; }
        public string remark { get; set; }

    }

    public class C100StatusRp
    {
        public string applicationCode { get; set; }
        public string applicationStatus { get; set; }
        public string approvalStatus { get; set; }
        public DateTime approvalDatetime { get; set; }
        public string remark { get; set; }
        public string losApplicationCode { get; set; }
        public string contractNo { get; set; }
    }
    public class MessageReturn
    {
        public string? StatusCode { get; set; }
        public string? Message { get; set; }
    }

    public class RegisIMEIRequest
    {
        public string? SerrialNo { get; set; }
        public string? APPLICATION_CODE { get; set; }
        public string? Brand { get; set; }
    }

    public class RegisIMEIRespone
    {
        public string? statusCode { get; set; }
    }
    public class GetOuCodeRespone
    {
        public string? statusCode { get; set; }
        public string? OU_Code { get; set; }
    }
    public class C100StatusRq
    {
        public string ApplicationCode { get; set; }
    }
    public class getDataRef2
    {
        public string ApplicationCode { get; set; }
    }

    public class ApplicationRq
    {
        public string AccountNo { get; set; }
        public string ApplicationCode { get; set; }
        public string ProductSerialNo { get; set; }
        public string CustomerID { get; set; }
        public string status { get; set; }
        public string startdate { get; set; }
        public string enddate { get; set; }
        public string CustomerName { get; set; }
        public string area { get; set; }
        public string department { get; set; }
    }

    public class SearchGetApplicationHistory
    {
        public string AccountNo { get; set; }
        public string ApplicationCode { get; set; }
        public string startdate { get; set; }
        public string enddate { get; set; }
    }

    public class ApplicationCancelModel
    {
        public string AccountNo { get; set; }
        public string remark { get; set; }
    }
    public class GetTokenEZTaxRp
    {
        public string? StatusCode { get; set; }
        public string? access_token { get; set; }
        public string? refresh_token { get; set; }
        public string? token_type { get; set; }
    }
    public class GetTokenEZTaxRq
    {
        public string? username { get; set; }
        public string? password { get; set; }
        public string? client_id { get; set; }
    }
    public class CCOWebServiceModel
    {
        public string id { get; set;}
    }

    public class GetApplication
    {
        public string? ApplicationCode { get; set; }
    }
    
    public class GetMsDepartment
    {
        public string? area { get; set; }
    }
    public class ApiChangePayment
    {
        public string? ApplicationCode1 { get; set; }
        public string? ApplicationCode2 { get; set; }
    }

    public class MsArea
    {
        public string? AREA_CODE { get; set; }
        public string? AREA_NAME_THA { get; set; }
        public string? AREA_NAME_ENG { get; set; }
    }
    public class MsAreaListViewModel
    {
        public List<MsArea> MsAreas { get; set; }
    }

    public class MsDepartment
    {
        public string? DEP_CODE { get; set; }
        public string? DEP_NAME_THA { get; set; }
    }
    public class MsDepartmentViewModel
    {
        public List<MsDepartment> MsDepartment { get; set; }
    }

    public class GetApplicationRespone
    {
        public string? statusCode { get; set; }
        public string? AccountNo { get; set; }
        public string? ApplicationStatusID { get; set; }
        public string? ApplicationID { get; set; }
        public string? ApplicationCode { get; set; }

        public string? SaleDepCode { get; set; }
        public string? SaleDepName { get; set; }
        public string? ProductModelName { get; set; }
        public string? ProductSerialNo { get; set; }
        public string? ProductBrandName { get; set; }
        public string? CustomerID { get; set; }
        public string? Cusname { get; set; }
        public string? cusMobile { get; set; }
        public string? SaleName { get; set; }
        public string? SaleTelephoneNo { get; set; }
        public string? ref4 { get; set; }
        public string? getPay { get; set;}
        public string? getPaid { get; set; }
        public string? FirstPaymentAmount { get; set; }

    }
}
