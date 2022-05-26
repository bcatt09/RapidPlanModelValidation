namespace RapidPlanModelValidation
{
    public class MetricResult
    {
        public string Structure { get; set; }
		public string Metric { get; set; }
		public double ClinPlanResult { get; set; }
		public double RapPlanResult { get; set; }
		public double Difference { get; set; }
		public string ClinPlanStructureId { get; set; }
		public string RapPlanStructureId { get; set; }
	}
}