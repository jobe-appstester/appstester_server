using System;

namespace AppsTester.Shared.SubmissionChecker
{
    public interface ISubmissionPlainParametersProvider
    {
        TParameter GetParameter<TParameter>(string name);
    }
    
    internal class SubmissionPlainParametersProvider : SubmissionProcessor, ISubmissionPlainParametersProvider
    {
        public TParameter GetParameter<TParameter>(string name)
        {
            if (!SubmissionCheckRequestEvent.PlainParameters.ContainsKey(name))
                throw new ArgumentException($"Can't find plain parameter with name \"{name}\"");

            var plainParameter = SubmissionCheckRequestEvent.PlainParameters[name];
            if (plainParameter is not TParameter parameter)
            {
                throw new ArgumentException(
                    $"Requested parameter with name \"{name}\" and type {typeof(TParameter)}. " +
                    $"But {plainParameter.GetType()} found.");
            }

            return parameter;
        }
    }
}