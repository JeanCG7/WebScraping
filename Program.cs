using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Web;
using CsvHelper;
using HtmlAgilityPack;
using ScrapySharp.Exceptions;
using ScrapySharp.Network;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;

namespace PelandoSalesWebScrap
{
    class Product
    {
        public string Name { get; set; }
        public string Price { get;  set; }
        public int Temperature { get; set; }
        public string CommentsQuantity { get; set;}
        public string Link { get; set; }
    }

    class Program
    {
        static ScrapingBrowser Browser = new ScrapingBrowser();
        static string Search = "";

        static bool IsInputValid()
        {
            if (string.IsNullOrWhiteSpace("Search")) {
                Console.WriteLine("Você deve digitar o nome do produto antes de continuar.");
                return false;
            }

            var invalidCharacters = new List<string>{ "/", "\\", "\"" };
            if (invalidCharacters.Any(a => Search.Contains(a))) {
                Console.WriteLine("Caracter inválido, digite novamente");
                return false;
            }

            return true;
        }

        static void Main(string[] args)
        {
            do 
            {
                Console.WriteLine("Digite o nome de um produto que deseja pesquisar no pelando.com.br");
                Search = Console.ReadLine();
            }
            while(!IsInputValid());
            
            var mainPage = GetHtml($"https://www.pelando.com.br/search?q={HttpUtility.UrlEncode(Search)}");
            var products = GetProducts(mainPage.OwnerDocument.DocumentNode.SelectNodes("/html/body/main/div[2]/section[2]")[0].ChildNodes);
            ExportToCsv(products);
            Console.WriteLine("Download concluído. Pressione qualquer tecla para sair");
            Console.Read();            
        }

        static HtmlNode GetHtml(string url)
        {
            WebPage page = Browser.NavigateToPage(new Uri(url));
            return page.Html;
        }

        static List<Product> GetProducts(HtmlNodeCollection productsNodes)
        {
            Console.WriteLine("Baixando CSV com as ofertas para seu computador");
            var products = new List<Product>();
            var lockObject = new Object();
            
            Parallel.ForEach(Partitioner.Create(0, productsNodes.Count), 
            range  => {
                if (productsNodes[range.Item1].Name != "div")
                    return;

                var productId = GetProductId(productsNodes[range.Item1]);
                if (string.IsNullOrWhiteSpace(productId))
                    return;

                string name = "", price = "", link = "", commentsQuantity = "", stringTemperature = "";
                int temperature = 0;
                
                GetValueByXPath(productsNodes[range.Item1], $"//*[@id='thread_{productId}']/div[1]/div[1]/span", out stringTemperature);

                Parallel.Invoke(
                    () => GetValueByXPath(productsNodes[range.Item1], $"//*[@id='thread_{productId}']/div[3]/strong/a/text()", out name),
                    () => GetValueByXPath(productsNodes[range.Item1], $"//*[@id='thread_{productId}']/div[4]/span[1]/span", out price),
                    () => FormatHtmlString(productsNodes[range.Item1].SelectSingleNode($"//*[@id='thread_{productId}']/div[3]/strong/a").Attributes[2].Value, out link),
                    () => GetValueByXPath(productsNodes[range.Item1], $"//*[@id='thread_{productId}']/div[6]/div[2]/a/span", out commentsQuantity),
                    () => GetTemperature(stringTemperature, out temperature)
                );

                var product = new Product() {
                    Name = name,
                    Price = price,
                    Link = link,
                    CommentsQuantity = commentsQuantity,
                    Temperature = temperature
                };

                lock(lockObject){
                    products.Add(product);
                }
            });

            return products.OrderByDescending(o => o.Temperature).ToList();
        }
        
        static void GetValueByXPath(HtmlNode productNode, string xPath, out string text)
        {
            FormatHtmlString(productNode.SelectSingleNode(xPath)?.InnerText, out text);
        }

        static string GetProductId(HtmlNode productNode)
        {
            if (productNode.ChildNodes[0].Attributes.Count < 5)
                return null;

            var id = productNode.ChildNodes[0].Attributes[3].Value;
            return id.Substring(14, 6);
        }

        static void FormatHtmlString(string text, out string encodedText)
        {
            if (string.IsNullOrWhiteSpace(text)){
                encodedText = "Sem preço informado";
                return;
            }

            encodedText = WebUtility.HtmlDecode(text).Replace("\n", "").Replace("\t", "");
        }

        static void GetTemperature(string stringTemperature, out int temperature)
        {
            temperature = Int32.Parse(stringTemperature.Substring(0, stringTemperature.IndexOf('°')));
        }
       
        static void ExportToCsv(List<Product> lstProducts)
        {
            var fileName = $"{Search}_{DateTime.Now.ToString()}".Replace(@"/", "-").Replace(":", "-");
            using (var writer = new StreamWriter($"C:/Users/jeang/Desktop/Projetos/Everis/PelandoSalesWebScrap/{fileName}.csv", false, Encoding.UTF8))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture)) {
                csv.WriteRecords(lstProducts);
            }
        }
    }
}
