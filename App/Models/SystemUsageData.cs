using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace App.Models
{
    using Microsoft.ML.Data;

    public class SystemUsageData
    {
        [LoadColumn(1)]
        public float CpuUsage { get; set; }

        [LoadColumn(2)]
        public float GpuUsage { get; set; }

        [LoadColumn(3)]
        public float RamUsage { get; set; }

        [LoadColumn(4)]
        public string Label { get; set; } // Idle, Office, Gaming
    }


    public class SystemUsagePrediction
    {
        public string PredictedLabel { get; set; }
    }
}
