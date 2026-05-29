using System;

namespace Selvagen.Core.Api
{
    public static class ExceptionExtensions
    {
        /// <summary>
        /// Returns the innermost exception, unwrapping AggregateException and
        /// nested InnerExceptions. Use for user-facing error messages, since
        /// Task.GetResult() wraps the real cause one or more levels deep.
        /// </summary>
        public static Exception Unwrap(this Exception ex)
        {
            if (ex == null) return null;
            if (ex is AggregateException agg) return agg.Flatten().InnerException?.Unwrap() ?? agg;
            return ex.InnerException != null ? ex.InnerException.Unwrap() : ex;
        }
    }
}
