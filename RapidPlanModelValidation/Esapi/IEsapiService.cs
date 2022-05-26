using OxyPlot;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace RapidPlanModelValidation
{
	// Interface to ESAPI functionality while not exposing ESAPI objects.
	// This allows the interface to be passed to other projects
	// without requiring those objects to depend on ESAPI.
	// Also, this interface allows for mocking in unit testing.
	// Finally, all the methods are asynchronous because the implementation
	// uses a separate thread to run ESAPI methods.
	// This prevents slow ESAPI methods from blocking the GUI thread.
	public interface IEsapiService
	{
		Task LogInAsync();

		Task OpenPatientAsync(string patientId);
		Task ClosePatientAsync();

		Task<ModelPatientData> GetPatientInfoFromXMLAsync(string rpModel, string rapCourseId, string rapPlanId, string clinCourseId, string clinPlanId, Dictionary<string, RapidPlanModelDefinitionStructure> strucMatches, Dictionary<string, DoseLevel> targetDoses);
		Task<ModelPatientData> GetPatientDataAsync(ModelPatientData patientData);
		Task<bool> ValidatePlanExists(string courseId, string planId);
		Task<bool> ValidateStructureExists(string courseId, string planId, string strucId);

		Task BeginPatientModifications();
		Task SaveModifications();

		Task CalculateDVHEstimatesAsync(ModelPatientData patientData, Dictionary<string, RapidPlanModelDefinitionStructure> modelStructures);
		Task OptimizePlanAsync(ModelPatientData patientData);
		Task CalculatePlanDoseAsync(ModelPatientData patientData);
		Task NormalizePlanAsync(ModelPatientData patientData);
		Task<ObservableCollection<MetricResult>> AnalyzePlansAsync(ModelPatientData patientData, List<Metric> metrics);

		Task<PlotModel> CreatePlotModelAsync(ModelPatientData patientData);
		Task AddDvhCurveAsync(PlotModel plot, ModelPatientData patientData, string modelStrucId, Dictionary<string, RapidPlanModelDefinitionStructure> strucMatches);
		Task RemoveDvhCurveAsync(PlotModel plot, string modelStrucId);
	}
}