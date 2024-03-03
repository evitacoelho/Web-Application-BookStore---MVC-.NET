using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models;
using Bulky.Models.Models;
using Bulky.Models.ViewModels;
using Bulky.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe.Checkout;
using System.Security.Claims;

namespace BulkyWeb.Areas.Customer.Controllers
{
    [Area("customer")]
    [Authorize]
    public class CartController : Controller
    {
        //displays cart details like order total and cart list - using a VM

        private readonly IUnitOfWork _unitOfWork;

        //bind shopping cart values to populate this vm
        [BindProperty]
        public ShoppingCartVM ShoppingCartVM { get; set; }

        public CartController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public IActionResult Index()
        {
            //retrieve the authorized user
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userID = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

            //populate the shopping VM based on userID
            ShoppingCartVM = new()
            {
                ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userID,
                includeProperties: "Product"),
                OrderHeader = new()
            };
            //iterate through the cart list to get the price of items based on the quantity
            foreach(var cart in ShoppingCartVM.ShoppingCartList)
            {
                cart.Price = GetPriceBasedOnQuantity(cart);
                ShoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count);
            }
            return View(ShoppingCartVM);
        }

        public IActionResult Summary()
        {
            //retrieve the authorized user
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userID = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

            //populate the shopping VM based on userID
            ShoppingCartVM = new()
            {
                ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userID,
                includeProperties: "Product"),
                OrderHeader = new()
            };

            ShoppingCartVM.OrderHeader.ApplicationUser = _unitOfWork.ApplicationUser.Get(u => u.Id == userID);
            ShoppingCartVM.OrderHeader.Name = ShoppingCartVM.OrderHeader.ApplicationUser.Name;
            ShoppingCartVM.OrderHeader.PhoneNumber = ShoppingCartVM.OrderHeader.ApplicationUser.PhoneNumber;
            ShoppingCartVM.OrderHeader.StreetAddress= ShoppingCartVM.OrderHeader.ApplicationUser.StreetAddress;
            ShoppingCartVM.OrderHeader.City = ShoppingCartVM.OrderHeader.ApplicationUser.City;
            ShoppingCartVM.OrderHeader.State = ShoppingCartVM.OrderHeader.ApplicationUser.State;
            ShoppingCartVM.OrderHeader.PostalCode = ShoppingCartVM.OrderHeader.ApplicationUser.PostalCode;
            //iterate through the cart list to get the price of items based on the quantity
            foreach (var cart in ShoppingCartVM.ShoppingCartList)
            {
                cart.Price = GetPriceBasedOnQuantity(cart);
                ShoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count);
            }

            return View(ShoppingCartVM);
        }

        [HttpPost]
        [ActionName("Summary")]
		public IActionResult SummaryPOST()
		{
			//retrieve the authorized user
			var claimsIdentity = (ClaimsIdentity)User.Identity;
			var userID = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

            //populate the shopping VM - no need to create a new object - binding property used above
            ShoppingCartVM.ShoppingCartList = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == userID,
                includeProperties: "Product");

            ShoppingCartVM.OrderHeader.OrderDate = DateTime.Now;
            ShoppingCartVM.OrderHeader.ApplicationUserID = userID;

           ApplicationUser applicationUser = _unitOfWork.ApplicationUser.Get(u => u.Id == userID);
			
			//iterate through the cart list to get the price of items based on the quantity
			foreach (var cart in ShoppingCartVM.ShoppingCartList)
			{
				cart.Price = GetPriceBasedOnQuantity(cart);
				ShoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count);
			}

            // check if company account - place order without payment
            if (applicationUser.CompanyId.GetValueOrDefault()==0)
            {
                // regular customer - Capture payment
                ShoppingCartVM.OrderHeader.PaymentStatus = SD.PaymentStatusPending;
                ShoppingCartVM.OrderHeader.OrderStatus = SD.StatusPending;
            }
            else
            {
				//company user
				ShoppingCartVM.OrderHeader.PaymentStatus = SD.PaymentStatusDelayedPayment;
				ShoppingCartVM.OrderHeader.OrderStatus = SD.StatusApproved;
			}

            //Add the populated order header and save changes
            _unitOfWork.OrderHeader.Add(ShoppingCartVM.OrderHeader);
            _unitOfWork.Save();

			//create order detail
			foreach (var cart in ShoppingCartVM.ShoppingCartList)
			{
                OrderDetail orderDetail = new()
                {
                    ProductId = cart.ProductId,
                    OrderHeaderId = ShoppingCartVM.OrderHeader.OrderHeaderId,
                    Price = cart.Price,
                    Count = cart.Count
                };

                //add order detail and save
                _unitOfWork.OrderDetail.Add(orderDetail);
			    _unitOfWork.Save();

			}

			if (applicationUser.CompanyId.GetValueOrDefault() == 0)
			{
                // regular customer - Capture payment Stripe logic
                //capture the localhost url
                var domain = "https://localhost:7246/";
                //create session using the classes provided by stripe
				var options = new Stripe.Checkout.SessionCreateOptions
				{
                    //define a success and cancel url
					SuccessUrl = domain+$"customer/cart/OrderConfirmation?id={ShoppingCartVM.OrderHeader.OrderHeaderId}",
					CancelUrl = domain+ "customer/cart/index",

                    //hold all the products details
                    LineItems = new List<SessionLineItemOptions>(),
	                Mode = "payment",
                };
                //iterate over the shopping cart items
                foreach (var item in ShoppingCartVM.ShoppingCartList)
                {
                    var sessionLineItem = new SessionLineItemOptions
                    {
                        //configure the price data - used to create a new price object
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            //convert price into UnitAmount
                            UnitAmount = (long)(item.Price * 100),
                            Currency = "gbp",

                            //used to retrieve any product details
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = item.Product.Title
                            }
                        },
                        //define quantity to determine the final price
                        Quantity = item.Count
                    };
                    //add line item to the list
                    options.LineItems.Add(sessionLineItem);
                }
                //create a new service session with the defined options
				var service = new Stripe.Checkout.SessionService();
                //holds the session ID and the payment intent ID
				Session session = service.Create(options);
                //update payment id
                _unitOfWork.OrderHeader.UpdateStripePaymentID(ShoppingCartVM.OrderHeader.OrderHeaderId, session.Id, session.PaymentIntentId);
                _unitOfWork.Save();
                
                //URL to redirect is present in the session object provided by stripe
                Response.Headers.Add("Location", session.Url);
                return new StatusCodeResult(303);
			}
            return RedirectToAction(nameof(OrderConfirmation),new { id = ShoppingCartVM.OrderHeader.OrderHeaderId });
		}

        public IActionResult OrderConfirmation(int id)
        {
            //retrieve the entire order header based on the ID
            OrderHeader orderHeader = _unitOfWork.OrderHeader.Get(u => u.OrderHeaderId == id, includeProperties: "ApplicationUser");
            
            //check if it is a customer order
            if(orderHeader.PaymentStatus != SD.PaymentStatusDelayedPayment)
            {
                //retrieve a stripe session using stripe class
                var service = new SessionService();

                //retrieve the session object
                Session session = service.Get(orderHeader.SessionId);

                //stripe enum values for payment - paid, unpaid etc
                if (session.PaymentStatus.ToLower() == "paid")
                {
					//retrieve the payment intent id
					_unitOfWork.OrderHeader.UpdateStripePaymentID(id, session.Id, session.PaymentIntentId);
                    _unitOfWork.OrderHeader.UpdateStatus(id, SD.StatusApproved, SD.PaymentStatusApproved);
                    _unitOfWork.Save();
                }
                HttpContext.Session.Clear();

            }
            //retrive the shopping list and remove it from the db
            List<ShoppingCart> shoppingCarts = _unitOfWork.ShoppingCart
                .GetAll(u =>u.ApplicationUserId == orderHeader.ApplicationUserID).ToList();
            _unitOfWork.ShoppingCart.RemoveRange(shoppingCarts);
            return View(id);
        }

		public IActionResult Plus (int cartId)
        {
            var cartFromDB = _unitOfWork.ShoppingCart.Get(u=>u.CartId == cartId);
            cartFromDB.Count += 1;
            _unitOfWork.ShoppingCart.Update(cartFromDB);
            _unitOfWork.Save();
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Minus(int cartId)
        {
            var cartFromDB = _unitOfWork.ShoppingCart.Get(u => u.CartId== cartId, tracked: true);
            if(cartFromDB.Count <= 1)
            {
                //remove from cart
                HttpContext.Session.SetInt32(SD.SessionCart,
               _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == cartFromDB.ApplicationUserId).Count() - 1);
                _unitOfWork.ShoppingCart.Remove(cartFromDB);
            }
            else
            {
                cartFromDB.Count -= 1;
                _unitOfWork.ShoppingCart.Update(cartFromDB);
            }
                    
            _unitOfWork.Save();
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Remove(int cartId)
        {
            var cartFromDB = _unitOfWork.ShoppingCart.Get(u => u.CartId == cartId, tracked:true);
            HttpContext.Session.SetInt32(SD.SessionCart, 
                _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == cartFromDB.ApplicationUserId).Count() - 1);
            _unitOfWork.ShoppingCart.Remove(cartFromDB);
            _unitOfWork.Save();
            return RedirectToAction(nameof(Index));
        }

        //get the price of an item based on quantity ordered
        private double GetPriceBasedOnQuantity(ShoppingCart shoppingCart)
        {
            if(shoppingCart.Count <= 50)
            {
                return shoppingCart.Product.Price;
            }
            else
            {
                if(shoppingCart.Count <= 100)
                {
                    return shoppingCart.Product.Price50;
                }
                else
                {
                    return shoppingCart.Product.Price100;
                }
            }
        }

    }
}
