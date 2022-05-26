using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace RapidPlanModelValidation
{
    // This class works directly with ESAPI objects, but it will be wrapped by EsapiService,
    // which doesn't expose ESAPI objects in order to isolate the app from ESAPI
    public class DoseMetricCalculator
	{
		private const DoseValue.DoseUnit SystemUnits = DoseValue.DoseUnit.cGy;

		public double CalculateDoseAtVolume(ExternalPlanSetup plan, Structure structure, double volume, Unit volUnits, Unit doseUnits)
		{
			try
			{
				var dvhResult = plan.GetDoseAtVolume(structure, volume, volUnits == Unit.Percent ? VolumePresentation.Relative : VolumePresentation.AbsoluteCm3, doseUnits == Unit.Percent ? DoseValuePresentation.Relative : DoseValuePresentation.Absolute);

				if ((dvhResult.Unit == DoseValue.DoseUnit.Gy && doseUnits == Unit.Gy) || (dvhResult.Unit == DoseValue.DoseUnit.cGy && doseUnits == Unit.cGy))
					return dvhResult.Dose;
				else if (dvhResult.Unit == DoseValue.DoseUnit.cGy && doseUnits == Unit.Gy)
					return dvhResult.Dose / 100;
				else if (dvhResult.Unit == DoseValue.DoseUnit.Gy && doseUnits == Unit.cGy)
					return dvhResult.Dose * 100;
				else if (dvhResult.Unit == DoseValue.DoseUnit.Percent)
					return dvhResult.Dose;
				else
				{
					MessageBox.Show($"Invalid dose units {doseUnits}", "Error Calculating Metric", MessageBoxButton.OK, MessageBoxImage.Error);
					throw new ArgumentException("Unable to calculate dose at volume (Invalid dose units)");
				}

			}
			catch (Exception e)
			{
				MessageBox.Show($"Unable to calculate dose at volume\n\n{e.Message}\n\n{e.StackTrace}", "Error Calculating Metric", MessageBoxButton.OK, MessageBoxImage.Error);
				throw new ApplicationException("Unable to calculate dose at volume", e);
			}
		}

		public double CalculateVolumeAtDose(ExternalPlanSetup plan, Structure structure, double dose, Unit doseUnits, Unit volUnits)
		{
			try
			{
				var doseUnit = DoseValue.DoseUnit.Unknown;

				switch (doseUnits)
				{
					case Unit.cGy:
						doseUnit = DoseValue.DoseUnit.cGy;
						break;
					case Unit.Gy:
						doseUnit = DoseValue.DoseUnit.Gy;
						break;
					case Unit.Percent:
						doseUnit = DoseValue.DoseUnit.Percent;
						break;
					default:
						doseUnit = DoseValue.DoseUnit.Unknown;
						break;
				}

				var doseValue = ConvertToSystemUnits(new DoseValue(dose, doseUnit));
				var dvhResult = plan.GetVolumeAtDose(structure, doseValue, volUnits == Unit.Percent ? VolumePresentation.Relative : VolumePresentation.AbsoluteCm3);

				return dvhResult;
			}
			catch (Exception e)
			{
				MessageBox.Show($"Unable to calculate volume at dose\n\n{e.Message}\n\n{e.StackTrace}", "Error Calculating Metric", MessageBoxButton.OK, MessageBoxImage.Error);
				throw new ApplicationException("Unable to calculate volume at dose", e);
			}
		}

		public double CalculateMeanDose(ExternalPlanSetup plan, Structure structure, Unit units)
        {
            try
            {
				DVHData dvhResult;
				if(units == Unit.Percent)
					dvhResult = plan.GetDVHCumulativeData(structure, DoseValuePresentation.Relative, VolumePresentation.AbsoluteCm3, 0.01);
				else
					dvhResult = plan.GetDVHCumulativeData(structure, DoseValuePresentation.Absolute, VolumePresentation.AbsoluteCm3, 0.01);

				return ConvertUnits(dvhResult.MeanDose, units).Dose;
            }
            catch (Exception e)
            {
				// There are many reasons the DVH calculation could fail,
				// so wrap any exception in a general exception
				MessageBox.Show($"Failed to calculate mean dose for structure {structure.Id} in plan {plan.Id}\n\n{e.Message}\n\n{e.StackTrace}", "Error Calculating Mean Dose", MessageBoxButton.OK, MessageBoxImage.Error);
                throw new InvalidOperationException("Unable to calculate the mean dose", e);
            }
		}

		public double CalculateMaxDose(ExternalPlanSetup plan, Structure structure, Unit units)
		{
			try
			{
				DVHData dvhResult;
				if (units == Unit.Percent)
					dvhResult = plan.GetDVHCumulativeData(structure, DoseValuePresentation.Relative, VolumePresentation.AbsoluteCm3, 0.01);
				else
					dvhResult = plan.GetDVHCumulativeData(structure, DoseValuePresentation.Absolute, VolumePresentation.AbsoluteCm3, 0.01);

				return ConvertUnits(dvhResult.MaxDose, units).Dose;
			}
			catch (Exception e)
			{
				// There are many reasons the DVH calculation could fail,
				// so wrap any exception in a general exception
				MessageBox.Show($"Failed to calculate maximum dose for structure {structure.Id} in plan {plan.Id}\n\n{e.Message}\n\n{e.StackTrace}", "Error Calculating Max Dose", MessageBoxButton.OK, MessageBoxImage.Error);
				throw new InvalidOperationException("Unable to calculate the maximum dose", e);
			}
		}

		public double CalculateMinDose(ExternalPlanSetup plan, Structure structure, Unit units)
		{
			try
			{
				DVHData dvhResult;
				if (units == Unit.Percent)
					dvhResult = plan.GetDVHCumulativeData(structure, DoseValuePresentation.Relative, VolumePresentation.AbsoluteCm3, 0.01);
				else
					dvhResult = plan.GetDVHCumulativeData(structure, DoseValuePresentation.Absolute, VolumePresentation.AbsoluteCm3, 0.01);

				return ConvertUnits(dvhResult.MinDose, units).Dose;
			}
			catch (Exception e)
			{
				// There are many reasons the DVH calculation could fail,
				// so wrap any exception in a general exception
				MessageBox.Show($"Failed to calculate minimum dose for structure {structure.Id} in plan {plan.Id}\n\n{e.Message}\n\n{e.StackTrace}", "Error Calculating Min Dose", MessageBoxButton.OK, MessageBoxImage.Error);
				throw new InvalidOperationException("Unable to calculate the minimum dose", e);
			}
		}

		private DoseValue ConvertToSystemUnits(DoseValue dose)
		{
			if (dose.Unit == SystemUnits || dose.Unit == DoseValue.DoseUnit.Percent)
				return dose;
			else if (dose.Unit == DoseValue.DoseUnit.Gy && SystemUnits == DoseValue.DoseUnit.cGy)
				return new DoseValue(dose.Dose * 100, DoseValue.DoseUnit.cGy);
			else if (dose.Unit == DoseValue.DoseUnit.cGy && SystemUnits == DoseValue.DoseUnit.Gy)
				return new DoseValue(dose.Dose / 100, DoseValue.DoseUnit.Gy);
			else
			{
				MessageBox.Show($"Invalid dose units {dose.Unit}, could not convert to system dose units {SystemUnits}", "Invalid Dose Units", MessageBoxButton.OK, MessageBoxImage.Error);
				throw new ArgumentException($"Invalid dose units {dose.Unit}, could not convert to system dose units {SystemUnits}");
			}
		}

		private DoseValue ConvertUnits(DoseValue dose, Unit unit)
		{
			if ((dose.Unit == DoseValue.DoseUnit.cGy && unit == Unit.cGy) || (dose.Unit == DoseValue.DoseUnit.Gy && unit == Unit.Gy) || (dose.Unit == DoseValue.DoseUnit.Percent && unit == Unit.Percent))
				return dose;
			else if (dose.Unit == DoseValue.DoseUnit.cGy && unit == Unit.Gy)
				return new DoseValue(dose.Dose / 100, DoseValue.DoseUnit.Gy);
			else if (dose.Unit == DoseValue.DoseUnit.Gy && unit == Unit.cGy)
				return new DoseValue(dose.Dose * 100, DoseValue.DoseUnit.cGy);
			else
			{
				MessageBox.Show($"Could not convert dose units from {dose.Unit} to {unit}", "Error Converting Dose Units", MessageBoxButton.OK, MessageBoxImage.Error);
				throw new ArgumentException($"Could not convert dose units from {dose.Unit} to {unit}");
			}
		}
    }
}
