using Bulky.Models.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bulky.Models.ViewModels
{   // order header and order details
	public class OrderVM
	{
		public OrderHeader OrderHeader { get; set; }	
		public IEnumerable<OrderDetail> OrderDetail { get; set; }
	}
}
