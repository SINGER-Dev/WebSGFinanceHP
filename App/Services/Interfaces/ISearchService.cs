using App.Model;

namespace App.Services.Interfaces
{
    public interface ISearchService
    {
        Task<List<ApplicationResponeModel>> Search(ApplicationRq applicationRq);
    }
}
