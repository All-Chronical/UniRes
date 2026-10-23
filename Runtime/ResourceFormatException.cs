using System;

namespace UniRes
{
    /// <summary>
    /// Thrown when a UniRes file or text cannot be parsed or does not match the resource
    /// classes it describes. <see cref="Line"/> and <see cref="Column"/> are 1-based.
    /// </summary>
    public class ResourceFormatException : UniResException
    {
        public ResourceFormatException(string message, int line, int column)
            : base($"line {line}, column {column}: {message}")
        {
            Line = line;
            Column = column;
        }

        public int Line { get; }

        public int Column { get; }
    }
}
