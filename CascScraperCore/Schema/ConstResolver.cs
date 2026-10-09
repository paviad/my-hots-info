using System.Globalization;
using System.Xml;
using Antlr4.Runtime;

namespace CascScraperCore.Schema;

/// <summary>
/// Resolves a catalog value to a number: a literal, a <c>$const</c> reference (optionally negated, e.g.
/// <c>-$XalatathVoidMinionLifeDecayRate</c>), or a const whose value is a prefix formula
/// (<c>evaluateAsExpression="1"</c>, parsed with const.g4). Consts are looked up in the hero catalog first, then in
/// the reference catalogs.
/// </summary>
internal class ConstResolver(XmlDocument heroCatalog, Dictionary<string, XmlDocument> referenceCatalog) {
    public decimal Resolve(string value) {
        value = value.Trim();
        if (decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) {
            return number;
        }

        if (value.StartsWith('-')) {
            return -Resolve(value[1..]);
        }

        if (FindConst(value) is not { } constNode) {
            Console.WriteLine($"Can't find const {value}, using 0");
            return 0;
        }

        var constValue = constNode.RequiredAttr("value");
        return constNode.Attr("evaluateAsExpression") == "1"
            ? Evaluate(constValue)
            : Resolve(constValue);
    }

    private XmlNode? FindConst(string id) {
        foreach (var doc in new[] { heroCatalog }.Concat(referenceCatalog.Values)) {
            var constNodes = doc.SelectNodes($"//const[@id='{id}']")!;
            if (constNodes.Count > 1) {
                Console.WriteLine($"Const {id} is defined {constNodes.Count} times, using the first");
            }

            if (constNodes.Count > 0) {
                return constNodes[0];
            }
        }

        return null;
    }

    private decimal Evaluate(string formula) {
        try {
            var lexer = new constLexer(new AntlrInputStream(formula));
            var parser = new constParser(new CommonTokenStream(lexer));
            SyntaxErrorListener.Throwing.Attach(lexer, parser);
            return new ConstVisitor(this).Visit(parser.file());
        }
        catch (Exception e) {
            Console.WriteLine($"Can't evaluate const formula {formula}, using 0: {e.Message}");
            return 0;
        }
    }
}

/// <summary>
/// Reports ANTLR syntax errors. Const formulas are strict (<see cref="Throwing"/>); talent refs are not, because
/// Blizzard ships malformed refs (unbalanced parentheses, stray operators) that the game evaluates anyway and that
/// the parser's default error recovery gets right, so those are only logged (<see cref="Logging"/>).
/// </summary>
internal class SyntaxErrorListener(bool throwOnError) : IAntlrErrorListener<int>, IAntlrErrorListener<IToken> {
    public static readonly SyntaxErrorListener Throwing = new(true);
    public static readonly SyntaxErrorListener Logging = new(false);

    public void Attach(Lexer lexer, Parser parser) {
        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(this);
        parser.RemoveErrorListeners();
        parser.AddErrorListener(this);
    }

    public void SyntaxError(IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg,
        RecognitionException e) =>
        Report(recognizer, charPositionInLine, msg);

    public void SyntaxError(IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine,
        string msg, RecognitionException e) =>
        Report(recognizer, charPositionInLine, msg);

    private void Report(IRecognizer recognizer, int charPositionInLine, string msg) {
        if (throwOnError) {
            throw new FormatException($"col {charPositionInLine}: {msg}");
        }

        var input = recognizer.InputStream switch {
            ICharStream chars => chars.ToString(),
            ITokenStream tokens => tokens.TokenSource.InputStream.ToString(),
            _ => "?",
        };
        Console.WriteLine($"Syntax error in {input} at col {charPositionInLine} (recovered): {msg}");
    }
}
