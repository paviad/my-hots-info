using MyReplayQuery.Events;

namespace MyReplayQuery.Query;

/// <summary>
/// Recursive-descent parser for the replay query language.
/// </summary>
/// <remarks>
/// <code>
/// program  := (let | find)*
/// let      := 'let' NAME '=' pattern
/// find     := 'find' pattern
/// pattern  := postfix (seqop postfix 'within' DURATION)*          -- left-associative
/// seqop    := 'then' ['all'] | 'not' 'followed' 'by' | 'preceded' 'by' | 'not' 'preceded' 'by'
/// postfix  := primary ('near' '(' value ',' NUMBER ')' | 'count' '>=' NUMBER 'within' DURATION)*
/// primary  := KIND ['(' [filter (',' filter)*] ')'] | NAME | '(' pattern ')'
/// filter   := FIELD ':' value
/// value    := unary ('|' unary)*
/// unary    := '!' unary | ('>=' | '<=' | '>' | '<') NUMBER | NUMBER | STRING | $VAR | NAME
/// </code>
/// </remarks>
public sealed class QueryParser {
    private static readonly HashSet<string> Keywords =
        ["let", "find", "then", "all", "not", "followed", "preceded", "by", "within", "near", "count"];

    private readonly List<Token> _tokens;
    private readonly string _source;
    private readonly Dictionary<string, Pattern> _lets = new(StringComparer.OrdinalIgnoreCase);
    private int _i;

    private QueryParser(string source) {
        _source = source;
        _tokens = Lexer.Tokenize(source);
    }

    public static QueryProgram Parse(string source) => new QueryParser(source).ParseProgram();

    private Token Peek(int ahead = 0) => _tokens[Math.Min(_i + ahead, _tokens.Count - 1)];

    private Token Next() => _tokens[Math.Min(_i++, _tokens.Count - 1)];

    private bool IsWord(string word, int ahead = 0) =>
        Peek(ahead) is { Kind: TokenKind.Ident } t && t.Text.Equals(word, StringComparison.OrdinalIgnoreCase);

    private bool IsPunct(string p) => Peek() is { Kind: TokenKind.Punct } t && t.Text == p;

    private Token Expect(string what, Func<Token, bool> ok) {
        var t = Peek();
        if (!ok(t)) {
            throw new QueryException($"Expected {what}, found {t}", t.Pos);
        }

        return Next();
    }

    private void ExpectWord(string word) => Expect($"'{word}'", t => t.Kind == TokenKind.Ident && t.Text.Equals(word, StringComparison.OrdinalIgnoreCase));

    private void ExpectPunct(string p) => Expect($"'{p}'", t => t.Kind == TokenKind.Punct && t.Text == p);

    private QueryProgram ParseProgram() {
        var statements = new List<Statement>();
        while (Peek().Kind != TokenKind.End) {
            var start = Peek();
            if (IsWord("let")) {
                Next();
                var name = Expect("a name", t => t.Kind == TokenKind.Ident);
                if (Keywords.Contains(name.Text) || EventSchema.Fields.ContainsKey(name.Text.ToLowerInvariant())) {
                    throw new QueryException($"'{name.Text}' is reserved and can't be used as a name", name.Pos);
                }

                ExpectPunct("=");
                var pattern = ParsePattern();
                _lets[name.Text] = pattern;
                statements.Add(new LetStatement(name.Text, pattern, start.Pos));
            }
            else if (IsWord("find")) {
                Next();
                var first = Peek();
                var pattern = ParsePattern();
                statements.Add(new FindStatement(pattern, SourceText(first, _tokens[_i - 1]), start.Pos));
            }
            else {
                throw new QueryException($"Expected 'let' or 'find', found {start}", start.Pos);
            }
        }

        if (!statements.OfType<FindStatement>().Any()) {
            throw new QueryException("The query has no 'find' statement", Peek().Pos);
        }

        return new QueryProgram(statements, _source);
    }

    /// <summary>The source text from one token through another, on one line, for echoing a find.</summary>
    private string SourceText(Token from, Token to) =>
        string.Join(' ', _source[from.Start..to.End].Split((char[])['\r', '\n', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries));

    private Pattern ParsePattern() {
        var left = ParsePostfix();
        while (true) {
            var pos = Peek().Pos;
            SeqOp op;
            var all = false;
            if (IsWord("then")) {
                Next();
                op = SeqOp.Then;
                if (IsWord("all")) {
                    Next();
                    all = true;
                }
            }
            else if (IsWord("not") && IsWord("followed", 1)) {
                Next();
                Next();
                ExpectWord("by");
                op = SeqOp.NotFollowedBy;
            }
            else if (IsWord("not") && IsWord("preceded", 1)) {
                Next();
                Next();
                ExpectWord("by");
                op = SeqOp.NotPrecededBy;
            }
            else if (IsWord("preceded")) {
                Next();
                ExpectWord("by");
                op = SeqOp.PrecededBy;
            }
            else {
                return left;
            }

            var right = ParsePostfix();
            ExpectWord("within");
            var within = Expect("a duration like 10s", t => t.Kind == TokenKind.Duration).Number;
            left = new SeqPattern(left, op, right, within, all, pos);
        }
    }

    private Pattern ParsePostfix() {
        var p = ParsePrimary();
        while (true) {
            if (IsWord("near")) {
                var pos = Next().Pos;
                if (p is not AtomPattern atom) {
                    throw new QueryException("'near' applies to a single event pattern, like cast(...) near(me, 6)", pos);
                }

                ExpectPunct("(");
                var subject = ParseValue();
                ExpectPunct(",");
                var distance = Expect("a distance", t => t.Kind == TokenKind.Number).Number;
                ExpectPunct(")");
                p = atom with { Near = [.. atom.Near, new NearClause(subject, distance, pos)] };
            }
            else if (IsWord("count")) {
                var pos = Next().Pos;
                ExpectPunct(">=");
                var min = Expect("a number", t => t.Kind == TokenKind.Number);
                if (min.Number < 1 || min.Number != Math.Floor(min.Number)) {
                    throw new QueryException("count needs a whole number of at least 1", min.Pos);
                }

                ExpectWord("within");
                var within = Expect("a duration like 10s", t => t.Kind == TokenKind.Duration).Number;
                p = new CountPattern(p, (int)min.Number, within, pos);
            }
            else {
                return p;
            }
        }
    }

    private Pattern ParsePrimary() {
        var t = Peek();
        if (IsPunct("(")) {
            Next();
            var inner = ParsePattern();
            ExpectPunct(")");
            return inner;
        }

        if (t.Kind != TokenKind.Ident || Keywords.Contains(t.Text.ToLowerInvariant())) {
            throw new QueryException($"Expected an event like death(...) or a name from 'let', found {t}", t.Pos);
        }

        Next();
        var kind = t.Text.ToLowerInvariant();
        if (EventSchema.Fields.TryGetValue(kind, out var fields)) {
            var filters = new List<FieldFilter>();
            if (IsPunct("(")) {
                Next();
                while (!IsPunct(")")) {
                    if (filters.Count > 0) {
                        ExpectPunct(",");
                    }

                    var field = Expect("a field name", x => x.Kind == TokenKind.Ident);
                    var fieldName = field.Text.ToLowerInvariant();
                    if (!fields.Contains(fieldName)) {
                        throw new QueryException($"{kind} has no field '{field.Text}' (fields: {string.Join(", ", fields)})", field.Pos);
                    }

                    ExpectPunct(":");
                    filters.Add(new FieldFilter(fieldName, ParseValue(), field.Pos));
                }

                Next();
            }

            return new AtomPattern(kind, filters, [], t.Pos);
        }

        if (!_lets.ContainsKey(t.Text)) {
            throw new QueryException(
                $"Unknown event or name '{t.Text}' (events: {string.Join(", ", EventSchema.Fields.Keys)})", t.Pos);
        }

        return new RefPattern(t.Text, t.Pos);
    }

    private ValueExpr ParseValue() {
        var first = ParseUnaryValue();
        if (!IsPunct("|")) {
            return first;
        }

        var options = new List<ValueExpr> { first };
        while (IsPunct("|")) {
            Next();
            options.Add(ParseUnaryValue());
        }

        return new AltValue(options, first.Pos);
    }

    private ValueExpr ParseUnaryValue() {
        var t = Next();
        switch (t.Kind) {
            case TokenKind.Punct when t.Text == "!":
                return new NotValue(ParseUnaryValue(), t.Pos);
            case TokenKind.Punct when t.Text is ">=" or "<=" or ">" or "<":
                return new CompareValue(t.Text, Expect("a number", x => x.Kind == TokenKind.Number).Number, t.Pos);
            case TokenKind.Number:
                return new NumberValue(t.Number, t.Pos);
            case TokenKind.String:
                return new NameValue(t.Text, t.Pos);
            case TokenKind.Var:
                return new VarValue(t.Text, t.Pos);
            case TokenKind.Ident when t.Text.ToLowerInvariant() is "me" or "ally" or "enemy":
                return new KeywordValue(t.Text.ToLowerInvariant(), t.Pos);
            case TokenKind.Ident:
                return new NameValue(t.Text, t.Pos);
            default:
                throw new QueryException($"Expected a value, found {t}", t.Pos);
        }
    }
}
