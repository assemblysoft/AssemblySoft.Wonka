using AssemblySoft.DevOps;
using AssemblySoft.IO;
using AssemblySoft.Serialization;
using AssemblySoft.WonkaBuild.Models;
using AssemblySoft.WonkaBuild.shared;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Web.Configuration;
using System.Web.Http;

namespace AssemblySoft.WonkaBuild.Controllers
{
    public class TasksController : ApiController
    {
        const string TASK_DEFINITIONS_ARCHIVE = "TaskDefinitionsArchive";

        [HttpGet]        
        public HttpResponseMessage Definition(string name)
        {
            TaskService svc = new TaskService();
            var model = svc.LoadTaskDefinitionsOrderByLatestVersion().Where(e => e.Project.ToLower() == name.ToLower()).FirstOrDefault();

            // Return the data
            var response = Request.CreateResponse(HttpStatusCode.OK);
            response.Content = new StringContent(JsonConvert.SerializeObject(model), Encoding.UTF8, "application/json");
            return response;
        }

        [HttpGet]
        public HttpResponseMessage Task([FromUri] TaskModel model)
        {
            TaskService svc = new TaskService();
            var tasks = svc.LoadTasks(Path.Combine(model.Path, string.Format("{0}.tasks", model.Project)));

            // Return the data
            var response = Request.CreateResponse(HttpStatusCode.OK);
            response.Content = new StringContent(JsonConvert.SerializeObject(tasks), Encoding.UTF8, "application/json");
            return response;
        }

        [HttpGet]
        public HttpResponseMessage Dependencies(string source)
        {            
            var tmpPath = Path.Combine(Path.GetTempPath(), string.Format("{0}-{1}",WebConfigurationManager.AppSettings[TASK_DEFINITIONS_ARCHIVE],DateTime.Now.ToFileTimeUtc()));
            FileClient.CreateZipFromDirectory(source, tmpPath);
            HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(File.ReadAllBytes(tmpPath)),
                
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