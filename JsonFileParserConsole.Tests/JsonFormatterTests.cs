using JsonFileParserConsole;

public class JsonPrettyFormatterTests
{
    [Theory]
    [InlineData(@"{}")]
    [InlineData(@"[]")]
    [InlineData(@"{ ""a"": 1 }")]
    [InlineData(@"{ ""user"": { ""id"": 1, ""name"": ""Іван"", ""roles"": [""admin"",""user""], ""active"": true } }")]
    [InlineData(@"{ ""emoji"": ""\uD83D\uDE03"", ""ua"": ""Україна"" }")]
    [InlineData(@"{ ""n1"": 0, ""n2"": -12, ""n3"": 3.14, ""n4"": 6.022e+23 }")]
    public void Format_ValidJson_ProducesOutput(string json)
    {
        var formatter = new JsonPrettyFormatter(json, indentSize: 2);
        string result = formatter.Format();

        Assert.False(string.IsNullOrWhiteSpace(result));
        char first = result.TrimStart()[0];
        Assert.True(first == '{' || first == '[', "Formatted output should start with { or [");
    }

    [Theory]
    [InlineData(@"{ ""a"": 1")]                   
    [InlineData(@"{ ""a"": 1, }")]                
    [InlineData(@"[1,2,3,]")]                     
    [InlineData(@"{ ""a"": 1 ""b"": 2 }")]        
    [InlineData(@"{ ""a"" 1 }")]                  
    [InlineData(@"{ a: 1 }")]                     
    [InlineData(@"{ ""t"": ""\x"" }")]            
    [InlineData(@"{ ""u"": ""\u12G4"" }")]        
    [InlineData(@"{ ""s"": ""text }")]            
    [InlineData(@"{ ""a"": tru }")]               
    [InlineData(@"{ ""n"": 01 }")]                
    [InlineData(@"{ ""n"": 1. }")]                
    [InlineData(@"{ {} }")]                       
    [InlineData(@"{ ""n"": -.5 }")]               
    public void Format_InvalidJson_Throws_WithPosition_AndPartial(string json)
    {
        try
        {
            new JsonPrettyFormatter(json).Format();
            Assert.Fail("Expected JsonParseException was not thrown");
        }
        catch (JsonParseException ex)
        {
            Assert.False(string.IsNullOrWhiteSpace(ex.Message));
            Assert.True(ex.Line > 0, "Line should be > 0");
            Assert.True(ex.Column > 0, "Column should be > 0");
            Assert.NotNull(ex.PartialFormatted);
        }
        catch (Exception ex)
        {
            Assert.Fail($"Expected JsonParseException, got {ex.GetType().Name}");
        }
    }
}
