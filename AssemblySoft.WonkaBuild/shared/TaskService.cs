using AssemblySoft.DevOps;
using AssemblySoft.IO;
using AssemblySoft.Serialization;
using AssemblySoft.WonkaBuild.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Configuration;

namespace AssemblySoft.WonkaBuild.shared
{
    public class TaskService
    {
        //Filter
        const string TASKS_FILTER = "TasksFilter";

        private object AsVersionNumber(string number)
        {
            Version result;
            if (Version.TryParse(number, out result))
                return result;
            else
                return default(Version);
        }


        public IEnumerable<DevOpsTask> LoadTasks(string definitionPath)
        {            
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


    }
}