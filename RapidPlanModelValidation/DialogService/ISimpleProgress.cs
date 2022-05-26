namespace RapidPlanModelValidation
{
    public interface ISimpleProgress
	{
		void Increment();
		void Increment(string message);
	}
}
