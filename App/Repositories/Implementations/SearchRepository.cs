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
            using var connection = new SqlConnection(_connectionStrings.strConnString);

            var parameters = new
            {
                StartDate = applicationRq.startdate,     // date
                EndDate = applicationRq.enddate,       // date
                status = applicationRq.status,
                AccountNo = applicationRq.AccountNo,
                ApplicationCode = applicationRq.ApplicationCode,
                ProductSerialNo = applicationRq.ProductSerialNo,
                CustomerID = applicationRq.CustomerID,
                CustomerName = applicationRq.CustomerName,
                department = applicationRq.department,
                area = applicationRq.area
            };

            var sql = @$"{_appSettings.SGDIRECT}.[GetApplicationsSummary]";

            var result = (await connection.QueryAsync<ApplicationResponeModel>(sql,
                parameters,
                commandType: CommandType.StoredProcedure,
                commandTimeout: 60
            )).ToList();

            return result;
        }
    }

}
