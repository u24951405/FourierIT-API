using FourierIT_API.Data;
using FourierIT_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FourierIT_API.Controllers
{
    [ApiController]
    [Authorize]
    public class InstitutionDocumentRequestController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<User> _userManager;

        public InstitutionDocumentRequestController(AppDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        //Institution staff submits a document request targeting a specific enlisted client
        //[HttpPost("api/institutions/{institutionId:int}/document-requests")]
        //[Authorize(Roles = "Department Admin,Compliance Officer")]
    }
}
