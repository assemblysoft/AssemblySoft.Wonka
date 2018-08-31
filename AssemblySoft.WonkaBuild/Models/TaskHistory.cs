using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace AssemblySoft.WonkaBuild.Models
{
    public class TaskHistory
    {
        public TaskModel Task { get; set; }
        public TaskSummaryModel Summary { get; set; }

        public string Status { get; set; }

    }
}
