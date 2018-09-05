using AssemblySoft.DevOps;
using AssemblySoft.IO;
using AssemblySoft.Serialization;
using AssemblySoft.WonkaBuild.Models;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Web;
using System.Web.Configuration;

namespace AssemblySoft.WonkaBuild.shared
{
    public interface ITaskService
    {
        IEnumerable<TaskModel> LoadTaskDefinitionsOrderByLatestVersion(int takeCount = 1);
        IEnumerable<DevOpsTask> LoadTasks(TaskModel model);

        IEnumerable<TaskHistory> LoadTaskHistory();

        IEnumerable<TaskModel> LoadTaskDefinitions();

        Byte[] Dependencies(string source);
    }

    public class TaskService
    {
        //Filter
        protected const string TASKS_FILTER = "TasksFilter";
        protected const string TASK_DEFINITIONS_ARCHIVE = "TaskDefinitionsArchive";

        protected object AsVersionNumber(string number)
        {
            Version result;
            if (Version.TryParse(number, out result))
                return result;
            else
                return default(Version);
        }

    }
    

    public class BlobTaskService : TaskService, ITaskService
    {
        private static string GetBlobName(string path)
        {
            var pathParts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            var fileName = string.Join("/", pathParts.Skip(1));
            return fileName;
        }


        public IEnumerable<DevOpsTask> LoadTasks(TaskModel model)
        {
            var storageConnectionString = WebConfigurationManager.AppSettings["storageConnectionString"];
            var containerName = "task-definitions";
            CloudStorageAccount cloudStorageAccount;
            if (CloudStorageAccount.TryParse(storageConnectionString, account: out cloudStorageAccount))
            {
                try
                {
                    CloudBlobClient blobClient = cloudStorageAccount.CreateCloudBlobClient();
                    CloudBlobContainer cloudBlobContainer = blobClient.GetContainerReference(containerName);
                   
                    CloudBlockBlob blob = cloudBlobContainer.GetBlockBlobReference(model.FullName);
                    blob.FetchAttributes();
                    var content = blob.DownloadText();

                    var tasks = XmlSerialisationManager<DevOpsTask>.DeserializeStringObjects(content);
                    if (tasks != null)
                    {
                        return tasks;
                    }

                }
                catch (AggregateException ag)
                {
                    //HandleException(ag);                    
                }
                catch (Exception ex)
                {
                    //HandleException(ex);                    
                }                               
            }                      


            return null;
        }

        /// <summary>
        /// Retrieves collection of Task Definitions, ordered by version, latest first
        /// </summary>
        /// <param name="takeCount">default 1 signifies latest, -1 all</param>
        /// <returns></returns>
        public IEnumerable<TaskModel> LoadTaskDefinitionsOrderByLatestVersion(int takeCount = 1)
        {
            List<TaskModel> tasks = new List<TaskModel>();
            var storageConnectionString = WebConfigurationManager.AppSettings["storageConnectionString"];
            var containerName = "task-definitions";
            CloudStorageAccount cloudStorageAccount;
            if (CloudStorageAccount.TryParse(storageConnectionString, account: out cloudStorageAccount))
            {
                CloudBlobClient blobClient = cloudStorageAccount.CreateCloudBlobClient();
                CloudBlobContainer cloudBlobContainer = blobClient.GetContainerReference(containerName);

                foreach (IListBlobItem blobItem in cloudBlobContainer.ListBlobs())
                {
                    if (blobItem is CloudBlobDirectory)
                    {
                        CloudBlobDirectory directory = (CloudBlobDirectory)blobItem;
                        IEnumerable<IListBlobItem> blobs = directory.ListBlobs(true);
                        ICloudBlob blockBlob;
                        foreach (var blob in blobs)
                        {
                            if (blob is CloudBlockBlob)
                            {
                                blockBlob = blob as CloudBlockBlob;

                                if (blockBlob.Name.Contains(".tasks"))
                                {
                                    //string text = blockBlob.d.DownloadTextAsync().Result;

                                    var parts = blockBlob.Name.Split('/');

                                    tasks.Add(new TaskModel()
                                    {
                                        Task = Path.GetFileNameWithoutExtension(blockBlob.Name),
                                        FullName = blockBlob.Name,
                                        Path = blockBlob.StorageUri.PrimaryUri.ToString(),
                                        Project = parts[parts.Length - 3],
                                        Definition = "N/A",
                                        Version = parts[parts.Length - 2],
                                    });

                                }                               

                            }
                        }
                    }
                }

            }
            else
            {
                //ToDo: define logger

                // Otherwise, let the user know that they need to define the environment variable.
                //Console.WriteLine(
                //    "A connection string has not been defined in the system environment variables. " +
                //    "Add a environment variable named 'storageconnectionstring' with your storage " +
                //    "connection string as a value.");
                //Console.WriteLine("Press any key to exit the sample application.");
                //Console.ReadLine();
            }

            return tasks.OrderByDescending(e=> AsVersionNumber(e.Version));
        }

        public IEnumerable<TaskHistory> LoadTaskHistory()
        {
            var tasksRunnerRootPath = ConfigurationManager.AppSettings["tasksRunnerRootPath"];

            if (!Directory.Exists(tasksRunnerRootPath))
            {
                throw new DirectoryNotFoundException(BuildResources.Error_CannotfindTaskDefinitions);
            }

            DirectoryInfo projectInfo = new DirectoryInfo(tasksRunnerRootPath);
            var projectDirectories = projectInfo.EnumerateDirectories();
            List<TaskHistory> taskHistory = new List<TaskHistory>();
            foreach (var projDir in projectDirectories)
            {
                DirectoryInfo info = new DirectoryInfo(Path.Combine(tasksRunnerRootPath, projDir.Name));
                var directories = info.EnumerateDirectories();
                foreach (var dir in directories)
                {
                    var files = dir.GetFiles(WebConfigurationManager.AppSettings[TASKS_FILTER]);

                    foreach (var file in files)
                    {
                        var status = @"<i class='fa fa-times fa-2x faulted'></i>";
                        var buildLog = FileClient.ReadAllText(Path.Combine(dir.FullName, "build.log"));
                        if (System.IO.File.Exists(Path.Combine(dir.FullName, "completed.dat")))
                        {
                            status = @"<i class='fa fa-check fa-2x completed'></i>";
                        }


                        taskHistory.Add(
                        new TaskHistory()
                        {
                            Task = new TaskModel()
                            {
                                Task = Path.GetFileNameWithoutExtension(file.Name),
                                FullName = file.Name,
                                Path = dir.FullName,
                                Project = dir.Name,
                            },
                            Summary = new TaskSummaryModel()
                            {
                                BuildLog = string.IsNullOrEmpty(buildLog) ? "N/A" : buildLog,
                                BuildLabel = "this label",
                            },
                            Status = status
                        });
                    }
                }
            }

            return taskHistory;
        }

        public IEnumerable<TaskModel> LoadTaskDefinitions()
        {
            List<TaskModel> tasks = new List<TaskModel>();
            var tasksDestinationRootPath = ConfigurationManager.AppSettings["tasksDefinitionsRootPath"];

            if (!Directory.Exists(tasksDestinationRootPath))
            {
                throw new DirectoryNotFoundException(BuildResources.Error_CannotfindTaskDefinitions);
            }

            DirectoryInfo info = new DirectoryInfo(tasksDestinationRootPath);
            var directories = info.EnumerateDirectories();
            foreach (var dir in directories)
            {
                var files = dir.GetFiles(WebConfigurationManager.AppSettings[TASKS_FILTER]);

                foreach (var file in files)
                {
                    var model = (new TaskModel()
                    {
                        Task = Path.GetFileNameWithoutExtension(file.Name),
                        FullName = file.Name,
                        Path = dir.FullName,
                        Project = dir.Name,

                    });

                    var definition = FileClient.ReadAllText(file.FullName);

                    tasks.Add(new TaskModel()
                    {
                        Task = Path.GetFileNameWithoutExtension(file.Name),
                        FullName = file.Name,
                        Path = dir.FullName,
                        Project = dir.Name,
                        Definition = definition,
                    });
                }
            }


            return tasks;
        }

        public Byte[] Dependencies(string source)
        {
            var storageConnectionString = WebConfigurationManager.AppSettings["storageConnectionString"];
            var containerName = "task-definitions";
            CloudStorageAccount cloudStorageAccount;
            if (CloudStorageAccount.TryParse(storageConnectionString, account: out cloudStorageAccount))
            {
                CloudBlobClient blobClient = cloudStorageAccount.CreateCloudBlobClient();
                CloudBlobContainer cloudBlobContainer = blobClient.GetContainerReference(containerName);

                var depends = Path.ChangeExtension(source,"zip");
                CloudBlockBlob blob = cloudBlobContainer.GetBlockBlobReference(depends); //"LIFE/Development/18.3.0.9/Developer-Setup.tasks"
                blob.FetchAttributes();               
                long fileByteLength = blob.Properties.Length;
                byte[] fileContent = new byte[fileByteLength];
                var res = blob.DownloadToByteArray(fileContent,0);

                return fileContent;

            }

            return null;
        }
    }

    public class DirectoryTaskService : TaskService, ITaskService
    { 

        public IEnumerable<DevOpsTask> LoadTasks(TaskModel model)
        {
            var definitionPath = Path.Combine(model.Path, string.Format("{0}.tasks", model.Project));

            var tasks = XmlSerialisationManager<DevOpsTask>.DeserializeObjects(definitionPath);
            if (tasks != null)
            {
                return tasks;
            }

            return null;
        }

        /// <summary>
        /// Retrieves collection of Task Definitions, ordered by version, latest first
        /// </summary>
        /// <param name="takeCount">default 1 signifies latest, -1 all</param>
        /// <returns></returns>
        public IEnumerable<TaskModel> LoadTaskDefinitionsOrderByLatestVersion(int takeCount = 1)
        {
            List<TaskModel> tasks = new List<TaskModel>();
            var tasksDestinationRootPath = ConfigurationManager.AppSettings["tasksDefinitionsRootPath"];

            if (!Directory.Exists(tasksDestinationRootPath))
            {
                throw new DirectoryNotFoundException(BuildResources.Error_CannotfindTaskDefinitions);
            }

            DirectoryInfo groupInfo = new DirectoryInfo(tasksDestinationRootPath);
            var groupDirectories = groupInfo.EnumerateDirectories();
            foreach (var groupDir in groupDirectories)
            {
                DirectoryInfo projectInfo = new DirectoryInfo(groupDir.FullName);
                var projectDirectories = projectInfo.EnumerateDirectories();
                foreach (var projectDir in projectDirectories)
                {
                    //Take latest version directory for each project
                    DirectoryInfo versionInfo = new DirectoryInfo(projectDir.FullName);
                    IEnumerable<DirectoryInfo> versionDirectories = null;
                    if (takeCount != -1)
                        versionDirectories = versionInfo.EnumerateDirectories().OrderByDescending(e => AsVersionNumber(e.Name)).Take(takeCount);
                    else
                        versionDirectories = versionInfo.EnumerateDirectories().OrderByDescending(e => AsVersionNumber(e.Name));

                    foreach (var taskDir in versionDirectories)
                    {
                        DirectoryInfo info = new DirectoryInfo(taskDir.FullName);
                        var files = info.EnumerateFiles(WebConfigurationManager.AppSettings[TASKS_FILTER]);

                        foreach (var file in files)
                        {
                            var definition = FileClient.ReadAllText(file.FullName);

                            tasks.Add(new TaskModel()
                            {
                                Task = Path.GetFileNameWithoutExtension(file.Name),
                                FullName = file.Name,
                                Path = info.FullName,
                                Project = projectDir.Name,
                                Definition = definition,
                                Version = info.Name

                            });
                        }

                    }
                }

            }

            return tasks;
        }

        public IEnumerable<TaskHistory> LoadTaskHistory()
        {
            var tasksRunnerRootPath = ConfigurationManager.AppSettings["tasksRunnerRootPath"];

            if (!Directory.Exists(tasksRunnerRootPath))
            {
                throw new DirectoryNotFoundException(BuildResources.Error_CannotfindTaskDefinitions);
            }

            DirectoryInfo projectInfo = new DirectoryInfo(tasksRunnerRootPath);
            var projectDirectories = projectInfo.EnumerateDirectories();
            List<TaskHistory> taskHistory = new List<TaskHistory>();
            foreach (var projDir in projectDirectories)
            {
                DirectoryInfo info = new DirectoryInfo(Path.Combine(tasksRunnerRootPath, projDir.Name));
                var directories = info.EnumerateDirectories();
                foreach (var dir in directories)
                {
                    var files = dir.GetFiles(WebConfigurationManager.AppSettings[TASKS_FILTER]);

                    foreach (var file in files)
                    {
                        var status = @"<i class='fa fa-times fa-2x faulted'></i>";
                        var buildLog = FileClient.ReadAllText(Path.Combine(dir.FullName, "build.log"));
                        if (System.IO.File.Exists(Path.Combine(dir.FullName, "completed.dat")))
                        {
                            status = @"<i class='fa fa-check fa-2x completed'></i>";
                        }


                        taskHistory.Add(
                        new TaskHistory()
                        {
                            Task = new TaskModel()
                            {
                                Task = Path.GetFileNameWithoutExtension(file.Name),
                                FullName = file.Name,
                                Path = dir.FullName,
                                Project = dir.Name,
                            },
                            Summary = new TaskSummaryModel()
                            {
                                BuildLog = string.IsNullOrEmpty(buildLog) ? "N/A" : buildLog,
                                BuildLabel = "this label",
                            },
                            Status = status
                        });
                    }
                }
            }

            return taskHistory;
        }

        public IEnumerable<TaskModel> LoadTaskDefinitions()
        {
            List<TaskModel> tasks = new List<TaskModel>();
            var tasksDestinationRootPath = ConfigurationManager.AppSettings["tasksDefinitionsRootPath"];

            if (!Directory.Exists(tasksDestinationRootPath))
            {
                throw new DirectoryNotFoundException(BuildResources.Error_CannotfindTaskDefinitions);
            }

            DirectoryInfo info = new DirectoryInfo(tasksDestinationRootPath);
            var directories = info.EnumerateDirectories();
            foreach (var dir in directories)
            {
                var files = dir.GetFiles(WebConfigurationManager.AppSettings[TASKS_FILTER]);

                foreach (var file in files)
                {                    
                    var definition = FileClient.ReadAllText(file.FullName);

                    tasks.Add(new TaskModel()
                    {
                        Task = Path.GetFileNameWithoutExtension(file.Name),
                        FullName = file.Name,
                        Path = dir.FullName,
                        Project = dir.Name,
                        Definition = definition,
                    });
                }
            }


            return tasks;
        }

        public Byte[] Dependencies(string source)
        {
            var tmpPath = Path.Combine(Path.GetTempPath(), string.Format("{0}-{1}", WebConfigurationManager.AppSettings[TASK_DEFINITIONS_ARCHIVE], DateTime.Now.ToFileTimeUtc()));
            FileClient.CreateZipFromDirectory(source, tmpPath);
            return File.ReadAllBytes(tmpPath);
        }
    }


}