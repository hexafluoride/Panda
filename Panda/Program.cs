using System;
using System.Collections.Generic;
using System.Linq;
using Nancy;
using Nancy.Hosting.Self;
using System.IO;
using System.Web;

namespace Panda
{
    class Program
    {
        static void Main(string[] args)
        {
            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-us");
            MainModule.Files = new DirectoryInfo(MainModule.FilePath).GetDirectories().Select(d => d.Name.ToLower()).ToList(); // populate cached file list
            new NancyHost(new Uri(MainModule.URL)).Start();
            Console.ReadLine();
        }
    }
    public class MainModule : NancyModule
    {
        public static List<string> Files = new List<string>();
        public static string FilePath = "./files";
        public static string URL = "http://localhost:8888/";
        public static long MaxLength = 10000;
        public static Random Random = new Random();
        public MainModule()
        {
            Get["/"] = _ => "<form action=\"upload?output=gyazo\" method=\"post\" enctype=\"multipart/form-data\"><input type=\"file\" id=\"file\" name=\"file\"><input type=\"submit\"></form>";
            Get["/{filename}"] = (parameters) =>
            {
                string filename = parameters["filename"].ToString();
                string id = Path.GetFileNameWithoutExtension(filename);
                if (!Files.Contains(id.ToLower()))
                    return HttpStatusCode.NotFound;
                string actual_path = new DirectoryInfo(Path.Combine(FilePath, id)).GetFiles()[0].FullName;
                return Response.AsRedirect("~/" + id + "/" + HttpUtility.UrlEncode(Path.GetFileName(actual_path))).WithStatusCode(HttpStatusCode.MovedPermanently);
            };
            Get["/{id}/{filename}"] = (parameters) =>
            {
                string id = parameters["id"].ToString();
                if (!Files.Contains(id.ToLower()))
                    return HttpStatusCode.NotFound;
                string actual_path = new DirectoryInfo(Path.Combine(FilePath, id)).GetFiles()[0].FullName;
                return Response.FromStream(new FileStream(actual_path, FileMode.Open), MimeMapping.GetMimeMapping(actual_path));
            };
            Post["/upload"] = (parameters) =>
            {
                if (!this.Request.Files.Any())
                    return "You didn't send any files";
                string output = this.Request.Query["output"] ?? "gyazo";
                Func<string> RandomName = () => { return new string(new char[4].Select(c => (char)Random.Next(65, 90)).ToArray()); };
                string ret = "";
                if (output == "json")
                    ret = "{\"success\": true, \"files\": [";
                if (output == "csv")
                    ret = "name,url,hash,size\n";
                foreach (var file in this.Request.Files)
                {
                    string rnd = RandomName();
                    Directory.CreateDirectory(Path.Combine(FilePath, rnd));
                    file.Value.CopyTo(new FileStream(Path.Combine(FilePath, rnd, file.Name), FileMode.Create));
                    Files.Add(rnd);
                    string hash = BitConverter.ToString(new System.Security.Cryptography.SHA1CryptoServiceProvider().ComputeHash(file.Value)).ToLower().Replace("-", "");
                    if (file.Value.Length > MaxLength)
                    {
                        ret = output == "json" ? "{\"success\": false, \"errorcode\": 0, \"description\": \"File too big!\"}" : "File too big!\n";
                        break;
                    }
                    switch (output)
                    {
                        case "html":
                            ret += URL + rnd + Path.GetExtension(file.Name) + "<br>";
                            break;
                        case "json":
                            ret += "{\"name\": \"" + file.Name + "\", \"url\": \"" + URL + rnd + Path.GetExtension(file.Name) + "\", \"hash\": \"" + hash + "\", \"size\": " + file.Value.Length + "},";
                            break;
                        case "csv":
                            ret += file.Name + "," + URL + rnd + Path.GetExtension(file.Name) + "," + hash + "," + file.Value.Length + "\n";
                            break;
                        case "gyazo":
                        case "text":
                        default:
                            ret += URL + rnd + Path.GetExtension(file.Name) + "\n";
                            break;
                    }
                }
                if (output == "json" && ret.EndsWith(","))
                {
                    ret = ret.Substring(0, ret.Length - 1);
                    ret += "]}";
                }
                if (output == "text")
                    ret = ret.Trim();
                return Response.FromStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(ret)), output == "json" ? "application/json" : output == "csv" ? "text/csv" : output == "html" ? "text/html" : "text/plain");
            };
        }
    }
}