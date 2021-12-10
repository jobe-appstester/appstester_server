using System;

namespace AppsTester.Shared.SubmissionChecker
{
    public delegate bool Validator<in TParameter>(TParameter parameter);

    public interface ISubmissionPlainParametersProvider
    {
        bool IsValid<TParameter>(string name, Validator<TParameter> validator = null);

        TParameter GetParameter<TParameter>(string name);
    }

    internal class SubmissionPlainParametersProvider : ISubmissionPlainParametersProvider
    {
        private readonly ISubmissionProcessingContextAccessor _processingContextAccessor;

        public SubmissionPlainParametersProvider(ISubmissionProcessingContextAccessor processingContextAccessor)
        {
            _processingContextAccessor = processingContextAccessor;
        }

        public bool IsValid<TParameter>(string name, Validator<TParameter> validator = null)
        {
            var plainParameter = _processingContextAccessor.ProcessingContext.Event.PlainParameters[name];
            if (plainParameter is TParameter parameter)
            {
                return validator?.Invoke(parameter) ?? true;
            }

            return false;
        }

        public TParameter GetParameter<TParameter>(string name)
        {
            if (!_processingContextAccessor.ProcessingContext.Event.PlainParameters.ContainsKey(name))
                throw new ArgumentException($"Can't find plain parameter with name \"{name}\"");

            var plainParameter = _processingContextAccessor.ProcessingContext.Event.PlainParameters[name];
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