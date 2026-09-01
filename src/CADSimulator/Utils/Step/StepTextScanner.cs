namespace CADSimulator.Utils.Step
{
    internal class StepTextScanner
    {
        private readonly string _text;
        private int _pos;

        public StepTextScanner(string text)
        {
            _text = text;
            _pos = 0;
        }

        public bool IsAtEnd => _pos >= _text.Length;

        public char Peek(int offset = 0)
        {
            var index = _pos + offset;
            return index < _text.Length ? _text[index] : '\0';
        }

        public char Advance() => _text[_pos++];

        public void SkipWhitespaceAndComments()
        {
            while (!IsAtEnd)
            {
                var c = Peek();
                if (char.IsWhiteSpace(c))
                {
                    _pos++;
                }
                else if (c == '/' && Peek(1) == '*')
                {
                    _pos += 2;
                    while (!IsAtEnd && !(Peek() == '*' && Peek(1) == '/'))
                    {
                        _pos++;
                    }

                    if (!IsAtEnd)
                    {
                        _pos += 2;
                    }
                }
                else
                {
                    break;
                }
            }
        }
    }
}
