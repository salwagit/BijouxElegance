using System.Collections.Generic;
using BijouxElegance.Models;

namespace BijouxElegance.ViewModels
{
    public class HomePageViewModel
    {
        public List<Product> FeaturedProducts { get; set; } = new List<Product>();
        public List<Category> Categories { get; set; } = new List<Category>();
    }
}