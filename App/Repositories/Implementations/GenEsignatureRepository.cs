using App.Model;
using Azure.Core;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Diagnostics.Contracts;

namespace App.Repositories.Implementations
{
    public class GenEsignatureRepository : IGenEsignatureRepository
    {
        private readonly AppConfiguration _appSettings;
        private readonly ConnectionStrings _connectionStrings;

        public GenEsignatureRepository(AppConfiguration appSettings, ConnectionStrings connectionStrings)
        {
            _appSettings = appSettings;
            _connectionStrings = connectionStrings;
        }

        public async Task<int> MapingOrderAccount(GenEsignatureRq genEsignatureRq)
        {
            SqlCommand sqlCommand;
            string strSQL = @$"
            SELECT CASE WHEN EXISTS (
                SELECT 1
                FROM {_appSettings.SGCESIGNATURE}.[MapingOrderAccount] WITH (NOLOCK)
                WHERE ApplicationCode = @ApplicationCode
            ) THEN 1 ELSE 0 END AS IsExist
            ";

            SqlConnection connection = new SqlConnection();
            connection.ConnectionString = _connectionStrings.strConnString;
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
        public async Task<ContractRp> Contract(GenEsignatureRq genEsignatureRq)
        {
            var result = new ContractRp();
            SqlCommand sqlCommand;
            string strSQL = @$"
            SELECT TOP 1 
                1 AS IsExist,
                ISNULL(accountNo,'') AS AccountNo
            FROM {_appSettings.SGCESIGNATURE}.[contracts] WITH (NOLOCK)
            WHERE DocumentNo = @ApplicationCode
            ";

            SqlConnection connection = new SqlConnection();
            connection.ConnectionString = _connectionStrings.strConnString;
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
                result.IsExist = Convert.ToInt32(dt.Rows[0]["IsExist"]);
                result.AccountNo = dt.Rows[0]["AccountNo"].ToString();
            }

            return result;
        }
        public async Task GenContract(GenEsignatureRq genEsignatureRq)
        {
            var connStr = _connectionStrings.strConnString;
            var sql = @$"{_appSettings.SGCESIGNATURE}.[ESG_SP_GEN_CONTRACT_SGFINANCE]";

            await using var connection = new SqlConnection(connStr);
            await using var command = new SqlCommand(sql, connection)
            {
                CommandTimeout = 300,
                CommandType = CommandType.StoredProcedure
            };

            command.Parameters.AddWithValue("@ApplicationCode", genEsignatureRq.ApplicationCode);

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }
        public async Task<int> UpDateContractHeader(UpDateContractHeaderRq upDateContractHeaderRq)
        {
            var connStr = _connectionStrings.strConnString;
            var sql = $@"
                    UPDATE {_appSettings.SGDIRECT}.[dbo].[AUTO_SALE_POS_HEADER]
                    SET AccountNo = @AccountNo,
                        ContractNo = @AccountNo
                    WHERE AppOrderNo = @AppOrderNo";

            await using var connection = new SqlConnection(connStr);
            await using var command = new SqlCommand(sql, connection)
            {
                CommandTimeout = 300,
                CommandType = CommandType.Text
            };

            command.Parameters.AddWithValue("@AppOrderNo", upDateContractHeaderRq.ApplicationCode);
            command.Parameters.AddWithValue("@AccountNo", upDateContractHeaderRq.AccountNo);

            await connection.OpenAsync();
            int rowsAffected = await command.ExecuteNonQueryAsync();

            return rowsAffected; // ให้ผลลัพธ์กลับไปให้ Service layer ตัดสินใจ
        }

        public async Task<CheckDataHeaderRp> CheckDataHeader(GenEsignatureRq genEsignatureRq)
        {
            var result = new CheckDataHeaderRp();

            SqlCommand sqlCommand;
            string strSQL = @$"
            SELECT TOP 1 
                1 AS IsExist,
                ISNULL(AccountNo,'') AS AccountNo,
                ISNULL(PosTrackNumber,'') AS PosTrackNumber
            FROM {_appSettings.SGDIRECT}.[AUTO_SALE_POS_HEADER] WITH (NOLOCK)
            WHERE AppOrderNo = @ApplicationCode
            ";

            SqlConnection connection = new SqlConnection();
            connection.ConnectionString = _connectionStrings.strConnString;
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
                result.IsExist = Convert.ToInt32(dt.Rows[0]["IsExist"]);
                result.AccountNo = dt.Rows[0]["AccountNo"].ToString();
                result.PosTrackNumber = dt.Rows[0]["PosTrackNumber"].ToString();
            }

            return result;
        }
        public async Task<int> CheckPayment(GenEsignatureRq genEsignatureRq)
        {
            SqlCommand sqlCommand;
            string strSQL = @$"

            SELECT CASE WHEN EXISTS (
                SELECT 1
            FROM {_appSettings.DATABASEK2}.[application] a WITH (NOLOCK)
            JOIN {_appSettings.SGCROSSBANK}.[sg_payment_realtime] p WITH (NOLOCK) ON a.ref4 = p.ref1
            LEFT JOIN (
                SELECT Ref1, SUM(TRY_CAST(Amount AS DECIMAL(18, 2))) AS SumAmount
                FROM {_appSettings.SGCROSSBANK}.[BANK_TRANSACTION] WITH (NOLOCK)
                GROUP BY Ref1
            ) bank ON bank.Ref1 = p.ref1
            WHERE p.flag_status = 'Y' AND ISNULL(a.ref4, '') <> ''
            AND p.amt_shp_pay = bank.SumAmount
            AND a.applicationcode = @ApplicationCode
            ) THEN 1 ELSE 0 END AS IsExist

            
            ";

            SqlConnection connection = new SqlConnection();
            connection.ConnectionString = _connectionStrings.strConnString;
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
