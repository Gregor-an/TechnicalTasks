namespace JsonFileParserConsole
{
    public sealed class JsonParseException : Exception
    {
        public int Line { get; }
        public int Column { get; }
        public string PartialFormatted { get; }
        public string? ContextLine { get; }
        public string? CaretLine { get; }


        public JsonParseException(
            string message, int line, int column,
            string partialFormatted,
            string? contextLine = null,
            string? caretLine = null) 
        : base(message)
        {
            Line = line; 
            Column = column;
            PartialFormatted = partialFormatted;
            ContextLine = contextLine;
            CaretLine = caretLine;
        }
    }
}
