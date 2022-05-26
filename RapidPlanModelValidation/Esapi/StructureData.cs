using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace RapidPlanModelValidation
{
	public class StructureData
	{
		public string CorrespondingModelStructureID { get; set; }
		public DVHData DVHData { get; set; }
		public Color Color { get; set; }
		public IEnumerable<DVHPoint[]> DVHEstimateCurveData { get; set; }
	}
}
