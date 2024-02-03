using Bulky.DataAccess.Repository.IRepository;
using Bulky.DataAcess.Data;
using Bulky.Models.Models;
using Bulky.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BulkyWeb.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class ProductController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        //built in service to host files - in this case image file
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ProductController(IUnitOfWork unitOfWork, IWebHostEnvironment webHostEnvironment)
        {
            _unitOfWork = unitOfWork;
            _webHostEnvironment = webHostEnvironment;
        }


        public IActionResult Index()
        {
            List<Product> objProductList = _unitOfWork.Product.GetAll(includeProperties:"Category").ToList();
            
            return View(objProductList);
        }


        //upsert is a name for amalgamating create and edit 
        public IActionResult Upsert(int? id)

        {
            //get a category list to use as items in the dropdown menu for products
            //SelectListItem has a text, value pair to be used as a dropdown option element
            //project list of catergories to list of SelectListItem using Select operator
            // IEnumerable<SelectListItem> CategoryList = _unitOfWork.Category
            //  .GetAll().Select(u => new SelectListItem
            //   {
            //        Text = u.Name,
            //        Value = u.CategoryId.ToString()
            //    });
            //use viewbag when data is not in a model
            //when data is returned from the controller to the view
            //acts as a key value pair
            //ViewBag.CategoryList = CategoryList;
            //ViewData dictionary is an alternative to Viewbag and has similar usage
            //requires type casting in the view as a SelectItemList
            //ViewData["CategoryList"] = CategoryList;
            ProductVM productVM = new()
            {
                CategoryList = _unitOfWork.Category.GetAll()
                .Select(u => new SelectListItem
                {
                    Text = u.Name,
                    Value = u.CategoryId.ToString()
                }),
                Product = new Product() 
            };
            if (id == 0 || id == null)
            {
                //create - no id passed
                return View(productVM);
            }
            else
            {
                //update based on id
                //retrieve the product based on the id parameter passed 
                productVM.Product = _unitOfWork.Product.Get(u => u.ProductId == id);
                return View(productVM);
            }
            
        }

        [HttpPost]
        //IFormFile is for the image file that can uploaded during product creation
        public IActionResult Upsert(ProductVM productVM, IFormFile? file)
        {
            if (ModelState.IsValid)
            {
                //points to the root folder - wwwroot
                string wwwRootPath = _webHostEnvironment.WebRootPath;
                if(file != null)
                {
                    //rename the file to a guid and preserve the file extension
                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);   
                    //set an path to upload the path
                    string productPath = Path.Combine(wwwRootPath, @"images\products");

                    //if new image is uploaded as part of update, replace the old image
                    if(!string.IsNullOrEmpty(productVM.Product.ImageUrl))
                    {
                        //delete the old image
                        //trim to remove slashes when stores in DB
                        var oldImagePath =
                            Path.Combine(wwwRootPath, productVM.Product.ImageUrl.TrimStart('\\'));

                        if(System.IO.File.Exists(oldImagePath))
                        {
                            System.IO.File.Delete(oldImagePath);
                        }
                    }

                    //create a file stream to copy the new image file
                    //FileStream takes a the file pathname and the mode 
                    using (var fileStream = new FileStream(Path.Combine(productPath, fileName), FileMode.Create))
                    {
                        file.CopyTo(fileStream);    
                    }
                    productVM.Product.ImageUrl = @"\images\products\" + fileName;
                }
                //if id parameter is not there add a new product, update existing product otherwise
                if(productVM.Product.ProductId == 0)
                {
                    _unitOfWork.Product.Add(productVM.Product);
                }
                else
                {
                    _unitOfWork.Product.Update(productVM.Product);
                }
                
                _unitOfWork.Save();
                TempData["success"] = "Category Created Successfully";
                return RedirectToAction("Index");
            }
            else
            {
                productVM.CategoryList = _unitOfWork.Category.GetAll()
                    .Select(u => new SelectListItem
                    {
                        Text = u.Name,
                        Value = u.CategoryId.ToString()
                    });
                return View(productVM);
            }

        }


       
        #region API CALLS

        //creating an API that works same as index in order to use datatables instead of creating them
        [HttpGet]
        public IActionResult GetAll()
        {
            List<Product> objProductList = _unitOfWork.Product.GetAll(includeProperties: "Category").ToList();

            return Json(new { data = objProductList });
        }

       
        public IActionResult Delete(int? id)
        {
            var productToBeDeleted = _unitOfWork.Product.Get(u =>u.ProductId == id);
           
            if(productToBeDeleted == null) 
            {
                return Json(new { success = false, message = "Error while deleting" });
            }
            var oldImagePath =
                            Path.Combine(_webHostEnvironment.WebRootPath, 
                            productToBeDeleted.ImageUrl.TrimStart('\\'));

            if (System.IO.File.Exists(oldImagePath))
            {
                System.IO.File.Delete(oldImagePath);
            }

            _unitOfWork.Product.Remove(productToBeDeleted);
            _unitOfWork.Save();
            return Json(new { success = true, message = "Delete Successful" });
            
        }

        #endregion
    }
}
