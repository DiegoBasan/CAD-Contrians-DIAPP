using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CADSimulator.Utils.Step
{
    /// <summary>
    /// Generic reader for the STEP (ISO 10303-21) physical file structure: turns the DATA
    /// section into a table of entities (id -&gt; keyword + parameter values) without knowing
    /// about any particular EXPRESS schema (AP203/AP214/...). This preserves the exact BREP
    /// entity graph (faces, edges, analytic surfaces, product/assembly structure) as written in
    /// the file — nothing here tessellates or approximates geometry. Callers interpret the
    /// entities they care about (PRODUCT, NEXT_ASSEMBLY_USAGE_OCCURRENCE, ADVANCED_FACE, ...).
    /// </summary>
    public class StepRawParser
    {
        public Dictionary<int, StepEntity> EntitiesById { get; } = new Dictionary<int, StepEntity>();

        public static StepRawParser Parse(string text)
        {
            var parser = new StepRawParser();
            parser.ParseInternal(text);
            return parser;
        }

        private void ParseInternal(string text)
        {
            var dataStart = text.IndexOf("DATA;", StringComparison.Ordinal);
            var content = dataStart >= 0 ? text.Substring(dataStart + "DATA;".Length) : text;

            var scanner = new StepTextScanner(content);
            scanner.SkipWhitespaceAndComments();

            while (!scanner.IsAtEnd)
            {
                scanner.SkipWhitespaceAndComments();
                if (scanner.IsAtEnd || scanner.Peek() != '#')
                {
                    // Reached ENDSEC; (or trailing content) — the data section is over.
                    break;
                }

                try
                {
                    ParseEntity(scanner);
                }
                catch (FormatException)
                {
                    // Don't let one malformed/exotic entity take down the whole file.
                    SkipToNextSemicolon(scanner);
                }

                scanner.SkipWhitespaceAndComments();
            }
        }

        private void ParseEntity(StepTextScanner scanner)
        {
            scanner.Advance(); // '#'
            var id = ReadInteger(scanner);

            scanner.SkipWhitespaceAndComments();
            Expect(scanner, '=');
            scanner.SkipWhitespaceAndComments();

            var entity = new StepEntity { Id = id };

            if (scanner.Peek() == '(')
            {
                // Complex instance: "(KEYWORD_A(...) KEYWORD_B(...) ...)"
                scanner.Advance();
                scanner.SkipWhitespaceAndComments();
                while (scanner.Peek() != ')')
                {
                    entity.Blocks.Add(ReadBlock(scanner));
                    scanner.SkipWhitespaceAndComments();
                }

                scanner.Advance(); // ')'
            }
            else
            {
                entity.Blocks.Add(ReadBlock(scanner));
            }

            scanner.SkipWhitespaceAndComments();
            Expect(scanner, ';');

            EntitiesById[entity.Id] = entity;
        }

        private StepEntityBlock ReadBlock(StepTextScanner scanner)
        {
            var keyword = ReadIdentifier(scanner);
            scanner.SkipWhitespaceAndComments();
            Expect(scanner, '(');
            var parameters = ReadValueList(scanner);
            return new StepEntityBlock { Keyword = keyword, Parameters = parameters };
        }

        /// <summary>Reads comma-separated values up to (and consuming) the closing ')'.</summary>
        private List<StepValue> ReadValueList(StepTextScanner scanner)
        {
            var values = new List<StepValue>();
            scanner.SkipWhitespaceAndComments();
            if (scanner.Peek() == ')')
            {
                scanner.Advance();
                return values;
            }

            while (true)
            {
                values.Add(ReadValue(scanner));
                scanner.SkipWhitespaceAndComments();
                var c = scanner.Peek();
                if (c == ',')
                {
                    scanner.Advance();
                    scanner.SkipWhitespaceAndComments();
                    continue;
                }

                if (c == ')')
                {
                    scanner.Advance();
                    break;
                }

                throw new FormatException($"Expected ',' or ')' but found '{c}'.");
            }

            return values;
        }

        private StepValue ReadValue(StepTextScanner scanner)
        {
            scanner.SkipWhitespaceAndComments();
            var c = scanner.Peek();

            if (c == '#')
            {
                scanner.Advance();
                return StepValue.MakeReference(ReadInteger(scanner));
            }

            if (c == '\'')
            {
                return StepValue.MakeText(ReadString(scanner));
            }

            if (c == '.')
            {
                return StepValue.MakeEnumeration(ReadEnumeration(scanner));
            }

            if (c == '$')
            {
                scanner.Advance();
                return StepValue.MakeOmitted();
            }

            if (c == '*')
            {
                scanner.Advance();
                return StepValue.MakeInherited();
            }

            if (c == '(')
            {
                scanner.Advance();
                return StepValue.MakeList(ReadValueList(scanner));
            }

            if (c == '-' || c == '+' || char.IsDigit(c))
            {
                return StepValue.MakeNumber(ReadNumber(scanner));
            }

            if (char.IsLetter(c) || c == '_')
            {
                // Inline typed value (rare outside complex entity blocks).
                var keyword = ReadIdentifier(scanner);
                scanner.SkipWhitespaceAndComments();
                if (scanner.Peek() == '(')
                {
                    scanner.Advance();
                    return StepValue.MakeList(ReadValueList(scanner), keyword);
                }

                return StepValue.MakeEnumeration(keyword);
            }

            throw new FormatException($"Unexpected character '{c}' while reading a STEP value.");
        }

        private static string ReadString(StepTextScanner scanner)
        {
            scanner.Advance(); // opening quote
            var sb = new StringBuilder();
            while (true)
            {
                if (scanner.IsAtEnd)
                {
                    throw new FormatException("Unterminated STEP string literal.");
                }

                var c = scanner.Advance();
                if (c == '\'')
                {
                    if (scanner.Peek() == '\'')
                    {
                        scanner.Advance(); // escaped '' -> '
                        sb.Append('\'');
                        continue;
                    }

                    break;
                }

                sb.Append(c);
            }

            return sb.ToString();
        }

        private static string ReadEnumeration(StepTextScanner scanner)
        {
            scanner.Advance(); // opening '.'
            var sb = new StringBuilder();
            while (!scanner.IsAtEnd && scanner.Peek() != '.')
            {
                sb.Append(scanner.Advance());
            }

            if (!scanner.IsAtEnd)
            {
                scanner.Advance(); // closing '.'
            }

            return sb.ToString();
        }

        private static string ReadIdentifier(StepTextScanner scanner)
        {
            var sb = new StringBuilder();
            while (!scanner.IsAtEnd && (char.IsLetterOrDigit(scanner.Peek()) || scanner.Peek() == '_'))
            {
                sb.Append(scanner.Advance());
            }

            return sb.ToString();
        }

        private static int ReadInteger(StepTextScanner scanner)
        {
            var sb = new StringBuilder();
            while (!scanner.IsAtEnd && char.IsDigit(scanner.Peek()))
            {
                sb.Append(scanner.Advance());
            }

            return int.Parse(sb.ToString(), CultureInfo.InvariantCulture);
        }

        private static double ReadNumber(StepTextScanner scanner)
        {
            var sb = new StringBuilder();
            if (scanner.Peek() == '-' || scanner.Peek() == '+')
            {
                sb.Append(scanner.Advance());
            }

            while (!scanner.IsAtEnd && char.IsDigit(scanner.Peek()))
            {
                sb.Append(scanner.Advance());
            }

            if (scanner.Peek() == '.')
            {
                sb.Append(scanner.Advance());
                while (!scanner.IsAtEnd && char.IsDigit(scanner.Peek()))
                {
                    sb.Append(scanner.Advance());
                }
            }

            if (scanner.Peek() == 'E' || scanner.Peek() == 'e')
            {
                sb.Append(scanner.Advance());
                if (scanner.Peek() == '-' || scanner.Peek() == '+')
                {
                    sb.Append(scanner.Advance());
                }

                while (!scanner.IsAtEnd && char.IsDigit(scanner.Peek()))
                {
                    sb.Append(scanner.Advance());
                }
            }

            return double.Parse(sb.ToString(), CultureInfo.InvariantCulture);
        }

        private static void Expect(StepTextScanner scanner, char expected)
        {
            var c = scanner.Advance();
            if (c != expected)
            {
                throw new FormatException($"Expected '{expected}' but found '{c}'.");
            }
        }

        private static void SkipToNextSemicolon(StepTextScanner scanner)
        {
            while (!scanner.IsAtEnd && scanner.Peek() != ';')
            {
                scanner.Advance();
            }

            if (!scanner.IsAtEnd)
            {
                scanner.Advance();
            }
        }
    }
}
