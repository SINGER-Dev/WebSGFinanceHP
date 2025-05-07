using App.Model;

namespace App.Services.Interfaces
{
    public interface IStartFlowService
    {
        Task<MessageReturn> StartFlow(StartFlowRq startFlowRq);
    }
}
