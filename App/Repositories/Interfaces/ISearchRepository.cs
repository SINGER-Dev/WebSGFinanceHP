using App.Model;

namespace App.Repositories.Interfaces
{
    public interface ISearchRepository
    {
        Task<List<ApplicationResponeModel>> Search(ApplicationRq applicationRq);
    }
}
