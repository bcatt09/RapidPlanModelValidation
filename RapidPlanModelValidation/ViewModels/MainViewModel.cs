using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Xml.Linq;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using OxyPlot;

namespace RapidPlanModelValidation
{
	public class MainViewModel : ViewModelBase
	{
		private static readonly string exeDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

		// Validation Patients XML file
		private static readonly string _xmlFileLocation = $@"{exeDirectory}\RapidPlanValidationPatients.xml";
		private static readonly XElement _root = XElement.Load(_xmlFileLocation);

		private readonly IEsapiService _esapiService;
		private readonly IDialogService _dialogService;
		//Don't even think about making a DVH Service, you can't have two service login to Aria at the same time, just deal with the big EsapiService class

		private static readonly string errorLogFile = $@"{exeDirectory}\RapidPlanModelValidationLog.txt";

		public MainViewModel(IEsapiService esapiService, IDialogService dialogService)
		{
			_esapiService = esapiService;
			_dialogService = dialogService;

			WarningLogButtonVisibility = Visibility.Hidden;
		}

		private string _searchText;
		public string SearchText
		{
			get => _searchText;
			set => Set(ref _searchText, value);
		}

		private IEnumerable<string> _rapidPlanModels;
		public IEnumerable<string> RapidPlanModels
		{
			get => _rapidPlanModels;
			set => Set(ref _rapidPlanModels, value);
		}

		private string _selectedRapidPlanModel;
		public string SelectedRapidPlanModel
		{
			get => _selectedRapidPlanModel;
			set => Set(ref _selectedRapidPlanModel, value);
		}

		private List<ModelPatientData> _patientsInModel;
		public List<ModelPatientData> PatientsInModel
		{
			get => _patientsInModel;
			set => Set(ref _patientsInModel, value);
		}

		/// <summary>
		/// Key is the Structure ID in the plan, Value is the Structure in the Model definition
		/// </summary>
		private Dictionary<string, RapidPlanModelDefinitionStructure> _modelStructureMatchInfo;
		public Dictionary<string, RapidPlanModelDefinitionStructure> ModelStructureMatchInfo
		{
			get => _modelStructureMatchInfo;
			set => Set(ref _modelStructureMatchInfo, value);
		}

		private ModelPatientData _selectedPatientFromModel;
		public ModelPatientData SelectedPatientFromModel
		{
			get => _selectedPatientFromModel;
			set => Set(ref _selectedPatientFromModel, value);
		}

		private List<Metric> _validationMetrics;
		public List<Metric> ValidationMetrics
		{
			get => _validationMetrics;
			set => Set(ref _validationMetrics, value);
		}

		private ObservableCollection<MetricResult> _metricResults;
		public ObservableCollection<MetricResult> MetricResults
		{
			get => _metricResults;
			set => Set(ref _metricResults, value);
		}

		private MetricResult _selectedMetric;
		public MetricResult SelectedMetric
		{
			get => _selectedMetric;
			set => Set(ref _selectedMetric, value);
		}

		private PlotModel _dvhPlot;
		public PlotModel DVHPlot
		{
			get => _dvhPlot;
			set => Set(ref _dvhPlot, value);
		}

		private IEnumerable<DVHStructure> _dvhStructures;
		public IEnumerable<DVHStructure> DVHStructures
		{
			get => _dvhStructures;
			set => Set(ref _dvhStructures, value);
		}

		private Dictionary<string, ModelPatientData> _patientData;
		public Dictionary<string, ModelPatientData> PatientData
		{
			get => _patientData;
			set => Set(ref _patientData, value);
		}

		private Visibility _warningLogButtonVisibility;
		public Visibility WarningLogButtonVisibility
		{
			get => _warningLogButtonVisibility;
			set => Set(ref _warningLogButtonVisibility, value);
		}

		public ICommand StartCommand => new RelayCommand(Start);
		public ICommand OpenRapidPlanModelCommand => new RelayCommand(OpenRapidPlanModel);
		public ICommand ValidatePlansCommand => new RelayCommand(ValidatePlans);
		public ICommand MetricInfoCommand => new RelayCommand(DisplayMetricStructureInfo);
		public ICommand SelectPatientCommand => new RelayCommand(SelectPatient);
		public ICommand ViewWarningLogCommand => new RelayCommand(ViewWarningLog);

		private void Start()
		{
			_dialogService.ShowProgressDialog("Logging in to Eclipse\nPlease wait ...",
				async progress =>  await _esapiService.LogInAsync());
			

			InitializeRapidPlanModels();
		}

		/// <summary>
		/// Load list of RapidPlan Model names from XML file
		/// </summary>
		private void InitializeRapidPlanModels()
		{
			RapidPlanModels = _root.Elements().Where(x => x.Name == "Model").Select(x => x.Attribute("Name").Value);
		}

		/// <summary>
		/// Check that "Course" and "Plan" attributes exist for both a "ClinicalPlan" and a "RapidPlan" and that the plans specified exist
		/// </summary>
		/// <returns>True if valid, false otherwise</returns>
		private async Task<bool> ValidateInputs()
		{
			#region Model Selected
			if (String.IsNullOrEmpty(SelectedRapidPlanModel))
			{
				MessageBox.Show("Please select a RapidPlan model from the dropdown to validate", "No Model Selected", MessageBoxButton.OK, MessageBoxImage.Error);
				return false;
			}
			#endregion

			#region Validate Patient List
			foreach (var pat in _root.Elements().Where(x => x.Attribute("Name").Value == SelectedRapidPlanModel).Elements().Where(x => x.Name == "PatientList").Elements())
			{
				string patId, clinCourseId, clinPlanId, rapCourseId, rapPlanId;
				bool clinValid, rapValid;

				// Patient ID
				patId = (string)pat.Attribute("ID") ?? "";

				if (string.IsNullOrEmpty(patId))
				{
					MessageBox.Show($"No ID found for patient\n\nXML Node:\n{pat}\n\n\nPlease ensure that each patient has an ID attribute in the XML file", "No Patient ID Found", MessageBoxButton.OK, MessageBoxImage.Error);
					return false;
				}

				await _esapiService.ClosePatientAsync();
				await _esapiService.OpenPatientAsync(patId);

				// Plans Valid?
				try
				{
					if (pat.Elements().Where(x => x.Name == "ClinicalPlan").Count() > 1)
						Logger.LogWarning($"Multiple Plans Found", "More than one Clinical Plan found, only the first plan will be used", patId);
					if (pat.Elements().Where(x => x.Name == "RapidPlan").Count() > 1)
						Logger.LogWarning("Multiple Plans Found","More than one RapidPlan Plan found, only the first plan will be used", patId);

					XElement clinPlan = pat.Elements().Where(x => x.Name == "ClinicalPlan").FirstOrDefault();
					XElement rapPlan = pat.Elements().Where(x => x.Name == "RapidPlan").FirstOrDefault();

					clinCourseId = clinPlan.Attribute("Course").Value;
					clinPlanId = clinPlan.Attribute("Plan").Value;
					rapCourseId = rapPlan.Attribute("Course").Value;
					rapPlanId = rapPlan.Attribute("Plan").Value;

					clinValid = await _esapiService.ValidatePlanExists(clinCourseId, clinPlanId);
					rapValid = await _esapiService.ValidatePlanExists(rapCourseId, rapPlanId);

					if (!clinValid)
					{
						MessageBox.Show($"Patient ID: {patId}\nCourse ID: {clinCourseId}\nPlan ID: {clinPlanId}\n\nPlease ensure that all IDs are entered correctly", "Error loading clinical plan", MessageBoxButton.OK, MessageBoxImage.Error);
						return false;
					}
					if (!rapValid)
					{
						MessageBox.Show($"Patient ID: {patId}\nCourse ID: {rapCourseId}\nPlan ID: {rapPlanId}\n\nPlease ensure that all IDs are entered correctly", "Error loading RapidPlan plan", MessageBoxButton.OK, MessageBoxImage.Error);
						return false;
					}

					// Targets Valid?
					// Only needed for RapidPlan Plan
					if (rapPlan.Elements().Where(x => x.Name == "Target").Count() == 0)
					{
						MessageBox.Show($"No target structure found for plan {rapPlanId}.  Please define one in the XML file\n\nXML Node:\n{pat}", "No Target Structure Found", MessageBoxButton.OK, MessageBoxImage.Error);
						return false;
					}

					foreach (var target in rapPlan.Elements().Where(x => x.Name == "Target"))
					{
						var targetID = target.Attribute("ID")?.Value;
						var doseLevel = target.Attribute("DoseLevel")?.Value;

						if (targetID == null)
						{
							MessageBox.Show($"No target structure found for plan {rapPlanId}\n\nXML Node:\n{rapPlan}", "No Target Structure Found", MessageBoxButton.OK, MessageBoxImage.Error);
							return false;
						}
						else
						{
							var targetStrucExists = await _esapiService.ValidateStructureExists(rapCourseId, rapPlanId, targetID);

							if (!targetStrucExists)
							{
								MessageBox.Show($"Patient ID: {patId}\nCourse ID: {rapCourseId}\nPlan ID: {rapPlanId}\nStructure ID: {targetID}\n\nPlease check that target structure ID exists for this plan", "Error Loading Target Structure", MessageBoxButton.OK, MessageBoxImage.Error);
								return false;
							}
						}

						if (doseLevel != null)
						{
							try
							{
								DoseLevel targetDoseLevel = new DoseLevel(doseLevel);
							}
							catch
							{
								// Error messages should've been printed by the constructor
								return false;
							}
						}
					}
				}
				catch
				{
					MessageBox.Show($"Error while checking patient plans\n\nXML Node:\n{pat}\n\n\nPlease ensure that all IDs are entered correctly", "Unspecified error while checking patient plans", MessageBoxButton.OK, MessageBoxImage.Error);
					return false;
				}
			}
			#endregion

			#region Validate Structure Matching
			foreach (var struc in _root.Elements().Where(x => x.Attribute("Name").Value == SelectedRapidPlanModel).Elements().Where(x => x.Name == "StructureMatching").Elements())
			{
				string strucId, strucCode;

				strucId = (string)struc.Attribute("ID") ?? "";
				strucCode = (string)struc.Attribute("Code") ?? "";

				if (strucId == "")
				{
					MessageBox.Show($"No structue ID found in XML Node:\n{struc}", "No Structure ID Found", MessageBoxButton.OK, MessageBoxImage.Error);
					return false;
				}

				if (strucCode == "")
				{
					MessageBox.Show($"No structue code found in XML Node:\n{struc}", "No Structure Code Found", MessageBoxButton.OK, MessageBoxImage.Error);
					return false;
				}
			}
			#endregion

			#region Validate Metrics
			foreach (var metric in _root.Elements().Where(x => x.Attribute("Name").Value == SelectedRapidPlanModel).Elements().Where(x => x.Name == "Metrics").Elements())
			{
				string struc, constraint;

				struc = (string)metric.Attribute("Structure") ?? "";
				constraint = (string)metric.Attribute("Constraint") ?? "";

				if (struc == "")
				{
					MessageBox.Show($"No structue found in XML Node:\n{metric}", "No Structure Found", MessageBoxButton.OK, MessageBoxImage.Error);
					return false;
				}

				//make sure each structure in the metrics has a matching structure in the model
				if (!_root.Elements().Where(x => x.Attribute("Name").Value == SelectedRapidPlanModel).Elements().Where(x => x.Name == "StructureMatching").Elements().Select(x => x.Attribute("ID").Value).Contains(struc))
				{
					MessageBox.Show($"No matching structure found for {metric.Attribute("Structure").Value} in the model {SelectedRapidPlanModel}.  Please make sure that any metrics that you would like to calculate have matching structure IDs and fix the XML file", "No Matching Structure Found", MessageBoxButton.OK, MessageBoxImage.Error);
					return false;
				}

				if (constraint == "")
				{
					MessageBox.Show($"No constraint found in XML Node:\n{metric}", "No Constraint Found", MessageBoxButton.OK, MessageBoxImage.Error);
					return false;
				}
			}
			#endregion

			await _esapiService.ClosePatientAsync();

			return true;
		}

		/// <summary>
		/// Load patient list, structure matching, and analysis metrics from XML file
		/// </summary>
		private async void OpenRapidPlanModel()
		{
			ClearWarningLog();

			var inputsValid = await ValidateInputs();

			if (inputsValid)
			{
				// Load Structure Matching
				#region Load Structure Matching
				try
				{
					ModelStructureMatchInfo = _root.Elements().Where(x => x.Attribute("Name").Value == SelectedRapidPlanModel).Elements().Where(x => x.Name == "StructureMatching").Elements().Select(x => new RapidPlanModelDefinitionStructure
					(
						x.Attribute("ID").Value,
						x.Attribute("Code").Value,
						x.Attribute("IsTarget") != null ? x.Attribute("IsTarget").Value : "False"
					)).ToDictionary(x => x.ModelStructureId);
				}
				catch
				{
					MessageBox.Show($"Error while loading structure matching information from XML file for {SelectedRapidPlanModel}, this may cause structures to not be selected appropriately for DVH estimation\n\nXML Node:\n{_root.Elements().Where(x => x.Attribute("Name").Value == SelectedRapidPlanModel).Elements().Where(x => x.Name == "StructureMatching").FirstOrDefault()}\n\n\nPlease ensure that all structure information is entered correctly", "Unspecified Error During Structure Matching", MessageBoxButton.OK, MessageBoxImage.Error);
				}

				// warn user if no structure matching info was found
				if (ModelStructureMatchInfo.Count < 1)
				{
					Logger.LogError($"Missing Structure Matching Info in XML",  $"No structure matching information was found in XML file for {SelectedRapidPlanModel}, this may cause structures to not be selected appropriately for DVH estimation");
					MessageBox.Show($"No structure matching information was found in XML file for {SelectedRapidPlanModel}, this may cause structures to not be selected appropriately for DVH estimation\n\nXML Node:\n{_root.Elements().Where(x => x.Attribute("Name").Value == SelectedRapidPlanModel).FirstOrDefault()}\n\n\nPlease ensure that all structure information is entered correctly", "Unspecified Error During Structure Matching", MessageBoxButton.OK, MessageBoxImage.Error);
				}
				#endregion

				// Load Patient List
				#region Load Patient List
				//create temp array to store and update patient info
				var tempPatientList = new List<ModelPatientData>();
								
				var patList = _root.Elements().Where(x => x.Attribute("Name").Value == SelectedRapidPlanModel).Elements().Where(x => x.Name == "PatientList").Elements();
				var i = 1;

				_dialogService.ShowProgressDialog($"Loading patient data for {SelectedRapidPlanModel}\nPatient 1/{patList.Count()}\nPlease wait ...", patList.Count(),
					async progress =>
					{
						foreach (var pat in patList)
						{
							if (((string)pat.Attribute("ID") ?? "") != "")
							{
								string patId, clinCourseId, clinPlanId, rapCourseId, rapPlanId;

								var clinPlan = pat.Elements().Where(x => x.Name == "ClinicalPlan").FirstOrDefault();
								var rapPlan = pat.Elements().Where(x => x.Name == "RapidPlan").FirstOrDefault();

								patId = pat.Attribute("ID").Value;
								clinCourseId = clinPlan.Attribute("Course").Value;
								clinPlanId = clinPlan.Attribute("Plan").Value;
								rapCourseId = rapPlan.Attribute("Course").Value;
								rapPlanId = rapPlan.Attribute("Plan").Value;
								var targetDoses = rapPlan.Elements().Where(x => x.Name == "Target").ToDictionary(x => x.Attribute("ID").Value, x => new DoseLevel(x.Attribute("DoseLevel")?.Value));

								await _esapiService.OpenPatientAsync(patId);
								tempPatientList.Add(await _esapiService.GetPatientInfoFromXMLAsync(SelectedRapidPlanModel, rapCourseId, rapPlanId, clinCourseId, clinPlanId, ModelStructureMatchInfo, targetDoses));
								if (i + 1 <= patList.Count())
									progress.Increment($"Loading patient data for {SelectedRapidPlanModel}\nPatient {i+1}/{patList.Count()}\nPlease wait ...");
								await _esapiService.ClosePatientAsync();
							}
							i++;
						}
					});

				//check number of targets defined for each patient
				var targets = 0;
				var diffNumOfTargets = false;
				var targetsList = new Dictionary<string, int>();
				foreach (var pat in tempPatientList)
				{
					if (targets == 0)
						targets = pat.TargetDoses.Count;
					else if (pat.TargetDoses.Count != targets)
						diffNumOfTargets = true;

					targetsList.Add($"{pat.LastName}, {pat.FirstName} ({pat.ID})", pat.TargetDoses.Count);
				}

				if (diffNumOfTargets)
					Logger.LogWarning("Different Numbers of Targets", $"Targets found for each patient: {String.Join(", ", targetsList.Select(x => $"{x.Key} ({x.Value})"))}");


				//store the temp info in the existing array to trigger the binding updates
				PatientsInModel = tempPatientList;
				#endregion

				// Check Number of Targets Defined
				#region Check Number of Targets Defined
				var numTargetsInModel = ModelStructureMatchInfo.Where(x => x.Value.IsTarget).Count();
				foreach(var pat in PatientsInModel)
				{
					if (pat.TargetDoses.Count != numTargetsInModel)
						Logger.LogWarning("Number of Targets Does Not Match Model", $"Patient has {pat.TargetDoses.Count} targets defined while the selected model has {numTargetsInModel} targets defined", $"{pat.LastName}, {pat.FirstName} ({pat.ID})");
				}
				#endregion

				// Load Analysis Metrics
				#region Load Analysis Metrics
				try
				{
					ValidationMetrics = _root.Elements().Where(x => x.Attribute("Name").Value == SelectedRapidPlanModel).Elements().Where(x => x.Name == "Metrics").Elements().Select(x => new Metric
					(
						x.Attribute("Structure").Value,
						x.Attribute("Constraint").Value
					)).ToList();
				}
				catch
				{
					MessageBox.Show($"Error while loading metric information from XML file for {SelectedRapidPlanModel}, this may cause metrics to not be displayed appropriately during validation\n\nXML Node:\n{_root.Elements().Where(x => x.Attribute("Name").Value == SelectedRapidPlanModel).Elements().Where(x => x.Name == "Metrics").FirstOrDefault()}\n\n\nPlease ensure that all metric information is entered correctly", "Unspecified Error During Metric Loading", MessageBoxButton.OK, MessageBoxImage.Error);
				}

				// warn user if no structure matching info was found
				if (ValidationMetrics.Count < 1)
				{
					Logger.LogError($"No Metrics Found in XML", $"No metric information was found in XML file for {SelectedRapidPlanModel}, this may cause metrics to not be displayed appropriately during validation");
					MessageBox.Show($"No metric information was found in XML file for {SelectedRapidPlanModel}, this may cause metrics to not be displayed appropriately during validation\n\nXML Node:\n{_root.Elements().Where(x => x.Attribute("Name").Value == SelectedRapidPlanModel).FirstOrDefault()}\n\n\nPlease ensure that all metric information is entered correctly", "Unspecified Error During Metric Loading", MessageBoxButton.OK, MessageBoxImage.Error);
				}
				#endregion
			}
		}

		private async void ValidatePlans()
		{
			if (!await ValidateInputs())
				return;

			MessageBoxResult reallyActuallyCompute;

			reallyActuallyCompute = MessageBox.Show($"Are you sure you want to begin validating?\nModel: {SelectedRapidPlanModel}\nPatients/Plans: {PatientsInModel.Count}\n\nThis could take several hours or more, who even knows?", "Confirm Validation", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
				
			if (reallyActuallyCompute == MessageBoxResult.Yes)
			{
				_dialogService.ShowProgressDialog("Initializing ...", PatientsInModel.Count() * 5,
					async progress =>
					{
						var i = 1;
						foreach (var pat in PatientsInModel)
						{
							try
							{
								await _esapiService.ClosePatientAsync();
								await _esapiService.OpenPatientAsync(pat.ID);
								await _esapiService.BeginPatientModifications();

								//Open Patient and Gather Data
								progress.Increment($"Patient: {i}/{PatientsInModel.Count()}\nOpening Patient ...");
								var tempPat = await _esapiService.GetPatientDataAsync(pat);

								//Estimate DVH using RapidPlan model
								progress.Increment($"Patient: {i}/{PatientsInModel.Count()}\nEstimating DVHs ...");
								await _esapiService.CalculateDVHEstimatesAsync(tempPat, ModelStructureMatchInfo);

								//Optimize plan
								progress.Increment($"Patient: {i}/{PatientsInModel.Count()}\nOptimizing ...");
								await _esapiService.OptimizePlanAsync(tempPat);

								//Calculate dose
								progress.Increment($"Patient: {i}/{PatientsInModel.Count()}\nCalculating Dose ...");
								await _esapiService.CalculatePlanDoseAsync(tempPat);

								//Renormalize
								progress.Increment($"Patient: {i}/{PatientsInModel.Count()}\nNormalizing ...");
								await _esapiService.NormalizePlanAsync(tempPat);

								await _esapiService.SaveModifications();
							}
							catch (Exception e)
							{
								Logger.LogError($"Failed to Execute", $"{e.Message}\n{e.InnerException}", $"{pat.LastName}, {pat.FirstName} ({pat.ID})");
							}

							i++;
						}

						SelectedPatientFromModel = PatientsInModel.First();

						SelectPatient();

						WarningLogButtonVisibility = Visibility.Visible;
						if (Logger.GetLog() != "")
						{
							MessageBox.Show("Validation completed with warnings, please review", "Validation Complete", MessageBoxButton.OK, MessageBoxImage.Warning);
							File.WriteAllText(errorLogFile, $"Validation completed with warnings{Environment.NewLine}Structure matching warnings are most likely fine{Environment.NewLine}{Environment.NewLine}{Logger.GetLog()}");
							System.Diagnostics.Process.Start(errorLogFile);
						}
					});
			}
			else if (reallyActuallyCompute == MessageBoxResult.No)
				MessageBox.Show("Chicken.");
		}

		private void DisplayMetricStructureInfo()
		{
			if (SelectedMetric == null)
				return;

			MessageBox.Show($"{SelectedMetric.Structure}\n{SelectedMetric.Metric}\nStructure from Clinical Plan: {SelectedMetric.ClinPlanStructureId}\nStructure from RapidPlan Plan: {SelectedMetric.RapPlanStructureId}", "Structures Used For Metric Calculation", MessageBoxButton.OK, MessageBoxImage.Information);
		}

		public async void AddDVHCurve(string rapModelStrucId)
		{
			await _esapiService.AddDvhCurveAsync(DVHPlot, SelectedPatientFromModel, rapModelStrucId, ModelStructureMatchInfo);
		}

		public async void RemoveDVHCurve(string rapModelStrucId)
		{
			await _esapiService.RemoveDvhCurveAsync(DVHPlot, rapModelStrucId);
		}

		private async void SelectPatient()
		{
			if (SelectedPatientFromModel?.ID == null)
				return;

			ResetDVH();

			if (MetricResults != null)
				MetricResults.Clear();

			await _esapiService.ClosePatientAsync();
			await _esapiService.OpenPatientAsync(SelectedPatientFromModel.ID);

			_dialogService.ShowProgressDialog("Analyzing Plan ...",
				async progress =>
				{
					try
					{
						SelectedPatientFromModel = await _esapiService.GetPatientDataAsync(SelectedPatientFromModel);
						MetricResults = await _esapiService.AnalyzePlansAsync(SelectedPatientFromModel, ValidationMetrics);
						DVHPlot = await _esapiService.CreatePlotModelAsync(SelectedPatientFromModel);
					}
					catch(Exception e)
					{
						MessageBox.Show("Failed to analyze plan", "Error Analyzing Plan", MessageBoxButton.OK, MessageBoxImage.Error);
					}
				});
		}

		private void ResetDVH()
		{
			if (DVHPlot != null)
			{
				DVHPlot.Series.Clear();
				DVHPlot.InvalidatePlot(true);
			}

			if (DVHStructures != null)
				DVHStructures.ToList().ForEach(x => x.OnDVH = false);
			DVHStructures = SelectedPatientFromModel.DVHStructures;
		}

		private void ClearWarningLog()
		{
			Logger.ClearLog();
			WarningLogButtonVisibility = Visibility.Hidden;
		}

		private void ViewWarningLog()
		{
			if (Logger.GetLog() != "")
				System.Diagnostics.Process.Start(errorLogFile);
			else
				MessageBox.Show("No warnings during validation", "Errors/Warnings", MessageBoxButton.OK, MessageBoxImage.Hand);
		}

		public static void DeleteWarningLog()
		{
			File.Delete(errorLogFile);
		}
	}
}