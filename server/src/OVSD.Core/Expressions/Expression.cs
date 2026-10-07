using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace OVSD.Core.Expressions;

public sealed class ExpressionException(string message) : Exception(message);

/// <summary>
/// Small expression language used in conditions, templates and state expressions.
/// <code>
///   obs.scene == 'Main' &amp;&amp; !audio.muted
///   round(sys.cpu) + '%'
///   [mqtt.home/temp] > 25 ? 'hot' : 'ok'
///   default(user.counter, 0) + 1
/// </code>
/// Identifiers may contain dots; anything else goes in brackets. Unknown variables evaluate to null.
/// </summary>
public static class Expression
{
    private static readonly ConcurrentDictionary<string, Node> Cache = new();
    private const int MaxCacheEntries = 10_000;

    public static object? Evaluate(string source, Func<string, object?> resolve) =>
        Parse(source).Eval(resolve);

    public static Node Parse(string source)
    {
        if (Cache.TryGetValue(source, out var cached)) return cached;
        var node = new Parser(source).ParseAll();
        if (Cache.Count >= MaxCacheEntries) Cache.Clear();
        Cache[source] = node;
        return node;
    }

    // ---------------------------------------------------------------- AST

    public abstract class Node
    {
        public abstract object? Eval(Func<string, object?> resolve);
    }

    private sealed class Literal(object? value) : Node
    {
        public override object? Eval(Func<string, object?> resolve) => value;
    }

    private sealed class Variable(string name) : Node
    {
        public override object? Eval(Func<string, object?> resolve) => Values.Normalize(resolve(name));
    }

    private sealed class Unary(string op, Node operand) : Node
    {
        public override object? Eval(Func<string, object?> resolve)
        {
            var v = operand.Eval(resolve);
            return op switch
            {
                "!" => !Values.IsTruthy(v),
                "-" => -Values.ToNumber(v),
                _ => throw new ExpressionException($"Unknown operator {op}"),
            };
        }
    }

    private sealed class Ternary(Node condition, Node whenTrue, Node whenFalse) : Node
    {
        public override object? Eval(Func<string, object?> resolve) =>
            Values.IsTruthy(condition.Eval(resolve)) ? whenTrue.Eval(resolve) : whenFalse.Eval(resolve);
    }

    private sealed class Binary(string op, Node left, Node right) : Node
    {
        public override object? Eval(Func<string, object?> resolve)
        {
            var l = left.Eval(resolve);
            switch (op)
            {
                case "&&": return Values.IsTruthy(l) && Values.IsTruthy(right.Eval(resolve));
                case "||": return Values.IsTruthy(l) || Values.IsTruthy(right.Eval(resolve));
                case "??": return l is null or "" ? right.Eval(resolve) : l;
            }

            var r = right.Eval(resolve);
            return op switch
            {
                "==" => Values.AreEqual(l, r),
                "!=" => !Values.AreEqual(l, r),
                "<" => Values.Compare(l, r) < 0,
                "<=" => Values.Compare(l, r) <= 0,
                ">" => Values.Compare(l, r) > 0,
                ">=" => Values.Compare(l, r) >= 0,
                "+" => Add(l, r),
                "-" => Values.ToNumber(l) - Values.ToNumber(r),
                "*" => Values.ToNumber(l) * Values.ToNumber(r),
                "/" => Values.ToNumber(r) is 0 ? null : Values.ToNumber(l) / Values.ToNumber(r),
                "%" => Values.ToNumber(r) is 0 ? null : Values.ToNumber(l) % Values.ToNumber(r),
                _ => throw new ExpressionException($"Unknown operator {op}"),
            };
        }

        private static object? Add(object? l, object? r)
        {
            if (l is not string && r is not string) return (Values.ToNumber(l) ?? 0) + (Values.ToNumber(r) ?? 0);
            if (Values.ToNumber(l) is { } x && Values.ToNumber(r) is { } y) return x + y;
            return Values.ToText(l) + Values.ToText(r);
        }
    }

    private sealed class Call(string name, IReadOnlyList<Node> args) : Node
    {
        public override object? Eval(Func<string, object?> resolve)
        {
            if (name == "if")
            {
                Functions.Arity(name, args.Count, 3, 3);
                return Values.IsTruthy(args[0].Eval(resolve)) ? args[1].Eval(resolve) : args[2].Eval(resolve);
            }
            var values = new object?[args.Count];
            for (var i = 0; i < args.Count; i++) values[i] = args[i].Eval(resolve);
            return Functions.Invoke(name, values);
        }
    }

    // ---------------------------------------------------------------- Parser

    private sealed class Parser(string src)
    {
        private int _pos;

        public Node ParseAll()
        {
            var node = ParseTernary();
            SkipWs();
            if (_pos < src.Length) throw Error($"Unexpected '{src[_pos]}'");
            return node;
        }

        private Node ParseTernary()
        {
            var condition = ParseBinary(0);
            if (!Match("?")) return condition;
            var whenTrue = ParseTernary();
            Expect(":");
            var whenFalse = ParseTernary();
            return new Ternary(condition, whenTrue, whenFalse);
        }

        private static readonly string[][] Levels =
        [
            ["??"],
            ["||", "or"],
            ["&&", "and"],
            ["==", "!="],
            ["<=", ">=", "<", ">"],
            ["+", "-"],
            ["*", "/", "%"],
        ];

        private Node ParseBinary(int level)
        {
            if (level == Levels.Length) return ParseUnary();
            var left = ParseBinary(level + 1);
            while (true)
            {
                var op = Levels[level].FirstOrDefault(MatchOperator);
                if (op is null) return left;
                var normalized = op switch { "or" => "||", "and" => "&&", _ => op };
                left = new Binary(normalized, left, ParseBinary(level + 1));
            }
        }

        private Node ParseUnary()
        {
            if (Match("!") || MatchWord("not")) return new Unary("!", ParseUnary());
            if (Match("-")) return new Unary("-", ParseUnary());
            return ParsePrimary();
        }

        private Node ParsePrimary()
        {
            SkipWs();
            if (_pos >= src.Length) throw Error("Unexpected end of expression");
            var c = src[_pos];

            if (c == '(')
            {
                _pos++;
                var inner = ParseTernary();
                Expect(")");
                return inner;
            }
            if (c is '\'' or '"') return new Literal(ReadString(c));
            if (char.IsDigit(c) || (c == '.' && _pos + 1 < src.Length && char.IsDigit(src[_pos + 1])))
                return new Literal(ReadNumber());
            if (c == '[')
            {
                var end = src.IndexOf(']', _pos + 1);
                if (end < 0) throw Error("Missing ']'");
                var name = src[(_pos + 1)..end].Trim();
                _pos = end + 1;
                return new Variable(name);
            }
            if (IsIdentStart(c))
            {
                var ident = ReadIdentifier();
                switch (ident)
                {
                    case "true": return new Literal(true);
                    case "false": return new Literal(false);
                    case "null": return new Literal(null);
                }
                SkipWs();
                if (_pos < src.Length && src[_pos] == '(' && !ident.Contains('.'))
                {
                    _pos++;
                    var args = new List<Node>();
                    if (!Match(")"))
                    {
                        do args.Add(ParseTernary()); while (Match(","));
                        Expect(")");
                    }
                    if (!Functions.Exists(ident)) throw Error($"Unknown function '{ident}'");
                    return new Call(ident, args);
                }
                return new Variable(ident);
            }
            throw Error($"Unexpected '{c}'");
        }

        private string ReadString(char quote)
        {
            _pos++;
            var sb = new StringBuilder();
            while (_pos < src.Length && src[_pos] != quote)
            {
                if (src[_pos] == '\\' && _pos + 1 < src.Length)
                {
                    _pos++;
                    sb.Append(src[_pos] switch { 'n' => '\n', 't' => '\t', var other => other });
                }
                else sb.Append(src[_pos]);
                _pos++;
            }
            if (_pos >= src.Length) throw Error("Unterminated string");
            _pos++;
            return sb.ToString();
        }

        private double ReadNumber()
        {
            var start = _pos;
            while (_pos < src.Length && (char.IsDigit(src[_pos]) || src[_pos] == '.')) _pos++;
            return double.Parse(src.AsSpan(start, _pos - start), CultureInfo.InvariantCulture);
        }

        private string ReadIdentifier()
        {
            var start = _pos;
            while (_pos < src.Length && (char.IsLetterOrDigit(src[_pos]) || src[_pos] is '_' or '.')) _pos++;
            return src[start.._pos].TrimEnd('.');
        }

        private static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_';

        private void SkipWs()
        {
            while (_pos < src.Length && char.IsWhiteSpace(src[_pos])) _pos++;
        }

        private bool MatchOperator(string op) => char.IsLetter(op[0]) ? MatchWord(op) : Match(op);

        private bool Match(string token)
        {
            SkipWs();
            if (string.CompareOrdinal(src, _pos, token, 0, token.Length) != 0) return false;
            // Don't read "=" of "==" as part of "<" etc., and don't treat "!=" as "!".
            if (token is "!" or "<" or ">" && _pos + 1 < src.Length && src[_pos + 1] == '=') return false;
            if (token is "?" && _pos + 1 < src.Length && src[_pos + 1] == '?') return false;
            _pos += token.Length;
            return true;
        }

        private bool MatchWord(string word)
        {
            SkipWs();
            var end = _pos + word.Length;
            if (end > src.Length || string.CompareOrdinal(src, _pos, word, 0, word.Length) != 0) return false;
            if (end < src.Length && (char.IsLetterOrDigit(src[end]) || src[end] is '_' or '.')) return false;
            _pos = end;
            return true;
        }

        private void Expect(string token)
        {
            if (!Match(token)) throw Error($"Expected '{token}'");
        }

        private ExpressionException Error(string message) =>
            new($"{message} at position {_pos} in \"{src}\"");
    }
}
