namespace ReeferSystem.Models
{
    /// <summary>
    /// Represents Ctr2 container table record returned by Pkg_Front_End.Get_All_Containers
    /// </summary>
    public class ContainerModel
    {
        public string CtrNbr { get; set; } = string.Empty;
        public string CtrSize { get; set; } = string.Empty;
        public string CtrType { get; set; } = string.Empty;
        public string CtrHeight { get; set; } = string.Empty;
        public decimal? TareWeight { get; set; }
        public string TareWeightUnit { get; set; } = "KG";
        public decimal? SafeWeight { get; set; }
        public string SafeWeightUnit { get; set; } = "KG";
        public string CurrentLocationType { get; set; } = "YARD";
        public string CurrentPosition { get; set; } = string.Empty;

        // Formatted display helpers
        public string FormattedTareWeight => TareWeight.HasValue ? $"{TareWeight.Value:N0} {TareWeightUnit}" : $"-- {TareWeightUnit}";
        public string FormattedSafeWeight => SafeWeight.HasValue ? $"{SafeWeight.Value:N0} {SafeWeightUnit}" : $"-- {SafeWeightUnit}";
        public string FormattedHeight => CtrHeight == "96" ? "9'6\" High Cube" : (CtrHeight == "86" ? "8'6\" Standard" : $"{CtrHeight}");
    }
}
