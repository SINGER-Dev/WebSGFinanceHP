using App.Model;
using Microsoft.AspNetCore.Mvc;
public interface IGenEsignatureService
{
    Task<MessageReturn> ValidateGenEsignature([FromBody] GenEsignatureRq genEsignatureRq);
}
