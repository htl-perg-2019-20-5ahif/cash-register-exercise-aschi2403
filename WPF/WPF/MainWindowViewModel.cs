using CashRegister.Shared;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace WPF
{
    public class MainWindowViewModel: BindableBase
    {

        public MainWindowViewModel()
        {
            // Connect the command with the handling function
            AddToBasketCommand = new DelegateCommand<int?>(OnAddToBasket);
            CheckoutCommand = new DelegateCommand(async () => await OnCheckout(), () => Basket.Count > 0);
            Basket.CollectionChanged += (_, __) =>
            {
                CheckoutCommand.RaiseCanExecuteChanged();
                RaisePropertyChanged(nameof(Total));
            };
            /*Products.Add(new Product
            {
                ID = 1,
                ProductName = "Bananen",
                UnitPrice = 1.0M
            });*/
        }

        private HttpClient HttpClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5000"),
            Timeout = TimeSpan.FromSeconds(5)
        };


        private ObservableCollection<Product> products = new ObservableCollection<Product>();
        public ObservableCollection<Product> Products
        {
            get { return products; }
            set { SetProperty(ref products, value); }
        }


        private ObservableCollection<ReceiptLineViewModel> basket = new ObservableCollection<ReceiptLineViewModel>();
        public ObservableCollection<ReceiptLineViewModel> Basket
        {
            get { return basket; }
            set { SetProperty(ref basket, value); }
        }

        public decimal Total
        {
            get
            {
                return Basket.Sum(p => p.TotalPrice);
            }
        }

        public DelegateCommand<int?> AddToBasketCommand { get; }

        private void OnAddToBasket(int? productID)
        {
            // Lookup the product based on the ID
            var product = Products.First(p => p.ID == productID);

            // Check whether the product is already in the basket
            var basketItem = Basket.FirstOrDefault(p => p.ProductID == productID);
            if (basketItem != null)
            {
                // Product already in the basket -> add amount and total price
                basketItem.Amount++;
                basketItem.TotalPrice += product.UnitPrice;
                RaisePropertyChanged(nameof(Total));
            }
            else
            {
                // New product -> add item to basket
                Basket.Add(new ReceiptLineViewModel
                {
                    ProductID = product.ID,
                    Amount = 1,
                    ProductName = product.ProductName,
                    TotalPrice = product.UnitPrice
                });
            }
        }
        public DelegateCommand CheckoutCommand { get; }

        public async Task InitAsync()
        {
            var productsString = await HttpClient.GetStringAsync("/api/products");
            Products = JsonSerializer.Deserialize<ObservableCollection<Product>>(productsString);
        }

        private async Task OnCheckout()
        {
            // Turn all items in the basket into DTO objects
            var dto = Basket.Select(b => new ReceiptLineDto
            {
                ProductID = b.ProductID,
                Amount = b.Amount
            }).ToList();

            // Create JSON content that can be sent using HTTP POST
            using (var content = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json"))
            {
                // Send the receipt to the backend
                var response = await HttpClient.PostAsync("/api/receipts2", content);

                // Throw exception if something went wrong
                response.EnsureSuccessStatusCode();
            }

            // Clear basket so shopping can start from scratch
            Basket.Clear();
        }
    }
}
