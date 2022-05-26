using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using VMS.TPS.Common.Model.API;

namespace RapidPlanModelValidation
{
	public class ModelPatientData
	{
		public string ID { get; set; }
		public string LastName { get; set; }
		public string FirstName { get; set; }

		public ExternalPlanSetup RapidPlanPlan { get; set; }
		public string RapidPlanPlanCourseID { get; set; }
		public string RapidPlanPlanID { get; set; }
		public Dictionary<string, StructureData> RapidPlanStructureData { get; set; }
		public Dictionary<string, string> RapidPlanPlanStructureMatches { get; set; }
		public Dictionary<string, DVHData> RapidPlanDVHData { get; set; }
		public Dictionary<string, Color> RapidPlanStructureColors { get; set; }

		public ExternalPlanSetup ClinicalPlanPlan { get; set; }
		public string ClinicalPlanCourseID { get; set; }
		public string ClinicalPlanID { get; set; }
		public Dictionary<string, StructureData> ClinicalStructureData { get; set; }
		public Dictionary<string, string> ClinicalPlanStructureMatches { get; set; }
		public Dictionary<string, DVHData> ClinicalDVHData { get; set; }
		public Dictionary<string, Color> ClinicalStructureColors { get; set; }

		public IEnumerable<DVHStructure> DVHStructures { get; set; }
		public string RapidPlanModel { get; set; }
		public Dictionary<string, DoseLevel> TargetDoses { get; set; }
	}
}
