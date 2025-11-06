using System;
using System.Text;

namespace JsonFileParserConsole
{
    internal enum TokenKind
    {
        LBrace, RBrace, LBracket, RBracket, Colon, Comma,
        String, Number, True, False, Null,
        EOF
    }

    internal readonly struct Token
    {
        public TokenKind Kind { get; }
        public string? Text { get; }
        public int Line { get; }
        public int Col { get; }   

        public Token(TokenKind kind, string? text, int line, int col)
        {
            Kind = kind; Text = text; Line = line; Col = col;
        }

        public override string ToString() => $"{Kind} @ {Line}:{Col} {(Text is null ? "" : "[" + Text + "]")}";
    }

    internal sealed class JsonLexer
    {
        private readonly string _source;
        private int _index;
        private int _line = 1;
        private int _col;

        public JsonLexer(string source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public int Index => _index;
        public int Line => _line;
        public int Column => _col + 1;
        public string Source => _source;

        public Token Next()
        {
            SkipWhiteSpaces();

            if (Eof()) return CreateToken(TokenKind.EOF);

            char c = Peek();
            return c switch
            {
                '{' => Bump(TokenKind.LBrace),
                '}' => Bump(TokenKind.RBrace),
                '[' => Bump(TokenKind.LBracket),
                ']' => Bump(TokenKind.RBracket),
                ':' => Bump(TokenKind.Colon),
                ',' => Bump(TokenKind.Comma),
                '"' => ReadString(),
                't' => ReadKeyword("true", TokenKind.True),
                'f' => ReadKeyword("false", TokenKind.False),
                'n' => ReadKeyword("null", TokenKind.Null),
                '-' or >= '0' and <= '9' => ReadNumber(),
                _ => throw Error($"Unexpected character '{c}'")
            };
        }

        private Token Bump(TokenKind kind)
        {
            int l = _line, c = _col + 1;
            Read();
            return new Token(kind, null, l, c);
        }

        private Token CreateToken(TokenKind k, string? text = null) => new(k, text, _line, _col + 1);

        private bool Eof() => _index >= _source.Length;
        private char Peek(int la = 0) => _source[_index + la];

        private char Read()
        {
            if (Eof())
            {
                throw Error("Unexpected end of input");
            }

            char c = _source[_index++];
            _col++;

            if (c == '\n') 
            { 
                _line++; 
                _col = 0; 
            }

            return c;
        }

        private void SkipWhiteSpaces()
        {
            while (!Eof())
            {
                char c = Peek();
                if (c is ' ' or '\t' or '\r' or '\n') 
                { 
                    Read(); 
                    continue; 
                }
                break;
            }
        }

        private JsonParseException Error(string message)
        {
            var (line, caret) = ContextAtCursor();
            return new JsonParseException(message, _line, _col + 1, partialFormatted: "", line, caret);
        }

        private (string line, string caret) ContextAtCursor()
        {
            int s = _index - 1; 
            while (s >= 0 && _source[s] != '\n' && _source[s] != '\r')
            {
                s--;
            }

            int lineStart = s + 1;
            int e = _index; 
            while (e < _source.Length && _source[e] != '\n' && _source[e] != '\r')
            {
                e++;
            }

            string lineText = _source.Substring(lineStart, Math.Max(0, e - lineStart));
            string caret = new string(' ', Math.Max(0, _col)) + '^';

            return (lineText, caret);
        }

        private Token ReadString()
        {
            int l = _line, c = _col + 1;
            Read();
            var sb = new StringBuilder();

            while (!Eof())
            {
                char character = Read();

                if (character == '"')
                {
                    return new Token(TokenKind.String, sb.ToString(), l, c);
                }

                if (character == '\\')
                {
                    if (Eof()) throw Error("Unfinished escape sequence");

                    char e = Read();

                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            {
                                int cUnicodeHigh = ReadHex4();
                                if (cUnicodeHigh is >= 0xD800 and <= 0xDBFF)
                                {
                                    if (Eof() || Peek() != '\\') throw Error("High surrogate must be followed by a low surrogate \\uXXXX");
                                    Read();
                                    if (Eof() || Peek() != 'u') throw Error("High surrogate must be followed by a low surrogate \\uXXXX");
                                    Read();
                                    
                                    int cUnicodeLow = ReadHex4();
                                    if (cUnicodeLow is < 0xDC00 or > 0xDFFF) throw Error("Invalid low surrogate after high surrogate");
                                    
                                    int codePoint = 0x10000 + ((cUnicodeHigh - 0xD800) << 10) + (cUnicodeLow - 0xDC00);

                                    sb.Append(char.ConvertFromUtf32(codePoint));
                                }
                                else
                                {
                                    sb.Append((char)cUnicodeHigh);
                                }
                                break;
                            }
                        default:
                            throw Error($"Invalid escape sequence '\\{e}'");
                    }
                }
                else
                {
                    if (character is '\r' or '\n') throw Error("Unescaped newline in string");
                    sb.Append(character);
                }
            }

            throw Error("Unterminated string (missing \")");
        }

        private int ReadHex4()
        {
            int hexValue = 0;
            for (int k = 0; k < 4; k++)
            {
                if (Eof()) throw Error("Incomplete \\uXXXX escape");
                char character = Read();
                int digit = character switch
                {
                    >= '0' and <= '9' => character - '0',
                    >= 'a' and <= 'f' => 10 + (character - 'a'), // calculate digit value for hex
                    >= 'A' and <= 'F' => 10 + (character - 'A'), // from 10 to 15
                    _ => -1
                };
                if (digit < 0) throw Error("Invalid hex digit in \\uXXXX escape");

                hexValue = (hexValue << 4) | digit; // << 4 is same as *16. | is same as + for positive d
                                                    // i am just have fun with bitwise operations
            }
            return hexValue;
        }

        private Token ReadKeyword(string keyword, TokenKind tKind)
        {
            int l = _line, c = _col + 1;
            for (int i = 0; i < keyword.Length; i++)
            {
                if (Eof() || Peek() != keyword[i]) throw Error($"Invalid literal; expected '{keyword}'");
                Read();
            }
            return new Token(tKind, keyword, l, c);
        }

        private Token ReadNumber()
        {
            int l = _line, c = _col + 1;
            int start = _index;

            if (Peek() == '-')
            {
                Read();
            }

            if (Eof()) throw Error("Invalid number: missing digits");

            if (Peek() == '0')
            {
                Read();
                if (!Eof() && char.IsDigit(Peek()))
                    throw Error("Leading zeros are not allowed");
            }
            else if (char.IsDigit(Peek()))
            {
                while (!Eof() && char.IsDigit(Peek()))
                {
                    Read();
                }
            }
            else throw Error("Invalid number: missing integer part");

            if (!Eof() && Peek() == '.')
            {
                Read();
                if (Eof() || !char.IsDigit(Peek())) throw Error("Invalid number: missing digit after decimal point");

                while (!Eof() && char.IsDigit(Peek()))
                { 
                    Read();
                }
            }

            if (!Eof() && (Peek() == 'e' || Peek() == 'E'))
            {
                Read();
                if (!Eof() && (Peek() == '+' || Peek() == '-')) 
                {
                    Read();
                }
                
                if (Eof() || !char.IsDigit(Peek())) throw Error("Invalid number: missing digit in exponent");

                while (!Eof() && char.IsDigit(Peek()))
                { 
                    Read();
                }
            }

            string lexeme = _source[start.._index];
            return new Token(TokenKind.Number, lexeme, l, c);
        }
    }
}
