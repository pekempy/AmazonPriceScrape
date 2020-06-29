using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using HtmlAgilityPack;
using Renci.SshNet;

namespace AmazonPriceScrape
{
    class Program
    {
        //Configure FTP info here
        //File is uploaded to a folder named "/pricescrape/"
        //Configure your website to find /pricescrape/output.csv"
        public string host = "hostaddress";
        public string user = "username";
        public string pass = "password";
        //End config
        public string csv;
        public List<string> AISINs = new List<string>();
        public List<Results> Results = new List<Results>();
        static void Main(string[] args)
        {
            Program pr = new Program();
            pr.DownloadAISINs();
            pr.FindPrice();
            pr.WriteCSV();
            pr.UploadFile();
        }
        public void DownloadAISINs()
        {
            //Fetch AISINs from URL and add them to AISINs list.
            string URL = "http://pekempy.co.uk/php/queryaisin";
            var client = new WebClient();
            var headers = new WebHeaderCollection();
            headers.Add(HttpRequestHeader.Accept, "text/html, application/xhtml+xml, */*");
            headers.Add(HttpRequestHeader.AcceptLanguage, "en-GB");
            headers.Add(HttpRequestHeader.UserAgent, "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/77.0.3865.90 Safari/537.36");
            client.Headers = headers;
            client.Encoding = System.Text.Encoding.UTF8;
            var rawHtml = client.DownloadString(URL);
            var aisinTempList = rawHtml.Split(',');
            foreach (var AISIN in aisinTempList)
            {
                if (AISIN != "")
                {
                    AISINs.Add(AISIN);
                }
            }
        }
        public void FindPrice()
        {
            int userAgentID = 0;
            foreach (var AISIN in AISINs)
            {
                string URL = "https://amazon.co.uk/dp/" + AISIN;
                string randomUserAgent = ChooseUserAgent(userAgentID);
                List<string> IDsToFind = new List<string>();
                var client = new WebClient();
                var headers = new WebHeaderCollection();
                headers.Add(HttpRequestHeader.Accept, "text/html, application/xhtml+xml, */*");
                headers.Add(HttpRequestHeader.AcceptLanguage, "en-GB");
                headers.Add(HttpRequestHeader.UserAgent, randomUserAgent);
                client.Headers = headers;
                client.Encoding = System.Text.Encoding.UTF8;
                var rawhtml = client.DownloadString(URL);
                rawhtml = Regex.Replace(rawhtml, @"\r\n?|\n|\t|\""", "");
                rawhtml = rawhtml.Replace("Â", "");
                HtmlAgilityPack.HtmlDocument Html = new HtmlAgilityPack.HtmlDocument();
                Html.LoadHtml(rawhtml);
                IDsToFind.Add("priceblock_ourprice");
                IDsToFind.Add("buyNewSection");
                IDsToFind.Add("usedBuySection");
                string itemPrice = "Unknown";
                string productTitle = "Unknown";
                try
                {
                    for (int id = 0; id < IDsToFind.Count; id++)
                    {
                        try
                        {
                            Random rnd = new Random();
                            foreach (HtmlNode node in Html.DocumentNode.SelectNodes("//span[@id='" + IDsToFind[id] + "']"))
                            {
                                string idvalue = node.InnerText;
                                itemPrice = ReplaceFiller(idvalue);
                            }
                            int sleep = rnd.Next(1000, 5000);
                            Thread.Sleep(sleep);
                        }
                        catch { }
                        try
                        {
                            Random rnd = new Random();
                            foreach (HtmlNode node in Html.DocumentNode.SelectNodes("//span[@id='productTitle']"))
                            {
                                productTitle = node.InnerText;
                            }
                        }
                        catch { }
                        if (itemPrice == "Unknown")
                        {
                            try
                            {
                                Random rnd = new Random();
                                foreach (HtmlNode node in Html.DocumentNode.SelectNodes("//span[@class='a-color-price']"))
                                {
                                    string idvalue = node.InnerText;
                                        if (idvalue.Contains("£"))
                                        {
                                            itemPrice = ReplaceFiller(idvalue);
                                        break;
                                    }
                                    }
                                int sleep = rnd.Next(1000, 5000);
                                Thread.Sleep(sleep);
                            }
                            catch (Exception e) { Console.WriteLine("Can't find class because: " + e); }
                        }
                        Results.Add(new Results
                        {
                            AISIN = AISIN,
                            Price = itemPrice,
                            ItemName = productTitle
                        });
                        if (productTitle.Length > 20)
                        {
                            Console.WriteLine(AISIN + "        " + itemPrice + "        " + productTitle.Substring(0, 20));
                        }
                        else { Console.WriteLine(AISIN + "        " + itemPrice + "        " + productTitle); }
                        break;
                    }
                }
                catch
                {
                    Console.WriteLine("Error with ID");
                    userAgentID++;
                }
            }
        }
        public void WriteCSV()
        {
            try { File.Delete("output.csv"); } catch { }
            csv = "Store,ID,Price" + System.Environment.NewLine;
            csv = csv + "Updated,DateTime,Prices updated at: " + DateTime.Now.ToString("dd MMM yyy HH:mm:ss") + System.Environment.NewLine;
            for (int aisin = 0; aisin < Results.Count; aisin++)
            {
                csv = csv + "Amazon," + Results[aisin].AISIN + "," + Results[aisin].Price + System.Environment.NewLine;
            }
            File.WriteAllText("output.csv", csv);
            Console.WriteLine("All prices have been found.");
        }
        public void UploadFile()
        {
            string file = "output.csv";
            using (var client = new WebClient())
            {
                string decodepass;
                byte[] data = Convert.FromBase64String(pass);
                decodepass = System.Text.Encoding.ASCII.GetString(data);

                using (var sftpClient = new SftpClient(host, 22, user, decodepass))
                {
                    sftpClient.Connect();
                    sftpClient.ChangeDirectory("/pricescrape/");

                    using (var fileStream = new FileStream(file, FileMode.Open))
                    {
                        sftpClient.BufferSize = 4 * 1024;
                        sftpClient.UploadFile(fileStream, Path.GetFileName(file));
                    }
                }
            }
        }
        public string ChooseUserAgent(int userAgentID) {
            List<string> userAgents = new List<string>();
            userAgents.Add("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/81.0.4044.92 Safari/537.36");
            userAgents.Add("Mozilla/5.0 (Windows NT 6.3; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/83.0.4103.61 Safari/537.36");
            userAgents.Add("Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/83.0.4103.116 Safari/537.36");
            userAgents.Add("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_14_6) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/13.1 Safari/605.1.15");
            userAgents.Add("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/83.0.4103.106 Safari/537.36 Edg/83.0.478.54");
            userAgents.Add("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/77.0.3865.90 Safari/537.36");
            userAgents.Add("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:75.0) Gecko/20100101 Firefox/75.0");
            userAgents.Add("Mozilla/5.0 (compatible; MSIE 9.0; AOL 9.7; AOLBuild 4343.19; Windows NT 6.1; WOW64; Trident/5.0; FunWebProducts)");
            userAgents.Add("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/83.0.4103.61 Safari/537.36");
            userAgents.Add("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/83.0.4103.106 Safari/537.36");
            userAgents.Add("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/80.0.3987.132 Safari/537.36");
            userAgents.Add("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_13_6) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/83.0.4103.97 Safari/537.36");
            userAgents.Add("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/83.0.4103.61 Safari/537.36");
            userAgents.Add("Mozilla/5.0 (X11; Ubuntu; Linux x86_64; rv:77.0) Gecko/20100101 Firefox/77.0");
            return userAgents[userAgentID];
        }
        public string ReplaceFiller(string input)
        {
            input = input.Replace(" ", "");
            input = input.Replace("BuyNew", "");
            input = input.Replace("Buynew:", "");
            input = input.Replace("Price:", "");
            return input;
        }
    }
    public class Results
    {
        public string AISIN { get; set; }
        public string Price { get; set; }
        public string ItemName { get; set; }
    }
}
