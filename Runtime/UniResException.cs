using System;

namespace UniRes
{
    /// <summary>
    /// Base class for every UniRes exception, so callers can catch one type.
    /// </summary>
    public class UniResException : Exception
    {
        public UniResException(string message) : base(message)
        {
        }

        public UniResException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
