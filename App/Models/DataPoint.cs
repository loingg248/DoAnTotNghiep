using System;

namespace SystemMonitor.Models
{
    public class DataPoint
    {
        public DateTime DateTime { get; set; }
        public double Value { get; set; }

        public DataPoint(DateTime dateTime, double value)
        {
            DateTime = dateTime;
            Value = value;
        }
    }
}