using System;
using System.Globalization;
using System.Text;

namespace UniRes
{
    /// <summary>
    /// Character cursor over UniRes text. Skips whitespace and <c>;</c> line comments,
    /// reads the small set of tokens the format uses, and reports errors with a position.
    /// </summary>
    internal sealed class TextScanner
    {
        private readonly string text;
        private int position;

        public TextScanner(string text)
        {
            this.text = text ?? throw new ArgumentNullException(nameof(text));
            if (this.text.Length > 0 && this.text[0] == '\uFEFF')
                position = 1;
        }

        public int Position => position;

        public bool AtEnd => position >= text.Length;

        public char Peek => position < text.Length ? text[position] : '\0';

        public void Advance()
        {
            position++;
        }

        /// <summary>Skips spaces, tabs, line breaks and <c>;</c> comments.</summary>
        public void SkipWhitespace()
        {
            while (position < text.Length)
            {
                char c = text[position];
                if (c == ';')
                {
                    while (position < text.Length && text[position] != '\n')
                        position++;
                }
                else if (char.IsWhiteSpace(c))
                {
                    position++;
                }
                else
                {
                    break;
                }
            }
        }

        /// <summary>Skips whitespace, then consumes <paramref name="expected"/> or fails.</summary>
        public void Expect(char expected)
        {
            SkipWhitespace();
            if (Peek != expected || AtEnd)
                throw Fail($"Expected '{expected}' but found {Describe()}.");

            position++;
        }

        /// <summary>Skips whitespace, then consumes <paramref name="c"/> if it is next.</summary>
        public bool TryConsume(char c)
        {
            SkipWhitespace();
            if (!AtEnd && text[position] == c)
            {
                position++;
                return true;
            }

            return false;
        }

        public static bool IsIdentifierStart(char c)
        {
            return c == '_' || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }

        public static bool IsIdentifierPart(char c)
        {
            return IsIdentifierStart(c) || (c >= '0' && c <= '9');
        }

        /// <summary>Skips whitespace, then reads <c>[A-Za-z_][A-Za-z0-9_]*</c>.</summary>
        public string ReadIdentifier()
        {
            SkipWhitespace();
            if (!IsIdentifierStart(Peek))
                throw Fail($"Expected a name but found {Describe()}.");

            int start = position;
            while (position < text.Length && IsIdentifierPart(text[position]))
                position++;

            return text.Substring(start, position - start);
        }

        /// <summary>Skips whitespace, then reads a double-quoted string with JSON-style escapes.</summary>
        public string ReadString()
        {
            SkipWhitespace();
            if (Peek != '"' || AtEnd)
                throw Fail($"Expected a quoted string but found {Describe()}.");

            position++;
            var sb = new StringBuilder();

            while (true)
            {
                if (AtEnd)
                    throw Fail("The string is missing its closing quote.");

                char c = text[position++];
                if (c == '"')
                    return sb.ToString();

                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }

                if (AtEnd)
                    throw Fail("The string is missing its closing quote.");

                char escape = text[position++];
                switch (escape)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        if (position + 4 > text.Length
                            || !int.TryParse(text.Substring(position, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int code))
                        {
                            throw Fail("\\u must be followed by four hex digits.");
                        }

                        sb.Append((char)code);
                        position += 4;
                        break;
                    default:
                        throw Fail($"Unknown escape sequence '\\{escape}'.");
                }
            }
        }

        /// <summary>
        /// Skips whitespace, then reads a number token (<c>-12</c>, <c>3.5</c>, <c>1E-05</c>).
        /// Returns the token text; <paramref name="isFloat"/> says whether it had a fraction or exponent.
        /// </summary>
        public string ReadNumberToken(out bool isFloat)
        {
            SkipWhitespace();
            int start = position;
            isFloat = false;

            if (Peek == '-' || Peek == '+')
                position++;

            int digitsStart = position;
            while (position < text.Length && char.IsDigit(text[position]))
                position++;

            if (position == digitsStart)
            {
                position = start;
                throw Fail($"Expected a number but found {Describe()}.");
            }

            if (position < text.Length && text[position] == '.')
            {
                isFloat = true;
                position++;
                while (position < text.Length && char.IsDigit(text[position]))
                    position++;
            }

            if (position < text.Length && (text[position] == 'e' || text[position] == 'E'))
            {
                isFloat = true;
                position++;
                if (position < text.Length && (text[position] == '-' || text[position] == '+'))
                    position++;

                int exponentStart = position;
                while (position < text.Length && char.IsDigit(text[position]))
                    position++;

                if (position == exponentStart)
                    throw Fail("The number's exponent has no digits.");
            }

            return text.Substring(start, position - start);
        }

        /// <summary>Creates an exception pointing at the current position.</summary>
        public ResourceFormatException Fail(string message)
        {
            return FailAt(position, message);
        }

        /// <summary>Creates an exception pointing at a given character position.</summary>
        public ResourceFormatException FailAt(int at, string message)
        {
            int line = 1;
            int column = 1;
            int end = Math.Min(at, text.Length);
            for (int i = 0; i < end; i++)
            {
                if (text[i] == '\n')
                {
                    line++;
                    column = 1;
                }
                else
                {
                    column++;
                }
            }

            return new ResourceFormatException(message, line, column);
        }

        /// <summary>Describes the next character for error messages.</summary>
        public string Describe()
        {
            if (AtEnd)
                return "the end of the file";

            char c = Peek;
            if (c == '\n')
                return "a line break";

            return $"'{c}'";
        }
    }
}
