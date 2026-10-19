namespace ReeferSystem.Models
{
    /// <summary>
    /// Represents yard_slots table record handled by Insert_Yard_slot, update_Yard_slot, and Delete_yard_solt
    /// </summary>
    public class YardSlotModel
    {
        public long Useq { get; set; }
        public string YardSlotName { get; set; } = string.Empty;
        public string YardSlotType { get; set; } = "STANDARD";
        public int StackRows { get; set; } = 14;
        public int YAxis { get; set; } = 6; // Tiers (Height)
        public int XAxis { get; set; } = 6; // Columns (Width)
        public string XAxisStartFrom { get; set; } = "A";
        public string YAxisStartFrom { get; set; } = "1";
        public string Active { get; set; } = "Y";
        public string UserId { get; set; } = "ADMIN";
        public DateTime? EnteredAt { get; set; }
        public string? EnteredBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public string? ModifiedBy { get; set; }

        public int TotalCapacity => StackRows * YAxis * XAxis;
    }
}
