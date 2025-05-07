using App.Model;

namespace App.Repositories.Interfaces
{
    public interface IPaymentRepository
    {
        Task<int> CheckValidateStatusPayment(GenEsignatureRq genEsignatureRq);
    }
}
