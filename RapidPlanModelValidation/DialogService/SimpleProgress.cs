using System;

namespace RapidPlanModelValidation
{
    public class SimpleProgress : ISimpleProgress
	{
		private readonly Action _onIncrement;
		private readonly Action<string> _onIncrementWithMessage;

		public SimpleProgress(Action onIncrement)
		{
			_onIncrement = onIncrement;
		}

		public SimpleProgress(Action<string> onIncrement)
		{
			_onIncrementWithMessage = onIncrement;
		}

		public void Increment()
		{
			_onIncrement();
		}

		public void Increment(string message)
		{
			_onIncrementWithMessage(message);
		}
	}
}
