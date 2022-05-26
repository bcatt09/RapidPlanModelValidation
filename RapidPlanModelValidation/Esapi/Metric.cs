using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace RapidPlanModelValidation
{
	public struct Metric
	{
		public string OrigString { get; private set; }
		public string StructureID { get; private set; }
		public MetricType QueryType { get; private set; }
		public double QueryValue { get; private set; }
		public Unit QueryUnits { get; private set; }
		public Unit ResultUnits { get; private set; }

		public Metric(string structure, string metric) : this()
		{
			OrigString = metric;

			var QueryTypeReg = @"^(V|D|Mean|Max|Min)";
			var QueryValueReg = @"\d+(\.?)(\d+)?";
			var QueryUnitsReg = @"((cc)|%|(c?Gy))";
			var ResultUnitsReg = @"\[(cc|%|(c?Gy))\]";
			var Valid = $"(((V|CV|DC|D)({QueryValueReg}{QueryUnitsReg}))|(Mean|Max|Min)){ResultUnitsReg}";

			var needsUnits = new List<MetricType> { MetricType.Dose, MetricType.Volume };

			var valid = new Regex(Valid, RegexOptions.IgnoreCase).Match(metric);
			var queryType = new Regex(QueryTypeReg, RegexOptions.IgnoreCase).Match(metric);
			var queryValue = new Regex(QueryValueReg, RegexOptions.IgnoreCase).Match(metric);
			var queryUnits = new Regex(QueryUnitsReg, RegexOptions.IgnoreCase).Match(metric);
			var resultUnits = new Regex(ResultUnitsReg, RegexOptions.IgnoreCase).Match(metric);

			if (!valid.Success)
			{
				MessageBox.Show($"Invalid dose metric formatting\n{metric}\nPlease check XML file and correct formatting", "Incorrect Formatting", MessageBoxButton.OK, MessageBoxImage.Error);
				throw new FormatException($"Invalid dose metric formatting\n{metric}\nPlease check XML file and correct formatting");
			}

			#region Structure
			StructureID = structure;
			#endregion

			#region QueryType
			switch (queryType.Value.ToLower())
			{
				case "d":
					QueryType = MetricType.Dose;
					break;
				case "v":
					QueryType = MetricType.Volume;
					break;
				case "max":
					QueryType = MetricType.Max;
					break;
				case "min":
					QueryType = MetricType.Min;
					break;
				case "mean":
					QueryType = MetricType.Mean;
					break;
				default:
					MessageBox.Show($"Invalid constraint type {queryType.Value} in constraint {metric} for structue {structure}\n\nThis may cause incorrect analysis of the validation metrics or cause the script to crash, please check the XML file", "Invalid Constraint", MessageBoxButton.OK, MessageBoxImage.Error);
					break;
			}
			#endregion

			#region QueryValue
			if (needsUnits.Contains(QueryType))
				QueryValue = double.Parse(queryValue.Value);
			else
				QueryValue = double.NaN;
			#endregion

			#region QueryUnits
			if (needsUnits.Contains(QueryType))
			{
				switch (queryUnits.Value.ToLower())
				{
					case "%":
						QueryUnits = Unit.Percent;
						break;
					case "cgy":
						QueryUnits = Unit.cGy;
						break;
					case "gy":
						QueryUnits = Unit.Gy;
						break;
					case "cc":
						QueryUnits = Unit.cc;
						break;
					default:
						MessageBox.Show($"Invalid query units {queryUnits.Value} in constraint {metric} for structure {structure}\n\nThis may cause incorrect analysis of the validation metrics or cause the script to crash, please check the XML file", "Invalid Constraint", MessageBoxButton.OK, MessageBoxImage.Error);
						break;
				}
			}
			else
				QueryUnits = Unit.NA;
			#endregion

			#region ResultUnits
			switch (resultUnits.Value.ToLower().ToLower().Replace("[", "").Replace("]", ""))
			{
				case "%":
					ResultUnits = Unit.Percent;
					break;
				case "cgy":
					ResultUnits = Unit.cGy;
					break;
				case "gy":
					ResultUnits = Unit.Gy;
					break;
				case "cc":
					ResultUnits = Unit.cc;
					break;
				default:
					MessageBox.Show($"Invalid resultant units {resultUnits.Value} in constraint {metric} for structure {structure}\n\nThis may cause incorrect analysis of the validation metrics or cause the script to crash, please check the XML file", "Invalid Constraint", MessageBoxButton.OK, MessageBoxImage.Error);
					break;
			}
			#endregion
		}
	}

	public enum MetricType
	{
		Dose, Volume, Min, Max, Mean
	}

	public enum Unit
	{
		Percent, cGy, Gy, cc, NA
	}
}
