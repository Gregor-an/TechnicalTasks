using System.Text;
using JsonFileParserConsole;

Console.OutputEncoding = Encoding.UTF8;

string baseDir = AppContext.BaseDirectory;
string inputPath = Path.Combine(baseDir, "dataInput.json");
string input = File.ReadAllText(inputPath, Encoding.UTF8);

try
{
    var formatedJson = new JsonPrettyFormatter(input, indentSize: 2).Format();
    Console.WriteLine(formatedJson);
}
catch (JsonParseException jex)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Error.WriteLine("==== PARTIAL FORMATTED OUTPUT ====");
    Console.ResetColor();
    Console.WriteLine(jex.PartialFormatted);
    Console.WriteLine();

    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine($"==== JSON ERROR at line:{jex.Line} col:{jex.Column} ====");
    Console.Error.WriteLine(jex.Message);

    if (!string.IsNullOrEmpty(jex.ContextLine))
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine(jex.ContextLine);
        if (!string.IsNullOrEmpty(jex.CaretLine))
            Console.Error.WriteLine(jex.CaretLine);
    }
    Console.ResetColor();
}
