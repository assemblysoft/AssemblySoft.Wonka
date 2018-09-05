using System;
using System.Configuration;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Mvc;

using AssemblySoft.DevOps;
using AssemblySoft.WonkaBuild.Hubs;
using AssemblySoft.WonkaBuild.Models;
using AssemblySoft.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Configuration;
using AssemblySoft.WonkaBuild.shared;

namespace AssemblySoft.WonkaBuild.Controllers
{
    public class BuildController : Controller
    {
        #region Constants
        //Files
        const string COMPLETED_DATA_FILE = "CompletedDataFile";
        const string ERROR_DATA_FILE = "ErrorDataFile";
        const string PROCESSING_DATA_FILE = "ProcessingDataFile";        
        const string BUILD_LOG_DATA_FILE = "BuildLogDataFile";
        const string FILE_WONKA_LOG = "WonkaLogFile";
        const string TASK_DEFINITIONS_ARCHIVE = "TaskDefinitionsArchive";


        //Session
        const string SESSION_KEY_RUN_PATH = "SessionRunPath";
        const string TASK_NAME = "SessionTaskName";

        #endregion

        ITaskService taskSvc;

        public BuildController()
        {
            //ToDo: //Add DI
            taskSvc = new DirectoryTaskService();
        }

        public ActionResult Index()
        {
            return View();
        }

        public ActionResult About()
        {
            ViewData["Message"] = "Ver 1.0.0";

            return View();
        }

        public ActionResult Help()
        {
            return View();
        }

        public ActionResult Error()
        {
            return View();
        }


        #region Task Actions


        public string AddStatus(string msg)
        {
            AddMessage(msg);

            return msg;
        }

        //public TaskModel GetDevTasks()
        //{
        //    return new TaskModel()
        //    {
        //        D
        //    };
        //}


        public ActionResult LoadHistory()
        {
            try
            {             
                
                AddMessage(BuildResources.Status_LoadingHistory);
                
                var model = taskSvc.LoadTaskHistory();
                if (model == null || !model.Any())
                {
                    AddMessage(BuildResources.Error_UnableToFindTaskHistoryToLoad);
                }
                else
                {
                    AddMessage(BuildResources.Status_CompletedLoadingHistory);
                }


                return PartialView("_LoadHistory", model.OrderByDescending(e => Number(e.Task.Project))); //order by project
            }
            catch (Exception e)
            {
                HandleException(e);
            }

            return PartialView("_LoadHistory");
        }

        public ActionResult LoadTasks()
        {
            try
            {
                AddMessage(BuildResources.Status_LoadingTasks);                
                var model = taskSvc.LoadTaskDefinitionsOrderByLatestVersion(-1);
                if (model == null || !model.Any())
                {
                    AddMessage(BuildResources.Error_UnableTofindTasksToLoad);
                }
                else
                {
                    AddMessage(BuildResources.Status_CompletedLoadingTasks);
                }

                //determine latest
                return PartialView("_LoadTasks", model);
            }
            catch (Exception e)
            {
                HandleException(e);
            }

            return PartialView("_LoadTasks");
        }

        public ActionResult LoadTasksByLatestVersion()
        {
            try
            {                
                AddMessage(BuildResources.Status_LoadingTasks);
                
                var model = taskSvc.LoadTaskDefinitionsOrderByLatestVersion(1);
                if (model == null || !model.Any())
                {
                    AddMessage(BuildResources.Error_UnableTofindTasksToLoad);
                }
                else
                {
                    AddMessage(BuildResources.Status_CompletedLoadingTasks);
                }

                //determine latest
                return PartialView("_LoadTasks", model);
            }
            catch (Exception e)
            {
                HandleException(e);
            }

            return PartialView("_LoadTasks");
        }

        public ActionResult Run(TaskModel taskModel)
        {
            var startTime = DateTime.Now;
            Session.Clear();

            AddMessage(BuildResources.Status_StartingBuild);
            string runPath = string.Empty;

            try
            {
                runPath = InitialiseBuildRun(taskModel.Path, taskModel.Project);

                Session[WebConfigurationManager.AppSettings[SESSION_KEY_RUN_PATH]] = runPath;
                Session[WebConfigurationManager.AppSettings[TASK_NAME]] = taskModel.Task;
            }
            catch (Exception e)
            {
                HandleException(e);
                return PartialView("_TasksFail");
            }


            var taskRunner = new TaskRunner(runPath);
            taskRunner.TaskStatus += (e) =>
            {
                AddMessage(e.Status);
            };

            taskRunner.TasksCompleted += (e) =>
            {
                while (Broadcaster.Instance.MessageCount > 1)
                {
                    Thread.Sleep(2000);
                }

                if (e.Status == TaskStatus.Faulted.ToString())
                {
                    System.IO.File.Create(Path.Combine(runPath, WebConfigurationManager.AppSettings[ERROR_DATA_FILE]));
                }
                else
                {
                    var timeTaken = DateTime.UtcNow - startTime;
                    System.IO.File.Create(Path.Combine(runPath, WebConfigurationManager.AppSettings[COMPLETED_DATA_FILE]));
                    
                    //ToDo:
                    //FileClient.WriteTextToFile(Path.Combine(runPath, COMPLETED_DATA_FILE), timeTaken.ToString());
                }

            };

            string tasksPath = string.Empty;

            try
            {
                CancellationTokenSource _cts = new CancellationTokenSource();
                var token = _cts.Token;

                Task t1 = new Task(() =>
                {
                    var path = Path.Combine(runPath, WebConfigurationManager.AppSettings[PROCESSING_DATA_FILE]);
                    System.IO.File.Create(Path.Combine(runPath, WebConfigurationManager.AppSettings[PROCESSING_DATA_FILE]));

                    tasksPath = Path.Combine(runPath, taskModel.FullName);

                    taskRunner.Run(token, Path.Combine(tasksPath));

                });

                Task t2 = t1.ContinueWith((ante) =>
                {
                }, TaskScheduler.FromCurrentSynchronizationContext());

                t1.Start();
            }
            catch (DevOpsTaskException ex)
            {
                HandleException(ex);
                return PartialView("_TasksFail");
            }
            catch (AggregateException ag)
            {
                HandleException(ag);
                return PartialView("_TasksFail");
            }
            catch (Exception ex)
            {
                HandleException(ex);
                return PartialView("_TasksFail");
            }
            finally
            {
                var tasks = taskRunner.GetDevOpsTaskWithState();
                //could serialize the state back to the tasks file for verification
                //taskRunner.S.SerializeTasksToFile(tasks, ConfigurationManager.AppSettings["tasksPath"]);
            }

            var model = new TaskInformationModel()
            {
                TaskName = taskModel.Task,
                TasksPath = taskModel.Path,
                TasksStartTime = startTime.ToString(),
            };

            return PartialView("_TasksRunning", model);
        }

        public ActionResult AgentRun(TaskModel taskModel)
        {
            var startTime = DateTime.Now;
            Session.Clear();

            AddMessage(BuildResources.Status_StartingBuild);
            string runPath = string.Empty;

            try
            {
                runPath = InitialiseBuildRun(taskModel.Path, taskModel.Project);

                Session[WebConfigurationManager.AppSettings[SESSION_KEY_RUN_PATH]] = runPath;
                Session[WebConfigurationManager.AppSettings[TASK_NAME]] = taskModel.Task;
            }
            catch (Exception e)
            {
                HandleException(e);
                return PartialView("_TasksFail");
            }


            var taskRunner = new TaskRunner(runPath);
            taskRunner.TaskStatus += (e) =>
            {
                AddMessage(e.Status);
            };

            taskRunner.TasksCompleted += (e) =>
            {
                while (Broadcaster.Instance.MessageCount > 1)
                {
                    Thread.Sleep(2000);
                }

                if (e.Status == TaskStatus.Faulted.ToString())
                {
                    System.IO.File.Create(Path.Combine(runPath, WebConfigurationManager.AppSettings[ERROR_DATA_FILE]));
                }
                else
                {
                    var timeTaken = DateTime.UtcNow - startTime;
                    System.IO.File.Create(Path.Combine(runPath, WebConfigurationManager.AppSettings[COMPLETED_DATA_FILE]));

                    //ToDo:
                    //FileClient.WriteTextToFile(Path.Combine(runPath, COMPLETED_DATA_FILE), timeTaken.ToString());
                }

            };

            string tasksPath = string.Empty;

            try
            {
                CancellationTokenSource _cts = new CancellationTokenSource();
                var token = _cts.Token;

                Task t1 = new Task(() =>
                {
                    var path = Path.Combine(runPath, WebConfigurationManager.AppSettings[PROCESSING_DATA_FILE]);
                    System.IO.File.Create(Path.Combine(runPath, WebConfigurationManager.AppSettings[PROCESSING_DATA_FILE]));

                    tasksPath = Path.Combine(runPath, taskModel.FullName);

                    taskRunner.Run(token, Path.Combine(tasksPath));

                });

                Task t2 = t1.ContinueWith((ante) =>
                {
                }, TaskScheduler.FromCurrentSynchronizationContext());

                t1.Start();
            }
            catch (DevOpsTaskException ex)
            {
                HandleException(ex);
                return PartialView("_TasksFail");
            }
            catch (AggregateException ag)
            {
                HandleException(ag);
                return PartialView("_TasksFail");
            }
            catch (Exception ex)
            {
                HandleException(ex);
                return PartialView("_TasksFail");
            }
            finally
            {
                var tasks = taskRunner.GetDevOpsTaskWithState();
                //could serialize the state back to the tasks file for verification
                //taskRunner.S.SerializeTasksToFile(tasks, ConfigurationManager.AppSettings["tasksPath"]);
            }

            var model = new TaskInformationModel()
            {
                TaskName = taskModel.Task,
                TasksPath = taskModel.Path,
                TasksStartTime = startTime.ToString(),
            };

            return PartialView("_TasksRunning", model);
        }

        public ActionResult TasksComplete()
        {
            return PartialView("_TasksComplete");
        }

        public ActionResult TasksSummary()
        {
            var runPath = GetRunPath();
            //var taskName = GetTaskName();
            if (!string.IsNullOrEmpty(runPath))
            {

                StringBuilder summaryBuilder = new StringBuilder();

                var logTxt = FileClient.ReadAllText(Path.Combine(runPath, WebConfigurationManager.AppSettings[FILE_WONKA_LOG]));
                if (!string.IsNullOrEmpty(logTxt))
                {
                    summaryBuilder.AppendFormat("Wonka Log {0} {1}", Environment.NewLine, logTxt);
                }


                var processingTxt = FileClient.ReadAllText(Path.Combine(runPath, WebConfigurationManager.AppSettings[PROCESSING_DATA_FILE]));
                if (!string.IsNullOrEmpty(processingTxt))
                {
                    summaryBuilder.AppendFormat("Processing Log {0} {1}", Environment.NewLine, processingTxt);
                }

                var errorTxt = FileClient.ReadAllText(Path.Combine(runPath, WebConfigurationManager.AppSettings[ERROR_DATA_FILE]));
                if (!string.IsNullOrEmpty(processingTxt))
                {
                    summaryBuilder.AppendFormat("Error Log {0} {1}", Environment.NewLine, errorTxt);
                }


                var model = new TaskSummaryModel()
                {
                    BuildLabel = "build x.x.x.",
                    BuildTime = "2 minutes",
                    BuildLog = summaryBuilder.ToString(),
                };

                return PartialView("_TasksSummary", model);
            }

            return PartialView("_TasksSummary");
        }

        public ActionResult TasksFail()
        {
            Session.Clear();
            return PartialView("_TasksFail");
        }

        /// <summary>
        /// Retrieves the status of the task run
        /// </summary>
        /// <returns></returns>
        public JsonResult GetStatus()
        {
            var d_status = GetTaskStatus();
            var d_detailsMarkup = GetDetails(d_status);

            dynamic data = new
            {
                status = d_status.ToString(),
            };

            return Json(data);
        }

        #endregion
        

        private string GetDetails(DevOpsTaskStatus d_status) 
        {
            if (d_status == DevOpsTaskStatus.Completed)
            {

                string runPath = GetRunPath();

                if (!string.IsNullOrEmpty(runPath))
                {
                    return string.Format("<a href='{0}'>Build Packages</a>", Path.Combine(runPath, WebConfigurationManager.AppSettings[BUILD_LOG_DATA_FILE]));
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Retrieves the task status
        /// </summary>
        /// <returns></returns>
        private DevOpsTaskStatus GetTaskStatus()
        {
            string runPath = GetRunPath();

            if (string.IsNullOrEmpty(runPath))
                return DevOpsTaskStatus.Idle;


            if (System.IO.File.Exists(Path.Combine(runPath, WebConfigurationManager.AppSettings[COMPLETED_DATA_FILE])))
            {
                return DevOpsTaskStatus.Completed;

            }
            else if (System.IO.File.Exists(Path.Combine(runPath, WebConfigurationManager.AppSettings[ERROR_DATA_FILE])))
            {
                return DevOpsTaskStatus.Faulted;
            }

            if (System.IO.File.Exists(Path.Combine(runPath, WebConfigurationManager.AppSettings[PROCESSING_DATA_FILE])))
            {
                return DevOpsTaskStatus.Running;
            }

            return DevOpsTaskStatus.Idle;
        }

        /// <summary>
        /// Retrieves the run path
        /// </summary>
        /// <returns></returns>
        private string GetRunPath()
        {
            string runPath = string.Empty;

            if (Session[WebConfigurationManager.AppSettings[SESSION_KEY_RUN_PATH]] != null)
            {
                runPath = Session[WebConfigurationManager.AppSettings[SESSION_KEY_RUN_PATH]].ToString();
            }

            return runPath;
        }

        private string GetTaskName()
        {
            const string TASK_NAME = "taskname";
            string taskName = string.Empty;

            if (Session[TASK_NAME] != null)
            {
                taskName = Session[TASK_NAME].ToString();
            }

            return taskName;
        }

        /// <summary>
        /// Adds a message for broadcast
        /// </summary>
        /// <param name="msg"></param>
        private void AddMessage(string msg)
        {
            if (string.IsNullOrEmpty(msg))
                return;

            Broadcaster.Instance.AddMessage(new MessageModel
            {
                Message = msg,
                Id = Guid.NewGuid().ToString(),
            });

        }

       
        
        

        private static StringBuilder SplitLinesWithHtmlBR(string definition)
        {
            string[] definitionLines = definition.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);
            StringBuilder definitionBuilder = new StringBuilder();
            foreach (var line in definitionLines)
            {
                definitionBuilder.AppendFormat("{0}{1}", line, "<br>");
            }

            return definitionBuilder;
        }
        

        private string InitialiseBuildRun(string sourcePath, string project)
        {
            var tasksDestinationPath = Path.Combine(ConfigurationManager.AppSettings["tasksRunnerRootPath"], project);

            //root path for the source task artifacts
            var tasksSourcePath = sourcePath;

            //create new directory for tasks to run
            if (!Directory.Exists(tasksDestinationPath))
            {
                Directory.CreateDirectory(tasksDestinationPath);
            }

            int latestCount = GetNextBuildNumber(tasksDestinationPath);

            var runPath = Path.Combine(tasksDestinationPath, string.Format("{0}", latestCount));
            Directory.CreateDirectory(runPath);

            //generate basic log to identify task run
            string path = Path.Combine(runPath, string.Format("{0}", WebConfigurationManager.AppSettings[BUILD_LOG_DATA_FILE]));
            // This text is added only once to the file.
            if (!System.IO.File.Exists(path))
            {
                // Create a file to write to.
                using (StreamWriter sw = System.IO.File.CreateText(path))
                {
                    sw.WriteLine(string.Format("{0} Ver: {1}", "Build Runner version", "2.1"));
                    sw.WriteLine(string.Format("{0} {1}", DateTime.UtcNow, runPath));
                }
            }

            //copy all build version specific definition files to latest build running path
            DirectoryClient.DirectoryCopy(tasksSourcePath, runPath, true);
            
            FileClient.CreateZipFromDirectory(tasksSourcePath, Path.Combine(runPath, WebConfigurationManager.AppSettings["TASK_DEFINITIONS_ARCHIVE"]));


            return runPath;
        }

        /// <summary>
        /// Gets the next build number
        /// </summary>
        /// <param name="rootPath"></param>
        /// <returns></returns>
        private static int GetNextBuildNumber(string rootPath)
        {
            DirectoryInfo info = new DirectoryInfo(rootPath);
            var directories = info.GetDirectories();
            int latestCount = 0;
            foreach (var dir in directories)
            {
                int res;
                if (int.TryParse(dir.Name, out res))
                {
                    if (res > latestCount)
                    {
                        latestCount = res;
                    }
                }

            }
            latestCount++;
            return latestCount;
        }

        /// <summary>
        /// Handles exceptions
        /// </summary>
        /// <param name="e"></param>
        private void HandleException(Exception e)
        {
            if (e is DevOpsTaskException)
            {
                var devOpsEx = e as DevOpsTaskException;
                AddMessage(string.Format("{0} failed with error: {1}", devOpsEx.Task != null ? devOpsEx.Task.Description : string.Empty, devOpsEx.Message));
            }
            else
            {
                AddMessage(string.Format("Failed with error: {0}", e.Message));
            }
        }

        delegate void AddStatusCallback(string text);              


        int Number(string str)
        {
            int result_ignored;
            if (int.TryParse(str, out result_ignored))
                return result_ignored;

            else
                return 0;
        }

        decimal AsDecimalNumber(string str)
        {
            decimal result_ignored;
            if (decimal.TryParse(str, out result_ignored))
                return result_ignored;
            else
                return 0;
        }
    }
}
