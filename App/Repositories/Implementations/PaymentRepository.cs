using App.Model;
using App.Repositories.Interfaces;
using Azure.Core;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Diagnostics.Contracts;

namespace App.Repositories.Implementations
{
    public class PaymentRepository : IPaymentRepository
    {
        private readonly AppSettings _appSettings;

        public PaymentRepository(AppSettings appSettings)
        {
            _appSettings = appSettings;
        }

        public async Task<int> CheckValidateStatusPayment(GenEsignatureRq genEsignatureRq)
        {
            SqlCommand sqlCommand;
            string strSQL = @$"
            SELECT CASE WHEN EXISTS (
                SELECT 1
                FROM {_appSettings.DATABASEK2}.[Application] WITH (NOLOCK)
                WHERE ApplicationCode = @ApplicationCode
                AND ApplicationStatusID = 'CLOSING'
            ) THEN 1 ELSE 0 END AS IsExist
            ";
            SqlConnection connection = new SqlConnection();
            connection.ConnectionString = _appSettings.strConnString;
            sqlCommand = new SqlCommand(strSQL, connection);
            sqlCommand.CommandType = CommandType.Text;
            sqlCommand.Parameters.AddWithValue("ApplicationCode", genEsignatureRq.ApplicationCode);
            SqlDataAdapter dtAdapter = new SqlDataAdapter();
            dtAdapter.SelectCommand = sqlCommand;
            DataTable dt = new DataTable();
            dtAdapter.Fill(dt);
            connection.Close();
            if (dt.Rows.Count > 0)
            {
                return Convert.ToInt32(dt.Rows[0]["IsExist"]);
            }
            return 0;
        }
    }
}
