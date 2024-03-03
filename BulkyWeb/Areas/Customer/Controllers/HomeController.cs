using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models.Models;
using Bulky.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Security.Claims;

namespace BulkyWeb.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        //get all products from DB to display on the home page --> use unit of work
        private readonly IUnitOfWork _unitOfWork;

        //pass it as a dependency injection to the constructor
        public HomeController(ILogger<HomeController> logger, IUnitOfWork unitOfWork)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
        }

        public IActionResult Index()
        {
            

            //display the product list on the home page
            IEnumerable<Product> productList = _unitOfWork.Product.GetAll(includeProperties: "Category");
            return View(productList);
        }

        public IActionResult Details(int id)
        {
            ShoppingCart cart = new()
            {
                Product = _unitOfWork.Product.Get(u => u.ProductId == id, includeProperties: "Category"),
                Count = 1,
                ProductId = id
            };
            return View(cart);
            
        }

        [HttpPost]
        [Authorize] //authorized user to add products in the cart
        public IActionResult Details(ShoppingCart shoppingCart)
        {
            //get the login details of the user using a helper method
            //get the user identity from the default User object and convert it into claims identity
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            
            //get the value of the user id using claims types
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

            //assign the extracted user id
            shoppingCart.ApplicationUserId = userId;

            //get the an existing entry for the same product and same user
            ShoppingCart cartFromDb = _unitOfWork.ShoppingCart.Get(u=> u.ApplicationUserId == userId 
                && u.ProductId == shoppingCart.ProductId);

            if(cartFromDb != null)
            {
                //update product count
                cartFromDb.Count += shoppingCart.Count;
                _unitOfWork.ShoppingCart.Update(cartFromDb);
                _unitOfWork.Save();
            }
            else
            {
                //add product to cart
                _unitOfWork.ShoppingCart.Add(shoppingCart);
                _unitOfWork.Save();
                //associate this request with a session
                //takes key value pairs - session name and value(object) - count of items in the cart
                //this is to display the count of items in shopping cart
                HttpContext.Session.SetInt32(SD.SessionCart,
                    _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userId).Count());
            }
            TempData["success"] = "Cart updated successfully";
                    
         

            return RedirectToAction(nameof(Index));

        }
        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
