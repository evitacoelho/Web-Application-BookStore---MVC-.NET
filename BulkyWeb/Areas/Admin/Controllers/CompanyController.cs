using Bulky.DataAccess.Repository.IRepository;
using Bulky.DataAcess.Data;
using Bulky.Models.Models;
using Bulky.Models.ViewModels;
using Bulky.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BulkyWeb.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Role_Admin)]
    public class CompanyController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        //built in service to host files - in this case image file
        private readonly IWebHostEnvironment _webHostEnvironment;

        public CompanyController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
           
        }


        public IActionResult Index()
        {
            List<Company> objCompanyList = _unitOfWork.Company.GetAll().ToList();
            
            return View(objCompanyList);
        }


        //upsert is a name for amalgamating create and edit 
        public IActionResult Upsert(int? id)

        {
           
            if (id == 0 || id == null)
            {
                //create - no id passed
                return View(new Company());
            }
            else
            {
                //update based on id
                //retrieve the company based on the id parameter passed 
                Company companyObj = _unitOfWork.Company.Get(u => u.CompanyId == id);
                return View(companyObj);
            }
            
        }

        [HttpPost]
        //IFormFile is for the image file that can uploaded during company creation
        public IActionResult Upsert(Company companyObj)
        {
            if (ModelState.IsValid)
            {
                
                //if id parameter is not there add a new company, update existing company otherwise
                if(companyObj.CompanyId == 0)
                {
                    _unitOfWork.Company.Add(companyObj);
                }
                else
                {
                    _unitOfWork.Company.Update(companyObj);
                }
                
                _unitOfWork.Save();
                TempData["success"] = "Category Created Successfully";
                return RedirectToAction("Index");
            }
            else
            {
               
                return View(companyObj);
            }

        }


       
        #region API CALLS

        //creating an API that works same as index in order to use datatables instead of creating them
        [HttpGet]
        public IActionResult GetAll()
        {
            List<Company> objCompanyList = _unitOfWork.Company.GetAll().ToList();

            return Json(new { data = objCompanyList });
        }

        [HttpDelete]
        public IActionResult Delete(int? id)
        {
            var companyToBeDeleted = _unitOfWork.Company.Get(u =>u.CompanyId == id);
           
            if(companyToBeDeleted == null) 
            {
                return Json(new { success = false, message = "Error while deleting" });
            }
            _unitOfWork.Company.Remove(companyToBeDeleted);
            _unitOfWork.Save();
            return Json(new { success = true, message = "Delete Successful" });
        }

        #endregion
    }
}
