namespace SystemMonitor.Models
{
    public class PowerPlan
    {
        public string Guid { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; }

        public override string ToString()
        {
            return IsActive ? $"{Name} (Active)" : Name;
        }
    }
}