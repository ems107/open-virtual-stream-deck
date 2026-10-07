using OVSD.Core.Expressions;

namespace OVSD.Core.Tests;

public class ExpressionTests
{
    private static readonly Dictionary<string, object?> Vars = new(StringComparer.OrdinalIgnoreCase)
    {
        ["obs.scene"] = "Main",
        ["sys.cpu"] = 12.345,
        ["audio.muted"] = true,
        ["mqtt.home/temp"] = "21.5",
        ["payload"] = """{"a":{"b":[10,20]}}""",
        ["empty"] = "",
    };

    private static object? Eval(string expr) => Expression.Evaluate(expr, n => Vars.GetValueOrDefault(n));

    [Theory]
    [InlineData("1 + 2 * 3", 7.0)]
    [InlineData("(1 + 2) * 3", 9.0)]
    [InlineData("-2 + 5", 3.0)]
    [InlineData("10 % 4", 2.0)]
    [InlineData("round(sys.cpu, 1)", 12.3)]
    [InlineData("[mqtt.home/temp] + 1", 22.5)]
    [InlineData("json(payload, 'a.b.1')", 20.0)]
    [InlineData("max(1, 5, 3)", 5.0)]
    [InlineData("default(missing, 0) + 1", 1.0)]
    [InlineData("len('hola')", 4.0)]
    public void Arithmetic(string expr, double expected) => Assert.Equal(expected, Eval(expr));

    [Theory]
    [InlineData("obs.scene == 'Main'", true)]
    [InlineData("obs.scene != 'Main'", false)]
    [InlineData("audio.muted && sys.cpu > 10", true)]
    [InlineData("!audio.muted || false", false)]
    [InlineData("not audio.muted or sys.cpu >= 12.345", true)]
    [InlineData("missing == null", true)]
    [InlineData("[mqtt.home/temp] > 20", true)]
    [InlineData("'10' == 10", true)]
    [InlineData("contains(obs.scene, 'ai')", true)]
    [InlineData("sys.cpu <= 12 and true", false)]
    public void Logic(string expr, bool expected) => Assert.Equal(expected, Eval(expr));

    [Theory]
    [InlineData("audio.muted ? 'on' : 'off'", "on")]
    [InlineData("if(sys.cpu > 50, 'high', 'low')", "low")]
    [InlineData("'CPU ' + round(sys.cpu) + '%'", "CPU 12%")]
    [InlineData("upper(obs.scene)", "MAIN")]
    [InlineData("empty ?? 'fallback'", "fallback")]
    [InlineData("truncate('abcdefgh', 4)", "abc…")]
    [InlineData("format(sys.cpu, '0.0')", "12.3")]
    [InlineData("\"quoted \\\"x\\\"\"", "quoted \"x\"")]
    public void Strings(string expr, string expected) => Assert.Equal(expected, Eval(expr));

    [Theory]
    [InlineData("1 +")]
    [InlineData("foo(1)")]
    [InlineData("'unterminated")]
    [InlineData("(1")]
    [InlineData("round()")]
    public void InvalidExpressionsThrow(string expr) => Assert.Throws<ExpressionException>(() => Eval(expr));

    [Fact]
    public void DivisionByZeroIsNull() => Assert.Null(Eval("1 / 0"));
}

public class TemplateTests
{
    private static readonly Dictionary<string, object?> Vars = new() { ["sys.cpu"] = 41.666, ["value"] = 30.0 };
    private static object? Resolve(string n) => Vars.GetValueOrDefault(n);

    [Theory]
    [InlineData("CPU {{round(sys.cpu)}}%", "CPU 42%")]
    [InlineData("plain text", "plain text")]
    [InlineData("{{sys.cpu}}", "41.67")]
    [InlineData("a {{missing}} b", "a  b")]
    [InlineData("bad {{1 +}}", "bad ⚠")]
    [InlineData("unclosed {{ x", "unclosed {{ x")]
    [InlineData("", "")]
    public void Render(string template, string expected) => Assert.Equal(expected, Template.Render(template, Resolve));

    [Fact]
    public void SingleExpressionKeepsType() => Assert.Equal(30.0, Template.Evaluate("{{value}}", Resolve));

    [Fact]
    public void MixedTemplateIsText() => Assert.Equal("v=30", Template.Evaluate("v={{value}}", Resolve));
}
