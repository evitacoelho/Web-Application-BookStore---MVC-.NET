using Bulky.DataAccess.Repository.IRepository;
using Bulky.DataAcess.Data;
using Bulky.Models;
using Bulky.Models.Models;
using Bulky.Models.ViewModels;
using Bulky.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BulkyWeb.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Role_Admin)]
    public class UserController : Controller
    {
        //use application db context instead of unit of work to retrieve all users
        private readonly ApplicationDbContext _db;

        //helper to update roles
        private readonly UserManager<IdentityUser> _userManager;

        public UserController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }


        public IActionResult Index()
        {

            return View();
        }

        public IActionResult RoleManagement(string userId)
        {
            //retrieve the role id based on user id and populate the VM prop Role
            string RoleId = _db.UserRoles.FirstOrDefault(u => u.UserId == userId).RoleId;

            // retrieve all the properties values defined in the VM
            RoleManagementVM RoleVM = new RoleManagementVM()
            {
                ApplicationUser = _db.ApplicationUsers.Include(u => u.Company).FirstOrDefault(u => u.Id == userId),
                RoleList = _db.Roles.Select(i => new SelectListItem
                {
                    Text = i.Name,
                    Value = i.Name
                }),
                CompanyList = _db.Roles.Select(i => new SelectListItem
                {
                    Text = i.Name,
                    Value = i.Id.ToString()
                }),
            };

            RoleVM.ApplicationUser.Role = _db.Roles.FirstOrDefault(u => u.Id == RoleId).Name;

            return View(RoleVM);  
        }

        [HttpPost]
        public IActionResult RoleManagement(RoleManagementVM roleManagementVM)
        {
            //retrieve the current role id based on VM
            string RoleId = _db.UserRoles.FirstOrDefault(u => u.UserId == roleManagementVM.ApplicationUser.Id).RoleId;
            //save the old role that was updated to identify change in role
            string oldRole = _db.Roles.FirstOrDefault(u => u.Id == RoleId).Name;
            // retrieve all the properties values defined in the VM
            
            if(!(roleManagementVM.ApplicationUser.Role == oldRole))
            {
                //role updated
               
                //retrieve the updated role from the VM
                ApplicationUser applicationUser = _db.ApplicationUsers.FirstOrDefault(u => u.Id == roleManagementVM.ApplicationUser.Id);
               
                //if the role is updated to company ---> assign a company id
                if(roleManagementVM.ApplicationUser.Role == SD.Role_Company)
                {
                    applicationUser.CompanyId = roleManagementVM.ApplicationUser.CompanyId;
                }

                //remove the old roles company id asssignment
                if(oldRole == SD.Role_Company)
                {
                    applicationUser.CompanyId = null;
                }
                _db.SaveChanges();

                //update changed roles
                _userManager.RemoveFromRoleAsync(applicationUser, oldRole).GetAwaiter().GetResult();
                _userManager.AddToRoleAsync(applicationUser, roleManagementVM.ApplicationUser.Role).GetAwaiter().GetResult();
               

            }
            return RedirectToAction("Index");
        }

        #region API CALLS

        //creating an API that works same as index in order to use datatables instead of creating them
        [HttpGet]
        public IActionResult GetAll()
        {
            //get users with company
            List<ApplicationUser> objUserList = _db.ApplicationUsers.Include(u => u.Company).ToList();

            //get user roles and role categories from DB
            var userRoles = _db.UserRoles.ToList();
            var roles = _db.Roles.ToList(); 

            //retrieve records even when company details are null
            foreach(var user in objUserList)
            {
                //find the role id of the user
                var roleId = userRoles.FirstOrDefault(u => u.UserId == user.Id).RoleId;
                
                //get the matched role name for the role id
               user.Role = roles.FirstOrDefault(u => u.Id == roleId).Name;
                 

                if(user.Company == null)
                {
                    user.Company = new (){ Name = "" };
                }
            }

            return Json(new { data = objUserList });
        }



        [HttpPost]
        public IActionResult LockUnlock([FromBody]string id)
        {
            var objFromDb = _db.ApplicationUsers.FirstOrDefault(u => u.Id == id);
            if(objFromDb == null)
            {
                return Json(new { success = true, message = "Error while Locking/Unlocking" });
            }
            if (objFromDb.LockoutEnd != null && objFromDb.LockoutEnd > DateTime.Now)
            {
                //user is locked, needs unlocking
                objFromDb.LockoutEnd = DateTime.Now;
            }
            else
            {
                //lock the user
                objFromDb.LockoutEnd = DateTime.Now.AddYears(100);
            }
            _db.SaveChanges();
            return Json(new { success = true, message = "Locking/Unlocking Successful" });
        }

        #endregion
    }
}
