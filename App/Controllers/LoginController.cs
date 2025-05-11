using App.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using System;
using System.Data;
using System.Data.Common;
using static System.Net.Mime.MediaTypeNames;

namespace App.Controllers
{
    public class LoginController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppConfiguration _appSettings;
        private readonly ConnectionStrings _connectionStrings;
        public LoginController(ILogger<HomeController> logger, AppConfiguration appSettings, ConnectionStrings connectionStrings)
        {
            _appSettings = appSettings;
            _connectionStrings = connectionStrings;
        }

        public IActionResult Index()
        {
            ViewBag.Env = _appSettings.Env;
            return View();
        }

        [HttpPost]
        public async Task<User> Login([FromBody] Login _Login)
        {
            
            User _User = new User();

            SqlConnection connection = new SqlConnection();
            connection.ConnectionString = _connectionStrings.strConnString3;
            connection.Open();
            SqlDataAdapter dtAdapter = new SqlDataAdapter();

            string strSQL = "LoginAuth";

            SqlCommand sqlCommand;
            sqlCommand = new SqlCommand(strSQL, connection);
            sqlCommand.CommandType = CommandType.StoredProcedure;

            //ชื่อตัวแปรในสโตร , ค่าที่เก็บสโตร
            sqlCommand.Parameters.AddWithValue("EMP_CODE", _Login.user_id);
            sqlCommand.Parameters.AddWithValue("Password", _Login.password);
            sqlCommand.Parameters.AddWithValue("ApplicationID", _appSettings.ApplicationID);
            dtAdapter.SelectCommand = sqlCommand;

            DataTable dt = new DataTable();
            dtAdapter.Fill(dt);
            sqlCommand.Parameters.Clear();
            connection.Close();

            try
            {
                if (dt.Rows.Count > 0)
                {

                    string[] jsonData21 = dt.Rows[0]["RoleDescription"].ToString().Split(',');
                    List<string> list = new List<string>();

                    foreach (var x in jsonData21)
                    {
                        string[] arrayTXT = x.ToString().Split('-');
                        if (arrayTXT.Length > 1)
                        {
                            string firstTXT = arrayTXT[0].ToString();
                            string secondTXT = arrayTXT[1].ToString().ToUpper();
                            list.Add(firstTXT.Trim() + "-" + secondTXT.Trim());
                        }
                        else
                        {
                            string firstTXT = arrayTXT[0].ToString();
                            list.Add(firstTXT.Trim());
                        }
                    }

                    _User.StatusCode = "SUCCESS";
                    _User.Message = "200";

                    _User.user_id = dt.Rows[0]["EMP_CODE"].ToString();
                    _User.name = dt.Rows[0]["FullName"].ToString();
                    _User.position = dt.Rows[0]["POSITION"].ToString();
                    _User.company = dt.Rows[0]["COMPANY"].ToString();
                    _User.first_name_en = dt.Rows[0]["EMP_NAME_ENG"].ToString();
                    _User.last_name_en = dt.Rows[0]["EMP_SUR_ENG"].ToString();



                    HttpContext.Session.SetString("FullName", dt.Rows[0]["FullName"].ToString());
                    HttpContext.Session.SetString("EMP_CODE", dt.Rows[0]["EMP_CODE"].ToString());
                    HttpContext.Session.SetString("RoleDescription", JsonConvert.SerializeObject(list));
                }
                else
                {
                    _User.StatusCode = "404";
                    _User.Message = "UserName or Password is incorrect";
                }
            }
            catch (Exception ex)
            {
                _User.StatusCode = "500";
                _User.Message = ex.Message;
            }


            return _User;
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        { 
            // Clear the session
            HttpContext.Session.Clear();

            // Redirect to the home page (or any other page)
            return RedirectToAction("index", "Login");
        }
    }
}
