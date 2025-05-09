namespace App.Model
{
    public class AppSettings
    {
        public string Env { get; set; }
        public string WSCANCEL { get; set; }
        public string SGAPIESIG { get; set; }
        public string Apikey { get; set; }
        public string ApplicationID { get; set; }
        public string UrlEztax { get; set; }
        public string UsernameEztax { get; set; }
        public string PasswordEztax { get; set; }
        public string ClientIdEztax { get; set; }
        public string LinkPayment { get; set; }
        public string DATABASEK2 { get; set; }
        public string SGDIRECT { get; set; }
        public string SGCESIGNATURE { get; set; }
        public string SGCROSSBANK { get; set; }
        public string CORELOAN { get; set; }
        public string WsLos { get; set; }
    }
    public class ConnectionStrings
    {
        public string strConnString { get; set; }
        public string strConnString3 { get; set; }
    }
}
