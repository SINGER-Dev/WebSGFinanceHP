using App.Model;
using Microsoft.AspNetCore.Mvc;

public interface IGenEsignatureRepository
{
    Task<int> MapingOrderAccount(GenEsignatureRq genEsignatureRq);
    Task<ContractRp> Contract(GenEsignatureRq genEsignatureRq);
    Task GenContract(GenEsignatureRq genEsignatureRq);
    Task<int> UpDateContractHeader(UpDateContractHeaderRq upDateContractHeaderRq);
    Task<CheckDataHeaderRp> CheckDataHeader(GenEsignatureRq genEsignatureRq);
    Task<int> CheckPayment(GenEsignatureRq genEsignatureRq);
}