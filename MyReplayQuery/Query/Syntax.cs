using System.Globalization;
using System.Text;

namespace MyReplayQuery.Query;

public readonly record struct SourcePos(int Line, int Column) {
    public override string ToString() => $"{Line}:{Column}";
}

public sealed class QueryException(string message, SourcePos? pos = null) : Exception(message) {
    public SourcePos? Pos { get; } = pos;

    /// <summary>The message plus the offending source line with a caret under the error.</summary>
    public string Render(string source) {
        if (Pos is not { } p) {
            return Message;
        }

        var lines = source.Split('\n');
        var line = p.Line - 1 < lines.Length ? lines[p.Line - 1].TrimEnd('\r') : "";
        return $"{p}: {Message}\n  {line}\n  {new string(' ', Math.Max(0, p.Column - 1))}^";
    }
}

public enum TokenKind { Ident, String, Number, Duration, Var, Punct, End }

public sealed record Token(TokenKind Kind, string Text, SourcePos Pos, double Number = 0) {
    /// <summary>Character offsets of the token in the source, for echoing query text.</summary>
    public int Start { get; init; }
    public int End { get; init; }

    public override string ToString() => Kind == TokenKind.End ? "end of query" : $"'{Text}'";
}

public static class Lexer {
    public static List<Token> Tokenize(string source) {
        var tokens = new List<Token>();
        int i = 0, line = 1, col = 1;

        void Advance(int n = 1) {
            for (var k = 0; k < n && i < source.Length; k++, i++) {
                if (source[i] == '\n') {
                    line++;
                    col = 1;
                }
                else {
                    col++;
                }
            }
        }

        static bool IsIdentStart(char c) => char.IsLetter(c) || c is '_' or '*' or '?';
        static bool IsIdentPart(char c) => char.IsLetterOrDigit(c) || c is '_' or '*' or '?' or '.' or '\'' or '-' or '#';

        while (i < source.Length) {
            var c = source[i];
            var pos = new SourcePos(line, col);
            var tokenStart = i;
            var before = tokens.Count;

            if (char.IsWhiteSpace(c) || c == ';') {
                Advance();
            }
            else if (c == '#') {
                while (i < source.Length && source[i] != '\n') {
                    Advance();
                }
            }
            else if (c == '"') {
                var sb = new StringBuilder();
                Advance();
                while (i < source.Length && source[i] != '"') {
                    if (source[i] == '\\' && i + 1 < source.Length) {
                        Advance();
                    }

                    sb.Append(source[i]);
                    Advance();
                }

                if (i >= source.Length) {
                    throw new QueryException("Unterminated string", pos);
                }

                Advance();
                tokens.Add(new Token(TokenKind.String, sb.ToString(), pos));
            }
            else if (c == '$') {
                var start = i;
                Advance();
                while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_')) {
                    Advance();
                }

                if (i - start < 2) {
                    throw new QueryException("Expected a variable name after '$'", pos);
                }

                tokens.Add(new Token(TokenKind.Var, source[(start + 1)..i], pos));
            }
            else if (char.IsDigit(c)) {
                var start = i;
                while (i < source.Length && (char.IsDigit(source[i]) || source[i] == '.')) {
                    Advance();
                }

                var number = double.Parse(source[start..i], CultureInfo.InvariantCulture);
                var unitStart = i;
                while (i < source.Length && char.IsLetter(source[i])) {
                    Advance();
                }

                var unit = source[unitStart..i];
                if (unit.Length == 0) {
                    tokens.Add(new Token(TokenKind.Number, source[start..i], pos, number));
                }
                else {
                    var seconds = unit switch {
                        "ms" => number / 1000,
                        "s" => number,
                        "m" => number * 60,
                        _ => throw new QueryException($"Unknown duration unit '{unit}' (use ms, s or m)", pos),
                    };
                    tokens.Add(new Token(TokenKind.Duration, source[start..i], pos, seconds));
                }
            }
            else if (IsIdentStart(c)) {
                var start = i;
                while (i < source.Length && IsIdentPart(source[i])) {
                    Advance();
                }

                tokens.Add(new Token(TokenKind.Ident, source[start..i], pos));
            }
            else if (i + 1 < source.Length && source.Substring(i, 2) is ">=" or "<=") {
                tokens.Add(new Token(TokenKind.Punct, source.Substring(i, 2), pos));
                Advance(2);
            }
            else if ("(),:|!<>=".Contains(c)) {
                tokens.Add(new Token(TokenKind.Punct, c.ToString(), pos));
                Advance();
            }
            else {
                throw new QueryException($"Unexpected character '{c}'", pos);
            }

            if (tokens.Count > before) {
                tokens[^1] = tokens[^1] with { Start = tokenStart, End = i };
            }
        }

        tokens.Add(new Token(TokenKind.End, "", new SourcePos(line, col)) { Start = source.Length, End = source.Length });
        return tokens;
    }
}

// ---- Syntax tree ----

public abstract record ValueExpr(SourcePos Pos);

/// <summary>A bare word or quoted string; <c>*</c> and <c>?</c> are wildcards.</summary>
public sealed record NameValue(string Text, SourcePos Pos) : ValueExpr(Pos);

public sealed record NumberValue(double Value, SourcePos Pos) : ValueExpr(Pos);

public sealed record CompareValue(string Op, double Value, SourcePos Pos) : ValueExpr(Pos);

/// <summary><c>me</c>, <c>ally</c> (my teammates, not me) or <c>enemy</c>.</summary>
public sealed record KeywordValue(string Keyword, SourcePos Pos) : ValueExpr(Pos);

public sealed record VarValue(string Name, SourcePos Pos) : ValueExpr(Pos);

public sealed record AltValue(IReadOnlyList<ValueExpr> Options, SourcePos Pos) : ValueExpr(Pos);

public sealed record NotValue(ValueExpr Inner, SourcePos Pos) : ValueExpr(Pos);

public sealed record FieldFilter(string Field, ValueExpr Value, SourcePos Pos);

public sealed record NearClause(ValueExpr Subject, double Distance, SourcePos Pos);

public abstract record Pattern(SourcePos Pos);

public sealed record AtomPattern(string Kind, IReadOnlyList<FieldFilter> Filters, IReadOnlyList<NearClause> Near, SourcePos Pos) : Pattern(Pos);

public sealed record RefPattern(string Name, SourcePos Pos) : Pattern(Pos);

public enum SeqOp { Then, NotFollowedBy, PrecededBy, NotPrecededBy }

public sealed record SeqPattern(Pattern Left, SeqOp Op, Pattern Right, double Within, bool All, SourcePos Pos) : Pattern(Pos);

public sealed record CountPattern(Pattern Inner, int Min, double Within, SourcePos Pos) : Pattern(Pos);

public abstract record Statement(SourcePos Pos);

public sealed record LetStatement(string Name, Pattern Pattern, SourcePos Pos) : Statement(Pos);

public sealed record FindStatement(Pattern Pattern, string Text, SourcePos Pos) : Statement(Pos);

public sealed record QueryProgram(IReadOnlyList<Statement> Statements, string Source);
