namespace ReeferSystem.Models
{
    /// <summary>
    /// Represents Cv2 container visit details record returned by Pkg_Front_End.Get_Cv_Details
    /// </summary>
    public class CvDetailsModel
    {
        public string CtrNbr { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string CtrStatus { get; set; } = string.Empty;
        public DateTime? YardInTime { get; set; }
        public DateTime? YardOutTime { get; set; }
        public string VesselCode { get; set; } = string.Empty;
        public string Voyage { get; set; } = string.Empty;
        public string CtrSize { get; set; } = string.Empty;
        public string CtrType { get; set; } = string.Empty;
        public string CtrHeight { get; set; } = string.Empty;
        public decimal? GrossWeight { get; set; }
        public string GrossWeightUnit { get; set; } = "KG";

        // Formatted display helpers
        public string FormattedGrossWeight => GrossWeight.HasValue ? $"{GrossWeight.Value:N0} {GrossWeightUnit}" : $"-- {GrossWeightUnit}";
        public string FormattedInTime => YardInTime.HasValue ? YardInTime.Value.ToString("yyyy-MM-dd HH:mm:ss") : "--";
        public string FormattedOutTime => YardOutTime.HasValue ? YardOutTime.Value.ToString("yyyy-MM-dd HH:mm:ss") : "--";
    }
}
