using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using VMS.TPS.Common.Model.Types;

namespace RapidPlanModelValidation
{
	public class DoseLevel
	{
		public DoseValue Dose { get; set; }

		public DoseLevel(DoseValue dose)
		{
			Dose = dose;
		}

		public DoseLevel(string dose)
		{
			if (dose == null)
			{
				Dose = DoseValue.Undefined;
				return;
			}

			var ValueReg = @"\d+(\.?)(\d+)?";
			var UnitsReg = @"\s*((cc)|%|(c?Gy))";
			var Valid = $"{ValueReg}{UnitsReg}";

			var valid = new Regex(Valid, RegexOptions.IgnoreCase).Match(dose);
			var value = new Regex(ValueReg, RegexOptions.IgnoreCase).Match(dose);
			var units = new Regex(UnitsReg, RegexOptions.IgnoreCase).Match(dose).Groups[1];

			if (!valid.Success)
			{
				MessageBox.Show($"Invalid target dose level formatting\n{dose}\nPlease check XML file and correct formatting", "Incorrect Formatting", MessageBoxButton.OK, MessageBoxImage.Error);
				throw new FormatException($"Invalid target dose level formatting\n{dose}\nPlease check XML file and correct formatting");
			}

			var doseValue = double.Parse(value.Value);

			switch (units.Value.ToLower())
			{
				case "cgy":
					Dose = new DoseValue(doseValue, DoseValue.DoseUnit.cGy);
					break;
				case "gy":
					Dose = new DoseValue(doseValue, DoseValue.DoseUnit.Gy);
					break;
				default:
					MessageBox.Show($"Invalid target dose units {units.Value} in {dose}, please check the XML file", "Invalid Target Dose Level", MessageBoxButton.OK, MessageBoxImage.Error);
					throw new ArgumentException($"Invalid target dose units {units.Value} in {dose}, please check the XML file");
			}
		}
	}
}
