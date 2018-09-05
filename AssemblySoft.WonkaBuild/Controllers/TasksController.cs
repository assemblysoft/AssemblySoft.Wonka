using AssemblySoft.DevOps;
using AssemblySoft.IO;
using AssemblySoft.Serialization;
using AssemblySoft.WonkaBuild.Models;
using AssemblySoft.WonkaBuild.shared;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Configuration;
using System.Web.Http;

namespace AssemblySoft.WonkaBuild.Controllers
{
    public class TasksController : ApiController
    {
        
        ITaskService taskSvc;

        public TasksController()
        {

        }

        private static async void GetBlobList(string searchText, CloudBlobContainer blobContainer, IEnumerable<IListBlobItem> blobItemList)
        {
            foreach (var item in blobItemList)
            {
                string line = string.Empty;
                CloudBlockBlob blockBlob = blobContainer.GetBlockBlobReference(item.Uri.ToString());
                if (blockBlob.Name.Contains(".txt"))
                {
                    await Search(searchText, blockBlob);
                }
            }
        }

        private async static Task Search(string searchText, CloudBlockBlob blockBlob)
        {
            string text = await blockBlob.DownloadTextAsync();
            if (text.ToLower().IndexOf(searchText.ToLower()) != -1)
            {
                //Console.WriteLine("Result : " + num + " => " + blockBlob.Name.Substring(blockBlob.Name.LastIndexOf('/') + 1));
                //num++;
            }
        }


        [HttpGet]
        public string Ping(string msg)
        {
            var storageConnectionString = WebConfigurationManager.AppSettings["storageConnectionString"];
            if (string.IsNullOrEmpty(storageConnectionString))
                storageConnectionString = "unable to retrieve";

            return string.Format("Ping: {0} {1}",msg,storageConnectionString);
        }

        [HttpGet]        
        public HttpResponseMessage Definition(string name, string sourceStore="directory")
        {
            //ToDo: Add DI
            if (sourceStore == "blob")
                taskSvc = new BlobTaskService();
            else
                taskSvc = new DirectoryTaskService();

            var model = taskSvc.LoadTaskDefinitionsOrderByLatestVersion().Where(e => e.Task.ToLower() == name.ToLower()).FirstOrDefault();          

            
            // Return the data
            var response = Request.CreateResponse(HttpStatusCode.OK);
            response.Content = new StringContent(JsonConvert.SerializeObject(model), Encoding.UTF8, "application/json");
            return response;
        }

        [HttpGet]
        public HttpResponseMessage Task([FromUri] TaskModel model)
        {
            //ToDo: //Add DI            
            //if (sourceStore == "blob")
                taskSvc = new BlobTaskService();
            //else
              //  taskSvc = new DirectoryTaskService();

            var tasks = taskSvc.LoadTasks(model);
            

            // Return the data
            var response = Request.CreateResponse(HttpStatusCode.OK);
            response.Content = new StringContent(JsonConvert.SerializeObject(tasks), Encoding.UTF8, "application/json");
            return response;
        }

        [HttpGet]
        public HttpResponseMessage Dependencies(string source, string sourceStore, int id)
        {
            if (sourceStore == "blob")
                taskSvc = new BlobTaskService();
            else
                taskSvc = new DirectoryTaskService();

            //var tmpPath = Path.Combine(Path.GetTempPath(), string.Format("{0}-{1}",WebConfigurationManager.AppSettings[TASK_DEFINITIONS_ARCHIVE],DateTime.Now.ToFileTimeUtc()));
            //FileClient.CreateZipFromDirectory(source, tmpPath);
            HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK)
            {
                //Content = new ByteArrayContent(File.ReadAllBytes(tmpPath)),
                Content = new ByteArrayContent(taskSvc.Dependencies(source))

            };
            //result.Content.Headers.ContentType = new MediaTypeHeaderValue("application/zip, application/octet-stream");

            return result;
        }


        public void Post([FromBody]string value)
        {
        }

        // PUT api/<controller>/5
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE api/<controller>/5
        public void Delete(int id)
        {
        }
    }
}