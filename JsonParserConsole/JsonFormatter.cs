using System;
using System.Collections.Generic;
using System.Text;

namespace JsonFileParserConsole
{
    public sealed class JsonPrettyFormatter
    {
        private readonly JsonLexer _jsonLexer;
        private readonly int _indentSize;
        private readonly StringBuilder _out = new();

        private Token _cursor;
        private int _indent;

        private enum CtxType { Obj, Arr }
        private enum ObjState { ExpectKeyOrEnd, ExpectColon, ExpectValue, ExpectCommaOrEnd }
        private enum ArrState { ExpectValueOrEnd, ExpectCommaOrEnd }

        private readonly Stack<CtxType> _types = new();
        private readonly Stack<object> _contextStack = new();

        public JsonPrettyFormatter(string source, int indentSize = 2)
        {
            _jsonLexer = new JsonLexer(source);
            _indentSize = indentSize;
            _cursor = _jsonLexer.Next();
        }

        public string Format()
        {
            try
            {
                if (_cursor.Kind is not TokenKind.LBrace and not TokenKind.LBracket)
                    throw ErrorLocation("JSON must start with '{' or '['");

                ParseValue(topLevel: true);

                if (_cursor.Kind != TokenKind.EOF)
                    throw ErrorLocation("Unexpected trailing characters after valid JSON");

                return _out.ToString();
            }
            catch (JsonParseException ex)
            {
                if (string.IsNullOrEmpty(ex.PartialFormatted))
                {
                    throw new JsonParseException(
                        ex.Message, ex.Line, ex.Column,
                        _out.ToString(),
                        ex.ContextLine, ex.CaretLine);
                }
                throw;
            }
        }

        private void ParseValue(bool topLevel = false)
        {
            switch (_cursor.Kind)
            {
                case TokenKind.LBrace:
                    {
                        ParseObject();
                        break;
                    }
                case TokenKind.LBracket:
                    {
                        ParseArray();
                        break;
                    }
                case TokenKind.String:
                    {
                        WriteStringAndAdvance(_cursor.Text!);
                        break;
                    }
                case TokenKind.Number:
                    {
                        _out.Append(_cursor.Text!);
                        Advance();
                        OnScalarValueCompleted();
                        break;
                    }
                case TokenKind.True:
                    {
                        _out.Append("true");
                        Advance();
                        OnScalarValueCompleted();
                        break;
                    }
                case TokenKind.False:
                    {
                        _out.Append("false");
                        Advance();
                        OnScalarValueCompleted();
                        break;
                    }
                case TokenKind.Null:
                    {
                        _out.Append("null");
                        Advance();
                        OnScalarValueCompleted();
                        break;
                    }
                default:
                    {
                        throw ErrorLocation("Value expected");
                    }
            }

            if (topLevel) _out.AppendLine();
        }

        private void ParseObject()
        {
            ValidateEnterValueIntoParent();

            Expect(TokenKind.LBrace, "'{' expected"); 
            _out.Append('{'); 
            
            NewLine();
            
            PushObj(ObjState.ExpectKeyOrEnd);
            
            IndentIncrement(); 
            
            AddIndent();

            if (_cursor.Kind == TokenKind.RBrace)
            {
                Advance(); 
                NewLine(); 
                IndentDec(); 
                AddIndent(); 
                
                _out.Append('}');

                OnContainerClosed();
                return;
            }

            while (true)
            {
                ExpectObjState(ObjState.ExpectKeyOrEnd, "Object expects a string key");
                if (_cursor.Kind != TokenKind.String) throw ErrorLocation("Object key must be a string");

                WriteStringAndAdvance(_cursor.Text!, isKey: true);

                Expect(TokenKind.Colon, "Missing ':' after object key");
                _out.Append(": "); 
                SetObj(ObjState.ExpectValue);

                ParseValue();

                ExpectObjState(ObjState.ExpectCommaOrEnd, "Object expects ',' or '}'");
                if (_cursor.Kind == TokenKind.Comma)
                {
                    Advance(); 

                    _out.Append(','); 
                    
                    NewLine(); 
                    
                    AddIndent();
                    SetObj(ObjState.ExpectKeyOrEnd);
                    continue;
                }

                if (_cursor.Kind == TokenKind.RBrace)
                {
                    Advance(); 
                    
                    NewLine(); 
                    
                    IndentDec(); 
                    AddIndent(); 
                    
                    _out.Append('}');
                    
                    PopObj(); 
                    OnContainerClosed();
                    break;
                }
                throw ErrorLocation("Expected ',' or '}' in object");
            }
        }

        private void ParseArray()
        {
            ValidateEnterValueIntoParent();

            Expect(TokenKind.LBracket, "'[' expected"); _out.Append('['); 
            
            NewLine();
            
            PushArr(ArrState.ExpectValueOrEnd);
            IndentIncrement(); 
            AddIndent();

            if (_cursor.Kind == TokenKind.RBracket)
            {
                Advance(); 
                
                NewLine(); 
                
                IndentDec(); 
                
                AddIndent(); 
                
                _out.Append(']');

                OnContainerClosed();
                return;
            }

            while (true)
            {
                ExpectArrState(ArrState.ExpectValueOrEnd, "Array expects a value");
                ParseValue();

                ExpectArrState(ArrState.ExpectCommaOrEnd, "Array expects ',' or ']'");
                if (_cursor.Kind == TokenKind.Comma)
                {
                    Advance();
                    
                    _out.Append(','); 
                    
                    NewLine(); 
                    
                    AddIndent();
                    SetArr(ArrState.ExpectValueOrEnd);
                    continue;
                }
                if (_cursor.Kind == TokenKind.RBracket)
                {
                    Advance(); 
                    
                    NewLine(); 
                    
                    IndentDec(); 
                    
                    AddIndent(); 
                    
                    _out.Append(']');

                    PopArr(); 
                    
                    OnContainerClosed();
                    break;
                }
                throw ErrorLocation("Expected ',' or ']' in array");
            }
        }

        private void WriteStringAndAdvance(string decoded, bool isKey = false)
        {
            _out.Append('"');
            _out.Append(decoded);
            _out.Append('"');

            Advance();

            if (isKey)
            {
                SetObj(ObjState.ExpectColon); 
            }
            else OnScalarValueCompleted();
        }

        private void Advance() => _cursor = _jsonLexer.Next();

        private Token Expect(TokenKind kind, string message)
        {
            if (_cursor.Kind != kind) throw ErrorLocation(message);
            
            var t = _cursor; 
            Advance();
            
            return t;
        }

        private void OnScalarValueCompleted()
        {
            if (_types.Count == 0) return;

            if (_types.Peek() == CtxType.Obj)
            {
                var st = (ObjState)_contextStack.Peek();
                if (st == ObjState.ExpectValue) 
                { 
                    _contextStack.Pop(); 
                    _contextStack.Push(ObjState.ExpectCommaOrEnd); 
                }
            }
            else
            {
                var st = (ArrState)_contextStack.Peek();
                if (st == ArrState.ExpectValueOrEnd) 
                { 
                    _contextStack.Pop(); 
                    _contextStack.Push(ArrState.ExpectCommaOrEnd); 
                }
            }
        }

        private void ValidateEnterValueIntoParent()
        {
            if (_types.Count == 0) return;

            if (_types.Peek() == CtxType.Obj)
            {
                ObjState oState = (ObjState)_contextStack.Peek();
                if (oState != ObjState.ExpectValue && oState != ObjState.ExpectKeyOrEnd)
                    throw ErrorLocation("Object member is in invalid state (':' or ',' misplaced)");
            }
            else
            {
                ArrState aState = (ArrState)_contextStack.Peek();
                if (aState != ArrState.ExpectValueOrEnd)
                    throw ErrorLocation("Array is not expecting a value here");
            }
        }

        private void OnContainerClosed()
        {
            if (_types.Count == 0) return;

            if (_types.Peek() == CtxType.Obj)
            {
                ObjState oState = (ObjState)_contextStack.Peek();
                if (oState == ObjState.ExpectValue) 
                { 
                    _contextStack.Pop(); 
                    _contextStack.Push(ObjState.ExpectCommaOrEnd);
                }
            }
            else
            {
                ArrState aState = (ArrState)_contextStack.Peek();
                if (aState == ArrState.ExpectValueOrEnd) 
                { 
                    _contextStack.Pop(); 
                    _contextStack.Push(ArrState.ExpectCommaOrEnd);
                }
            }
        }

        private void PushObj(ObjState st) 
        { 
            _types.Push(CtxType.Obj); 
            _contextStack.Push(st); 
        }

        private void PopObj() 
        { 
            _types.Pop(); 
            _contextStack.Pop(); 
        }
        private void SetObj(ObjState st) 
        { 
            _contextStack.Pop();
            _contextStack.Push(st);
        }
        private void ExpectObjState(ObjState st, string msg)
        {
            if (_types.Count == 0 || _types.Peek() != CtxType.Obj || (ObjState)_contextStack.Peek() != st)
                throw ErrorLocation(msg);
        }

        private void PushArr(ArrState st) 
        { 
            _types.Push(CtxType.Arr); 
            _contextStack.Push(st);
        }
        private void PopArr() 
        { 
            _types.Pop(); 
            _contextStack.Pop();
        }
        private void SetArr(ArrState st) 
        { 
            _contextStack.Pop(); 
            _contextStack.Push(st);
        }
        private void ExpectArrState(ArrState st, string msg)
        {
            if (_types.Count == 0 || _types.Peek() != CtxType.Arr || (ArrState)_contextStack.Peek() != st)
                throw ErrorLocation(msg);
        }

        private void IndentIncrement() => _indent++;
        private void IndentDec() => _indent = Math.Max(0, _indent - 1);
        private void AddIndent() => _out.Append(' ', _indent * _indentSize);
        private void NewLine() => _out.AppendLine();

        private JsonParseException ErrorLocation(string message)
        {
            var (lineText, caret) = GetErrorLineAndCaretFromSource(_jsonLexer.Source, _jsonLexer.Index, _jsonLexer.Column - 1);
            return new JsonParseException(message, _jsonLexer.Line, _jsonLexer.Column, _out.ToString(), lineText, caret);
        }

        private static (string lineText, string caretLine) GetErrorLineAndCaretFromSource(string src, int idx, int col)
        {
            int i = idx - 1;

            while (i >= 0 && src[i] != '\n' && src[i] != '\r') 
            {
                i--;
            }

            int lineStart = i + 1;
            int k = idx;
            while (k < src.Length && src[k] != '\n' && src[k] != '\r') 
            {
                k++;
            }
            
            string lineText = src.Substring(lineStart, Math.Max(0, k - lineStart));
            string caret = new string(' ', Math.Max(0, col)) + '^';

            return (lineText, caret);
        }
    }
}
