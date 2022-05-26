using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using EsapiEssentials.Standalone;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

[assembly: ESAPIScript(IsWriteable = true)]

namespace RapidPlanModelValidation
{
	public struct RapidPlanModelDefinitionStructure
	{
		public string ModelStructureId { get; private set; }
		public string ModelStructureCode { get; private set; }
		public bool IsTarget { get; private set; }

		public RapidPlanModelDefinitionStructure(string id, string code, string isTarget)
		  : this()
		{
			ModelStructureId = id;
			ModelStructureCode = code;
			IsTarget = (new List<string> { "yes", "check", "checked", "target", "true", "1", "one" }).Contains(isTarget.ToLower());
		}
	}
	
	public enum RapidPlanOrClinical
	{
		RapidPlan, Clinical
	}

	public class EsapiService : EsapiServiceBase, IEsapiService
	{
		private readonly DoseMetricCalculator _metricCalc;

		//private PatientSummarySearch _search;

		public EsapiService()
		{
			_metricCalc = new DoseMetricCalculator();
		}

		// Override the default LogInAsync functionality
		// so that the patients are obtained after logging in
		public override async Task LogInAsync()
		{
			try
			{
				await base.LogInAsync();
			}
			catch (Exception e)
			{
				MessageBox.Show($"{e.Message}\n\n{e.InnerException}\n\n{e.StackTrace}", "Could not login to Eclipse", MessageBoxButton.OK, MessageBoxImage.Error);
			}
			//await InitializeSearchAsync();
		}

		// Use the RunAsync set of methods to run ESAPI-related actions on a separate thread
		public Task<ModelPatientData> GetPatientInfoFromXMLAsync(string rpModel, string rapCourseId, string rapPlanId, string clinCourseId, string clinPlanId, Dictionary<string, RapidPlanModelDefinitionStructure> strucMatches, Dictionary<string, DoseLevel> targetDoses) =>
			RunAsync(patient =>
			{
				var rapPlan = patient?.Courses?.Where(c => c.Id == rapCourseId).FirstOrDefault().ExternalPlanSetups?.Where(p => p.Id == rapPlanId).FirstOrDefault();
				var clinPlan = patient?.Courses?.Where(c => c.Id == clinCourseId).FirstOrDefault().ExternalPlanSetups?.Where(p => p.Id == clinPlanId).FirstOrDefault();

				var rapPlanPlanStructureMatches = GetStructureMatches(rapPlan, strucMatches);
				var clinPlanStructureMatches = GetStructureMatches(clinPlan, strucMatches);

				return new ModelPatientData
				{
					ID = patient.Id,
					LastName = patient.LastName,
					FirstName = patient.FirstName,
					RapidPlanPlanCourseID = rapCourseId,
					RapidPlanPlanID = rapPlanId,
					RapidPlanPlanStructureMatches = rapPlanPlanStructureMatches,
					ClinicalPlanCourseID = clinCourseId,
					ClinicalPlanID = clinPlanId,
					ClinicalPlanStructureMatches = clinPlanStructureMatches,
					DVHStructures = GetModelStructures(rapPlan, clinPlan, rapPlanPlanStructureMatches, clinPlanStructureMatches),
					RapidPlanModel = rpModel,
					TargetDoses = targetDoses
				};
			});

		public Task<ModelPatientData> GetPatientDataAsync(ModelPatientData patientData) =>
			RunAsync(patient =>
			{
				var rapPlan = patient?.Courses?.Where(c => c.Id == patientData.RapidPlanPlanCourseID).FirstOrDefault().ExternalPlanSetups?.Where(p => p.Id == patientData.RapidPlanPlanID).FirstOrDefault();
				var clinPlan = patient?.Courses?.Where(c => c.Id == patientData.ClinicalPlanCourseID).FirstOrDefault().ExternalPlanSetups?.Where(p => p.Id == patientData.ClinicalPlanID).FirstOrDefault();

				//for constructing ModelPatientData
				var rapPlanDVHData = rapPlan.StructureSet.Structures.Where(x => patientData.RapidPlanPlanStructureMatches.ContainsKey(x.Id)).Select(x => new KeyValuePair<string, DVHData>(x.Id, rapPlan.GetDVHCumulativeData(x, DoseValuePresentation.Absolute, VolumePresentation.Relative, 10))).ToDictionary(d => d.Key, d => d.Value);
				var rapPlanStructureColors = rapPlan.StructureSet.Structures.Where(x => patientData.RapidPlanPlanStructureMatches.ContainsKey(x.Id)).Select(x => new KeyValuePair<string, Color>(x.Id, x.Color)).ToDictionary(d => d.Key, d => d.Value);
				var rapPlanStructureDataDictionary = rapPlan.StructureSet.Structures.Where(x => patientData.RapidPlanPlanStructureMatches.ContainsKey(x.Id)).Select(x => new
				{
					StructureId = x.Id,
					CorrespondingModelStructureID = patientData.RapidPlanPlanStructureMatches[x.Id],
					DVHData = rapPlanDVHData[x.Id],
					Color = rapPlanStructureColors[x.Id]
				});
				var rapPlanDVHEstimates = rapPlan.DVHEstimates;

				var clinPlanDVHData = clinPlan.StructureSet.Structures.Where(x => patientData.ClinicalPlanStructureMatches.ContainsKey(x.Id)).Select(x => new KeyValuePair<string, DVHData>(x.Id, clinPlan.GetDVHCumulativeData(x, DoseValuePresentation.Absolute, VolumePresentation.Relative, 10))).ToDictionary(d => d.Key, d => d.Value);
				var clinPlanStructureColors = clinPlan.StructureSet.Structures.Where(x => patientData.ClinicalPlanStructureMatches.ContainsKey(x.Id)).Select(x => new KeyValuePair<string, Color>(x.Id, x.Color)).ToDictionary(d => d.Key, d => d.Value);
				var clinPlanStructureDataDictionary = clinPlan.StructureSet.Structures.Where(x => patientData.ClinicalPlanStructureMatches.ContainsKey(x.Id)).Select(x => new
				{
					StructureId = x.Id,
					CorrespondingModelStructureID = patientData.ClinicalPlanStructureMatches[x.Id],
					DVHData = clinPlanDVHData[x.Id],
					Color = clinPlanStructureColors[x.Id]
				});
				var clinPlanDVHEstimates = clinPlan.DVHEstimates;

				return new ModelPatientData
				{
					ID = patient.Id,
					LastName = patient.LastName,
					FirstName = patient.FirstName,
					RapidPlanPlanCourseID = patientData.RapidPlanPlanCourseID,
					RapidPlanPlanID = patientData.RapidPlanPlanID,
					RapidPlanStructureData = rapPlanStructureDataDictionary.ToDictionary(x => x.StructureId, x => new StructureData
					{
						CorrespondingModelStructureID = x.CorrespondingModelStructureID,
						DVHData = x.DVHData,
						Color = x.Color,
						DVHEstimateCurveData = rapPlanDVHEstimates.Where(y => y.CurveData.Length > 1 && y.StructureId == x.StructureId).Select(y => y.CurveData)
					}),
					RapidPlanPlan = rapPlan,
					RapidPlanPlanStructureMatches = patientData.RapidPlanPlanStructureMatches,
					RapidPlanDVHData = rapPlanDVHData,
					RapidPlanStructureColors = rapPlanStructureColors,

					ClinicalPlanCourseID = patientData.ClinicalPlanCourseID,
					ClinicalPlanID = patientData.ClinicalPlanID,
					ClinicalStructureData = clinPlanStructureDataDictionary.ToDictionary(x => x.StructureId, x => new StructureData
					{
						CorrespondingModelStructureID = x.CorrespondingModelStructureID,
						DVHData = x.DVHData,
						Color = x.Color,
						DVHEstimateCurveData = clinPlanDVHEstimates.Where(y => y.CurveData.Length > 1 && y.StructureId == x.StructureId).Select(y => y.CurveData)
					}),
					ClinicalPlanPlan = clinPlan,
					ClinicalPlanStructureMatches = patientData.ClinicalPlanStructureMatches,
					ClinicalDVHData = clinPlanDVHData,
					ClinicalStructureColors = clinPlanStructureColors,
					DVHStructures = patientData.DVHStructures,
					RapidPlanModel = patientData.RapidPlanModel,
					TargetDoses = ConvertDoseUnits(patientData.TargetDoses, rapPlan)
				};
			});

		public Task<bool> ValidatePlanExists(string courseId, string planId) =>
			RunAsync(patient => patient?.Courses?.Where(c => c.Id == courseId).FirstOrDefault().PlanSetups?.Where(p => p.Id == planId).Count() > 0);

		public Task<bool> ValidateStructureExists(string courseId, string planId, string strucId) =>
			RunAsync(patient => patient?.Courses?.Where(c => c.Id == courseId).FirstOrDefault().PlanSetups?.Where(p => p.Id == planId).FirstOrDefault().StructureSet.Structures.Where(x => x.Id == strucId).Count() > 0);

		public Task BeginPatientModifications() =>
			RunAsync(app => app.BeginModifications());

		public Task CalculateDVHEstimatesAsync(ModelPatientData patientData, Dictionary<string, RapidPlanModelDefinitionStructure> modelStructures) =>
			RunAsync(patient =>
			{
				var plan = patient?.Courses?.Where(c => c.Id == patientData.RapidPlanPlanCourseID).FirstOrDefault().ExternalPlanSetups?.Where(p => p.Id == patientData.RapidPlanPlanID).FirstOrDefault();
				
				var doseLevels = GetTargetDoseLevels(plan, patientData, modelStructures);

				patientData.RapidPlanPlanStructureMatches = PruneNonTargetMatches(patientData, modelStructures, doseLevels);
				
				var estResult = plan.CalculateDVHEstimates(patientData.RapidPlanModel, doseLevels, patientData.RapidPlanPlanStructureMatches);

				if (!estResult.Success)
					MessageBox.Show($"Error calculating DVH estimates for {patient}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			});

		public Task OptimizePlanAsync(ModelPatientData patientData) =>
			RunAsync(patient =>
			{
				var plan = patient?.Courses?.Where(c => c.Id == patientData.RapidPlanPlanCourseID).FirstOrDefault().ExternalPlanSetups?.Where(p => p.Id == patientData.RapidPlanPlanID).FirstOrDefault();
				bool vmat;
				OptimizerResult optResult;

				if (plan.Beams.FirstOrDefault().MLCPlanType == MLCPlanType.VMAT)
					vmat = true;
				else
					vmat = false;

				if (vmat)
					optResult = plan.OptimizeVMAT();
				else
					optResult = plan.Optimize();

				if (!optResult.Success)
					MessageBox.Show($"Error optimizing for {patient}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			});

		public Task CalculatePlanDoseAsync(ModelPatientData patientData) =>
			RunAsync(patient =>
			{
				var plan = patient?.Courses?.Where(c => c.Id == patientData.RapidPlanPlanCourseID).FirstOrDefault().ExternalPlanSetups?.Where(p => p.Id == patientData.RapidPlanPlanID).FirstOrDefault();

				var calcResult = plan.CalculateDose();

				if (!calcResult.Success)
					MessageBox.Show($"Error calculating dose for {patient}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			});

		public Task NormalizePlanAsync(ModelPatientData patientData) =>
			RunAsync(patient =>
			{
				ExternalPlanSetup rapPlan = patient?.Courses?.Where(c => c.Id == patientData.RapidPlanPlanCourseID).FirstOrDefault().ExternalPlanSetups?.Where(p => p.Id == patientData.RapidPlanPlanID).FirstOrDefault();
				var clinPlan = patient?.Courses?.Where(c => c.Id == patientData.ClinicalPlanCourseID).FirstOrDefault().ExternalPlanSetups?.Where(p => p.Id == patientData.ClinicalPlanID).FirstOrDefault();
				
				//divide V98% for each plan to get new norm value
				var rapTarget = rapPlan.StructureSet.Structures.Where(x => x.Id == rapPlan.TargetVolumeID).FirstOrDefault();
				var clinTarget = clinPlan.StructureSet.Structures.Where(x => x.Id == clinPlan.TargetVolumeID).FirstOrDefault();
				rapPlan.PlanNormalizationValue = rapPlan.GetDoseAtVolume(rapTarget, 98, VolumePresentation.Relative, DoseValuePresentation.Relative) / clinPlan.GetDoseAtVolume(clinTarget, 98, VolumePresentation.Relative, DoseValuePresentation.Relative) * 100;
			});

		public Task SaveModifications() =>
			RunAsync(app => app.SaveModifications());

		public Task<ObservableCollection<MetricResult>> AnalyzePlansAsync(ModelPatientData patientData, List<Metric> metrics) =>
			RunAsync(patient =>
			{
				var results = new ObservableCollection<MetricResult>();
				ExternalPlanSetup rapPlan, clinPlan;

				//load plans to analyze
				try
				{
					if (patientData.RapidPlanPlan != null && patientData.ClinicalPlanPlan != null)
					{
						rapPlan = patientData.RapidPlanPlan;
						clinPlan = patientData.ClinicalPlanPlan;
					}
					else
						throw new ArgumentNullException();
				}
				catch
				{
					rapPlan = patient?.Courses?.Where(c => c.Id == patientData.RapidPlanPlanCourseID).FirstOrDefault().ExternalPlanSetups?.Where(p => p.Id == patientData.RapidPlanPlanID).FirstOrDefault();
					clinPlan = patient?.Courses?.Where(c => c.Id == patientData.ClinicalPlanCourseID).FirstOrDefault().ExternalPlanSetups?.Where(p => p.Id == patientData.ClinicalPlanID).FirstOrDefault();
				}

				foreach (var metric in metrics)
				{
					//get the corresponding id for the structure match in the plan's structure set
					var rapPlanStrucId = patientData.RapidPlanPlanStructureMatches.Where(s => s.Value == metric.StructureID).Select(s => s.Key).FirstOrDefault();
					var clinPlanStrucId = patientData.ClinicalPlanStructureMatches.Where(s => s.Value == metric.StructureID).Select(s => s.Key).FirstOrDefault();

					//find the correct structure from the structure set of the plan that we will evaluate the metric on
					var rapPlanStruc = rapPlan.StructureSet.Structures.Where(s => s.Id == rapPlanStrucId).FirstOrDefault();
					var clinPlanStruc = clinPlan.StructureSet.Structures.Where(s => s.Id == clinPlanStrucId).FirstOrDefault();

					// if either structure wasn't found or is empty just move on to the next one because not everything will be contoured for every patient
					if (rapPlanStruc == null || rapPlanStruc.IsEmpty || clinPlanStruc == null || clinPlanStruc.IsEmpty)
						continue;

					//evaluate metric based on what type it is
					if (metric.QueryType == MetricType.Dose)
					{
						var rapResult = _metricCalc.CalculateDoseAtVolume(rapPlan, rapPlanStruc, metric.QueryValue, metric.QueryUnits, metric.ResultUnits);
						var clinResult = _metricCalc.CalculateDoseAtVolume(clinPlan, clinPlanStruc, metric.QueryValue, metric.QueryUnits, metric.ResultUnits);

						results.Add(new MetricResult
						{
							Structure = metric.StructureID,
							Metric = metric.OrigString,
							ClinPlanResult = clinResult,
							RapPlanResult = rapResult,
							Difference = rapResult - clinResult,
							ClinPlanStructureId = clinPlanStruc.Id,
							RapPlanStructureId = rapPlanStruc.Id
						});
					}
					else if (metric.QueryType == MetricType.Volume)
					{
						var rapResult = _metricCalc.CalculateVolumeAtDose(rapPlan, rapPlanStruc, metric.QueryValue, metric.QueryUnits, metric.ResultUnits);
						var clinResult = _metricCalc.CalculateVolumeAtDose(clinPlan, clinPlanStruc, metric.QueryValue, metric.QueryUnits, metric.ResultUnits);

						results.Add(new MetricResult
						{
							Structure = metric.StructureID,
							Metric = metric.OrigString,
							ClinPlanResult = clinResult,
							RapPlanResult = rapResult,
							Difference = rapResult - clinResult,
							ClinPlanStructureId = clinPlanStruc.Id,
							RapPlanStructureId = rapPlanStruc.Id
						});
					}
					else if (metric.QueryType == MetricType.Mean)
					{
						var rapResult = _metricCalc.CalculateMeanDose(rapPlan, rapPlanStruc, metric.ResultUnits);
						var clinResult = _metricCalc.CalculateMeanDose(clinPlan, clinPlanStruc, metric.ResultUnits);

						results.Add(new MetricResult
						{
							Structure = metric.StructureID,
							Metric = metric.OrigString,
							ClinPlanResult = clinResult,
							RapPlanResult = rapResult,
							Difference = rapResult - clinResult,
							ClinPlanStructureId = clinPlanStruc.Id,
							RapPlanStructureId = rapPlanStruc.Id
						});
					}
					else if (metric.QueryType == MetricType.Max)
					{
						var rapResult = _metricCalc.CalculateMaxDose(rapPlan, rapPlanStruc, metric.ResultUnits);
						var clinResult = _metricCalc.CalculateMaxDose(clinPlan, clinPlanStruc, metric.ResultUnits);

						results.Add(new MetricResult
						{
							Structure = metric.StructureID,
							Metric = metric.OrigString,
							ClinPlanResult = clinResult,
							RapPlanResult = rapResult,
							Difference = rapResult - clinResult,
							ClinPlanStructureId = clinPlanStruc.Id,
							RapPlanStructureId = rapPlanStruc.Id
						});
					}
					else if (metric.QueryType == MetricType.Min)
					{
						var rapResult = _metricCalc.CalculateMinDose(rapPlan, rapPlanStruc, metric.ResultUnits);
						var clinResult = _metricCalc.CalculateMinDose(clinPlan, clinPlanStruc, metric.ResultUnits);

						results.Add(new MetricResult
						{
							Structure = metric.StructureID,
							Metric = metric.OrigString,
							ClinPlanResult = clinResult,
							RapPlanResult = rapResult,
							Difference = rapResult - clinResult,
							ClinPlanStructureId = clinPlanStruc.Id,
							RapPlanStructureId = rapPlanStruc.Id
						});
					}
				}

				return results;
			});


		/// <summary>
		/// Match plan structures to the RapidPlan model structures, using structure codes or ID
		/// Key is id from Structure Set in Patient, Value is ID from RapidPlan Model
		/// </summary>
		public static Dictionary<string, string> GetStructureMatches(PlanSetup plan, Dictionary<string, RapidPlanModelDefinitionStructure> modelStructures)
		{
			var result = new Dictionary<string, string>();
			var planStructures = plan.StructureSet.Structures.Where(s => !s.IsEmpty);

			foreach (var pStruc in planStructures.Where(x => !x.IsEmpty && x.DicomType != "SUPPORT" && x.DicomType != "MARKER"))
			{
				var codeMatches = new List<RapidPlanModelDefinitionStructure>();
				var idMatches = new List<RapidPlanModelDefinitionStructure>();

				foreach (var mStruc in modelStructures.Values)
				{
					//first check for a match of the structure codes
					if (pStruc.StructureCodeInfos.Select(s => s.Code).Contains(mStruc.ModelStructureCode))
						codeMatches.Add(mStruc);

					//if no match or multiple matches are found, will use structure ids
					if (pStruc.Id == mStruc.ModelStructureId)
						idMatches.Add(mStruc);
				}

				if (codeMatches.Count > 0)
				{
					result.Add(pStruc.Id, codeMatches.FirstOrDefault().ModelStructureId);
					
					if (codeMatches.Count > 1)
						Logger.LogWarning("Multiple Structure Code Matches Found", $"Multiple code matches found for plan structure {pStruc.Id}, using the first one ({codeMatches.FirstOrDefault().ModelStructureId})");
				}
				else if (idMatches.Count > 0)
					result.Add(pStruc.Id, idMatches.FirstOrDefault().ModelStructureId);
			}

			if(result.Count != modelStructures.Count)
				Logger.LogWarning($"No Matching Structures Found", $"No matching structures found for RapidPlan model structures: {string.Join(", ", modelStructures.Values.Where(x => !result.Values.Contains(x.ModelStructureId)).Select(x => x.ModelStructureId))}", $"{plan.Course.Patient.LastName}, {plan.Course.Patient.FirstName} ({plan.Course.Patient.Id})", $"{plan.Id}");

			return result;
		}

		/// <summary>
		/// Get prescription dose levels for each of the target structures
		/// </summary>
		private static Dictionary<string, DoseValue> GetTargetDoseLevels(PlanSetup plan, ModelPatientData patientData, Dictionary<string, RapidPlanModelDefinitionStructure> modelStructures)
		{
			var result = new Dictionary<string, DoseValue>();

			// For each structure in model listed as a target, find matching patient structure and assign plan target dose if necessary
			foreach (var modelStruc in modelStructures.Where(x => x.Value.IsTarget))
			{
				var patientStruc = patientData.RapidPlanPlanStructureMatches.Where(x => x.Value == modelStruc.Value.ModelStructureId).FirstOrDefault().Key;

				if (patientData.TargetDoses[patientStruc].Dose.IsUndefined())
				{
					result.Add(patientStruc, plan.TotalDose);
					Logger.LogWarning("No Target Dose in XML", $"No target dose found for {patientStruc}, using plan prescription dose instead\nPlease adjust the XML file if this is incorrect", $"{patientData.LastName}, {patientData.FirstName} ({patientData.ID})");
				}
				else
					result.Add(patientStruc, patientData.TargetDoses[patientStruc].Dose);
			}

			return result;
		}
		
		/// <summary>
		/// Remove any structure that are matched to a target in the model without having a dose level specified in the XML file
		/// </summary>
		/// <param name="patientData">Patient Data relating to Model</param>
		/// <param name="modelStructures">Model Structure information</param>
		/// <param name="doseLevels">Target dose levels defined in XML</param>
		/// <returns></returns>
		private static Dictionary<string, string> PruneNonTargetMatches(ModelPatientData patientData, Dictionary<string, RapidPlanModelDefinitionStructure> modelStructures, Dictionary<string, DoseValue> doseLevels)
		{
			var result = new Dictionary<string, string>();

			foreach (var match in patientData.RapidPlanPlanStructureMatches)
			{
				//if the matched structure in the model is listed as a target make sure that specific structure ID has a target dose defined in the XML
				if(modelStructures[match.Value].IsTarget)
				{
					//if the model structure ID has not been explicitly defined in the XML we won't add it as a match and will warn the user
					if(!doseLevels.ContainsKey(match.Key))
					{
						Logger.LogWarning("Invalid Structure Match Found", $"The non-target patient structure {match.Key} was matched to the target model structure {match.Value} and will not be added to the DVH Estimation.  If this was a target structure, make sure it is defined in the XML file", $"{patientData.LastName}, {patientData.FirstName} ({patientData.ID})", patientData.RapidPlanPlanID);
						continue;  //skip adding this one
					}
				}

				//keep the match
				result.Add(match.Key, match.Value);
			}

			return result;
		}

		private static Dictionary<string, DoseLevel> ConvertDoseUnits(Dictionary<string, DoseLevel> doses, ExternalPlanSetup plan)
		{
			var planUnits = plan.DosePerFraction.Unit;
			var newDoses = new Dictionary<string, DoseLevel>();

			foreach (var dose in doses)
			{
				//same
				if (dose.Value.Dose.Unit == planUnits)
					newDoses.Add(dose.Key, dose.Value);

				//going from cGy to Gy
				else if (dose.Value.Dose.Unit == DoseValue.DoseUnit.cGy && planUnits == DoseValue.DoseUnit.Gy)
					newDoses.Add(dose.Key, new DoseLevel(new DoseValue(dose.Value.Dose.Dose / 100, DoseValue.DoseUnit.Gy)));

				//going from Gy to cGy
				else if (dose.Value.Dose.Unit == DoseValue.DoseUnit.Gy && planUnits == DoseValue.DoseUnit.cGy)
					newDoses.Add(dose.Key, new DoseLevel(new DoseValue(dose.Value.Dose.Dose * 100, DoseValue.DoseUnit.cGy)));

				//if no units then it was most likely left blank and we will later assume the Rx dose
				else
					newDoses.Add(dose.Key, dose.Value);
			}

			return newDoses;
		}

		public static IEnumerable<DVHStructure> GetModelStructures(ExternalPlanSetup rapPlan, ExternalPlanSetup clinPlan, Dictionary<string,string> rapPlanStructureMatches, Dictionary<string,string> clinPlanStructureMatches)
		{
			List<DVHStructure> list = new List<DVHStructure>();

			foreach (var modelStrucId in rapPlanStructureMatches.Values.Union(clinPlanStructureMatches.Values))
			{
				//find structure from each structureset for the corresponding structure match in the rapidplan model
				var rapPlanStruc = rapPlan.StructureSet.Structures.Where(x => x.Id == rapPlanStructureMatches.Where(y => y.Value == modelStrucId).Select(y => y.Key).FirstOrDefault()).FirstOrDefault();
				var clinPlanStruc = clinPlan.StructureSet.Structures.Where(x => x.Id == clinPlanStructureMatches.Where(y => y.Value == modelStrucId).Select(y => y.Key).FirstOrDefault()).FirstOrDefault();

				//if either the clinical or rapidplan isn't empty, add it to the list
				if ((!clinPlanStruc.IsEmpty && clinPlanStruc.DicomType != "SUPPORT" && clinPlanStruc.DicomType != "MARKER") || (!rapPlanStruc.IsEmpty && rapPlanStruc.DicomType != "SUPPORT" && rapPlanStruc.DicomType != "MARKER"))
				{
					list.Add(new DVHStructure
					{
						ModelID = modelStrucId,
						OnDVH = false
					});
				}
			}

			//remove duplicates
			return list.Distinct().AsEnumerable();
		}




		public Task<PlotModel> CreatePlotModelAsync(ModelPatientData patientData) =>
			RunAsync(patient =>
			{
				var rapPlan = patient?.Courses?.Where(c => c.Id == patientData.RapidPlanPlanCourseID).FirstOrDefault().ExternalPlanSetups?.Where(p => p.Id == patientData.RapidPlanPlanID).FirstOrDefault();
				var clinPlan = patient?.Courses?.Where(c => c.Id == patientData.ClinicalPlanCourseID).FirstOrDefault().ExternalPlanSetups?.Where(p => p.Id == patientData.ClinicalPlanID).FirstOrDefault();

				var plotModel = new PlotModel();
				AddAxes(plotModel, rapPlan, clinPlan);
				plotModel.IsLegendVisible = true;
				plotModel.Legends.Add(new Legend
					{
						LegendPlacement = LegendPlacement.Outside,
						LegendOrientation = LegendOrientation.Vertical,
						LegendPosition = LegendPosition.BottomCenter,
						LegendMaxHeight = 75
					});

				return plotModel;
			});

		public Task AddDvhCurveAsync(PlotModel plot, ModelPatientData patientData, string modelStrucId, Dictionary<string, RapidPlanModelDefinitionStructure> strucMatches) =>
			RunAsync(patient =>
			{
				try
				{
					var clinPlan = patientData.ClinicalPlanPlan ?? patient?.Courses?.Where(c => c.Id == patientData.ClinicalPlanCourseID).FirstOrDefault().ExternalPlanSetups?.Where(p => p.Id == patientData.ClinicalPlanID).FirstOrDefault();
					var clinPlanStrucId = patientData.ClinicalPlanStructureMatches.Where(x => x.Value == modelStrucId).Select(x => x.Key).FirstOrDefault();

					plot.Series.Add(CreateDvhSeries(patientData.ClinicalStructureData[clinPlanStrucId], RapidPlanOrClinical.Clinical));
				}
				catch (Exception e) { MessageBox.Show($"Failed to add {modelStrucId} curve to DVH\n\n{e.Message}\n\n{e.InnerException}\n\n{e.StackTrace}"); }
				try
				{
					//plans to generate DVHs for
					var rapPlan = patientData.RapidPlanPlan ?? patient?.Courses?.Where(c => c.Id == patientData.RapidPlanPlanCourseID).FirstOrDefault().ExternalPlanSetups?.Where(p => p.Id == patientData.RapidPlanPlanID).FirstOrDefault();

					//find appropriate structure from structure sets which is matched to the RapidPlan model structure IDs which are being displayed
					var rapPlanStrucId = patientData.RapidPlanPlanStructureMatches.Where(x => x.Value == modelStrucId).Select(x => x.Key).FirstOrDefault();

					plot.Series.Add(CreateDvhSeries(patientData.RapidPlanStructureData[rapPlanStrucId], RapidPlanOrClinical.RapidPlan));

					if (patientData.RapidPlanStructureData[rapPlanStrucId].DVHEstimateCurveData.Count() > 1)
						plot.Series.Add(CreateDvhEstimateSeries(patientData.RapidPlanStructureData[rapPlanStrucId], RapidPlanOrClinical.RapidPlan));
				}
				catch (Exception e) { MessageBox.Show($"Failed to add {modelStrucId} curve to DVH\n\n{e.Message}\n\n{e.InnerException}\n\n{e.StackTrace}"); }

				UpdatePlot(plot);
			});

		public Task RemoveDvhCurveAsync(PlotModel plot, string modelStrucId) =>
			RunAsync(() =>
			{
				FindSeries(plot, modelStrucId).ForEach(x => plot.Series.Remove(x));

				UpdatePlot(plot);
			});

		private static void AddAxes(PlotModel plotModel, ExternalPlanSetup rapPlan, ExternalPlanSetup clinPlan)
		{
			var max = Math.Max(clinPlan.Dose.DoseMax3D.Unit == DoseValue.DoseUnit.Percent ? clinPlan.Dose.DoseMax3D.Dose * clinPlan.TotalDose.Dose / 100 : clinPlan.Dose.DoseMax3D.Dose,
								rapPlan.Dose.DoseMax3D.Unit == DoseValue.DoseUnit.Percent ? rapPlan.Dose.DoseMax3D.Dose * rapPlan.TotalDose.Dose / 100 : rapPlan.Dose.DoseMax3D.Dose);

			plotModel.Axes.Add(new LinearAxis
			{
				Title = "Dose [cGy]",
				Position = AxisPosition.Bottom,
				MajorGridlineStyle = LineStyle.Solid,
				MinorGridlineStyle = LineStyle.Dot,
				AbsoluteMinimum = 0,
				AbsoluteMaximum = max
			});

			plotModel.Axes.Add(new LinearAxis
			{
				Title = "Volume [%]",
				Position = AxisPosition.Left,
				MajorGridlineStyle = LineStyle.Solid,
				MinorGridlineStyle = LineStyle.Dot,
				AbsoluteMinimum = 0,
				AbsoluteMaximum = 100
			});
		}

		private OxyPlot.Series.Series CreateDvhSeries(StructureData structureData, RapidPlanOrClinical rporclinical)
		{
			var series = new OxyPlot.Series.LineSeries
			{
				Tag = rporclinical == RapidPlanOrClinical.Clinical ? structureData.CorrespondingModelStructureID + " (Clinical)" : structureData.CorrespondingModelStructureID + " (RapidPlan)",
				Title = rporclinical == RapidPlanOrClinical.Clinical ? structureData.CorrespondingModelStructureID + " (Clinical)" : structureData.CorrespondingModelStructureID + " (RapidPlan)",
				Color = OxyColor.FromRgb(structureData.Color.R, structureData.Color.G, structureData.Color.B),
				LineStyle = rporclinical == RapidPlanOrClinical.Clinical ? LineStyle.Dash : LineStyle.Solid
			};

			var points = structureData.DVHData.CurveData.Select(CreateDataPoint);
			series.Points.AddRange(points);
			series.TrackerFormatString = "{0} " + Environment.NewLine + "{1}: {2:0.000} " + Environment.NewLine + "{3}: {4:0.0} ";

			return series;
		}

		private OxyPlot.Series.Series CreateDvhEstimateSeries(StructureData structureData, RapidPlanOrClinical rporclinical)
		{
			var tag1 = rporclinical == RapidPlanOrClinical.Clinical ? " (Clinical Estimate)" : " (RapidPlan Estimate)";
			var tag = structureData.CorrespondingModelStructureID + tag1;

			var series = new OxyPlot.Series.AreaSeries
			{
				Tag = tag,
				Title = structureData.CorrespondingModelStructureID + " (RapidPlan DVH Estimate)",
				Color = OxyColor.FromRgb(structureData.Color.R, structureData.Color.G, structureData.Color.B),
				LineStyle = rporclinical == RapidPlanOrClinical.Clinical ? LineStyle.Dash : LineStyle.Solid,
				Fill = OxyColor.FromArgb(64, structureData.Color.R, structureData.Color.G, structureData.Color.B),
				StrokeThickness = 0
			};

			var points = structureData.DVHEstimateCurveData.Last().Select(CreateDataPoint);
			series.Points.AddRange(points);
			var points2 = structureData.DVHEstimateCurveData.First().Select(CreateDataPoint);
			series.Points2.AddRange(points2);
			series.TrackerFormatString = "{0} " + Environment.NewLine + "{1}: {2:0.000} " + Environment.NewLine + "{3}: {4:0.0} ";

			return series;
		}

		private DataPoint CreateDataPoint(DVHPoint p)
		{
			return new DataPoint(p.DoseValue.Dose, p.Volume);
		}

		private List<OxyPlot.Series.Series> FindSeries(PlotModel plot, string structureId)//, RapidPlanOrClinical rporclinical)
		{
			var qualifiers = new List<string> { " (Clinical)", " (Clinical Estimate)", " (RapidPlan)", " (RapidPlan Estimate)" };

			return plot.Series.Where(x => qualifiers.Select(y => structureId + y).Contains((string)x.Tag)).ToList();
		}

		private void UpdatePlot(PlotModel plot)
		{
			plot.InvalidatePlot(true);
		}
	}
}