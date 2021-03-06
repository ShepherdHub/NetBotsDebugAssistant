﻿using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Owin;
using Newtonsoft.Json.Linq;
using Owin;

[assembly: OwinStartup(typeof(NetBotLocalAssistant.Startup))]

namespace NetBotLocalAssistant
{
    public class Startup
    {
        private readonly string _basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        private HttpClient _client = new HttpClient();

        public void Configuration(IAppBuilder app)
        {
            app.Run(context =>
            {
                var path = GetPath(context);
                var contentType = GetContentType(path);
                context.Response.ContentType = contentType;

                if (contentType == "text/json")
                {
                    var stringToRender = RelayJsonRequest(context).Result;
                    return context.Response.WriteAsync(stringToRender);
                }
                byte[] file = GetFileBytes(path);
                return context.Response.WriteAsync(file);
            });
        }

        private async Task<string> RelayJsonRequest(IOwinContext context)
        {
            try
            {
                byte[] bytes = GetBytesFromBody(context.Request.Body);
                var myString = Encoding.Default.GetString(bytes);
                myString = System.Net.WebUtility.UrlDecode(myString);
                var myObject = JObject.Parse(myString);
                var payload = myObject["payload"];
                var address = (string)myObject["destination"];
                if (!address.StartsWith("http://") && !address.StartsWith("https://"))
                {
                    address = "http://" + address;
                }
                string payloadJson = payload.ToString();
                var content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
                if (!String.IsNullOrWhiteSpace(payloadJson))
                {
                    var response = await _client.PostAsync(address, content);
                    response.EnsureSuccessStatusCode();
                    var responeString = await response.Content.ReadAsStringAsync();
                    if (String.IsNullOrWhiteSpace(responeString))
                    {
                        responeString = "{ }";
                    }
                    return responeString;
                }
                else
                {
                    var response = await _client.GetAsync(address);
                    response.EnsureSuccessStatusCode();
                    var responeString = await response.Content.ReadAsStringAsync();
                    if (String.IsNullOrWhiteSpace(responeString))
                    {
                        responeString = "{ }";
                    }
                    return responeString;
                }
            }
            catch (Exception ex)
            {
                WriteErrorToConsole("Error communicating with local testing bot. Full error was:", ex);
                return "{ Error: " + ex.Message + " }. Check the Console for more detailed inforamton." +
                       "If your bot has CORS, you may want to check the 'Enable CORS' button";
            }
        }

        private static void WriteErrorToConsole(string errorMessage, Exception ex)
        {
            Console.WriteLine(errorMessage);
            Console.WriteLine(ex.Message);
            Console.WriteLine(ex);
        }

        private byte[] GetBytesFromBody(Stream input)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }

        private string GetContentType(string path)
        {
            var extension = GetExtension(path);
            switch (extension)
            {
                case "js": return "application/javascript";

                case "html":
                case "htm": 
                case "css":
                case "json": return "text/" + extension;

                case "png": 
                case "jpg":
                case "gif": return "image/" + extension;

                case "woff": return "application/font-woff";
                default: return "application/" + extension;
            }
        }

        private string GetExtension(string path)
        {
            var lastPeriod = path.LastIndexOf('.');
            var extension = path.Substring(lastPeriod + 1, path.Length - lastPeriod - 1);
            return extension;
        }

        private static string GetPath(IOwinContext context)
        {
            var path = context.Request.Path.ToString();
            if (path == "/")
            {
                path += "index.html";
            }
            return path;
        }

        private byte[] GetFileBytes(string path)
        {
            try
            {
                var fullPath = _basePath + "\\Website" + path;
                fullPath = fullPath.Replace('/', '\\');
                byte[] bytes = File.ReadAllBytes(fullPath);
                return bytes;
            }
            catch (Exception ex)
            {
                WriteErrorToConsole("Error loading binary file " + path, ex);
                return null;
            }
        }
    }
}
