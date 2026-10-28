using System;

namespace UniRes
{
    /// <summary>
    /// Thrown when a UniRes file or text cannot be parsed or does not match the resource
    /// classes it describes. <see cref="Line"/> and <see cref="Column"/> are 1-based.
    /// When the text came from a file, <see cref="Path"/> names it and the message starts with it.
    /// </summary>
    public class ResourceFormatException : UniResException
    {
        public ResourceFormatException(string message)
            : base(message)
        {
        }

        public ResourceFormatException(string message, int line, int column)
            : base($"line {line}, column {column}: {message}")
        {
            Line = line;
            Column = column;
        }

        public int Line { get; }

        public int Column { get; }

        /// <summary>Path of the file that failed to read, or null when reading from a string.</summary>
        public string Path { get; internal set; }

        public override string Message => string.IsNullOrEmpty(Path) ? base.Message : Path + ": " + base.Message;
    }
}
