using System.Xml;
using Antlr4.Runtime;

namespace CascScraperCore.Schema;

internal class ExprVisitor : arithmeticBaseVisitor<ResultType> {
    private readonly XmlDocument _heroCatalog;
    private readonly Dictionary<string, XmlDocument> _referenceCatalog;

    public ExprVisitor(XmlDocument heroCatalog, Dictionary<string, XmlDocument> referenceCatalog) {
        _heroCatalog = heroCatalog;
        _referenceCatalog = referenceCatalog;
    }

    public override ResultType VisitAtomex(arithmeticParser.AtomexContext context) {
        var arg = base.Visit(context.atom());
        var rc = context.MINUS().Length != 0
            ? -arg.Value
            : arg.Value;
        return new ResultType(rc);
    }

    public override ResultType VisitFile(arithmeticParser.FileContext context) {
        var rc = base.Visit(context.expression());
        return rc;
    }

    public override ResultType VisitObj(arithmeticParser.ObjContext context) {
        if (context.GetText() == "Behavior,CrusaderIndestructibleCooldown,Duration") {
            // A hack to avoid merging all hero catalogs.
            // This is defined in Crusader (Johanna) but is referenced in Fenix as well.
            return new ResultType(120m);
        }

        var cobj = base.Visit(context.objspec()).Node;
        var vars = context.indexed_variable();
        var numVars = vars.Length;
        if (vars[numVars - 1].GetText() == "") {
            // Hack to fix trailing dot
            numVars--;
        }

        for (var i = 0; i < numVars - 1; i++) {
            var theVar = vars[i];
            cobj = GetMember(theVar, cobj);
        }

        var finalVar = vars[numVars - 1];
        var rc = GetValue(finalVar, cobj);

        return new ResultType(rc);
    }

    public override ResultType VisitObjspec(arithmeticParser.ObjspecContext context) {
        var ctype = context.variable()[0].GetText();
        var cid = context.variable()[1].GetText();
        var obj = FindObject(ctype, cid);
        return new ResultType(obj);
    }

    public override ResultType VisitOpex(arithmeticParser.OpexContext context) {
        var ex1 = base.Visit(context.expression()[0]);
        var ex2 = base.Visit(context.expression()[1]);
        var op = context.op().GetText();
        decimal rc;
        switch (op) {
            case "+":
                rc = ex1.Value + ex2.Value;
                break;
            case "-":
                rc = ex1.Value - ex2.Value;
                break;
            case "*":
                rc = ex1.Value * ex2.Value;
                break;
            case "/":
                rc = ex1.Value / ex2.Value;
                break;
            default:
                rc = 0;
                break;
        }

        return new ResultType(rc);
    }

    public override ResultType VisitParenex(arithmeticParser.ParenexContext context) {
        var arg = base.Visit(context.expression());
        var rc = context.MINUS().Length != 0
            ? -arg.Value
            : arg.Value;
        return new ResultType(rc);
    }

    public override ResultType VisitScientific(arithmeticParser.ScientificContext context) {
        return new ResultType(GetDecimal(context.GetText()));
    }

    private static XmlNode GetMember2(arithmeticParser.Indexed_variableContext theVar, XmlNode cobj) {
        var name = theVar.GetText();
        if (theVar.index() != null) {
            // indexed
            var index = theVar.index().GetText();
            var array = theVar.variable().GetText();
            XmlNode cobj2 = null;
            try {
                cobj2 = cobj.SelectSingleNode($"./{array}[@index='{index}']");
            }
            catch {
                // ignored
            }

            if (cobj2 != null) {
                cobj = cobj2;
            }
            else if (int.TryParse(index, out var indexNum)) {
                try {
                    var nodeArray = cobj.SelectNodes($"./{array}");
                    if (indexNum >= nodeArray.Count) {
                        indexNum = nodeArray.Count - 1;
                    }

                    cobj = nodeArray[indexNum];
                }
                catch (Exception e) {
                    Console.WriteLine(e);
                    return null;
                }
            }
            else {
                return null;
            }
        }
        else {
            try {
                cobj = cobj.SelectSingleNode($"./{name}");
            }
            catch (Exception e) {
                Console.WriteLine(e);
                return null;
            }
        }

        return cobj;
    }

    private XmlNode FindObject(string ctype, string cid) {
        var f1 = FindObjectCatalog(_heroCatalog, ctype, cid);
        XmlNode f2 = null;
        if (!_referenceCatalog.ContainsKey(ctype)) {
            Console.WriteLine($"New ctype {ctype}");
        }
        else {
            f2 = FindObjectCatalog(_referenceCatalog[ctype], ctype, cid);
        }

        var rc = f1 ?? f2;

        return rc;
    }

    private static XmlNode FindObjectCatalog(XmlDocument doc, string ctype, string cid) {
        var candidates = doc.DocumentElement.SelectNodes($"/Catalog/*[@id=\"{cid}\"]");
        if (candidates.Count == 1) {
            return candidates[0];
        }

        if (candidates.Count > 1) {
            return candidates
                .Cast<XmlNode>()
                .FirstOrDefault(x => x.Name.StartsWith($"C{ctype}"));
        }

        return null;
    }

    private decimal GetConst(string value) {
        var docs = new[] { _heroCatalog }.Concat(_referenceCatalog.Values);
        foreach (var doc in docs) {
            var constNodes = doc.SelectNodes($"//const[@id='{value}']");
            if (constNodes.Count == 0) {
                Console.WriteLine("wtf");
                return 0;
            }

            if (constNodes.Count > 1) {
                Console.WriteLine("wtf");
                return 0;
            }

            var constNode = constNodes[0];
            var evalAsExpression = constNode.Attributes["evaluateAsExpression"]?.Value == "1";
            var val = constNode.Attributes["value"].Value;
            return evalAsExpression
                ? ParseConst(val)
                : decimal.Parse(val);
        }

        return 0;
    }

    private decimal GetDecimal(string value) {
        return decimal.TryParse(value, out var rc)
            ? rc
            : GetConst(value);
    }

    private XmlNode GetMember(arithmeticParser.Indexed_variableContext theVar, XmlNode cobj) {
        var rc = GetMember2(theVar, cobj);
        while (rc == null) {
            cobj = GetParent(cobj);
            if (cobj == null) {
                Console.WriteLine("Can't find object");
                return null;
            }

            rc = GetMember2(theVar, cobj);
        }

        return rc;
    }

    private XmlNode GetParent(XmlNode cobj) {
        var ctype = ToCtype(cobj.Name);
        var cid = cobj.Attributes["parent"]?.Value;
        return cid == null
            ? null
            : FindObject(ctype, cid);
    }

    private decimal GetValue(arithmeticParser.Indexed_variableContext theVar, XmlNode cobj) {
        var rc = GetValue2(theVar, cobj);
        while (rc == null) {
            cobj = GetParent(cobj);
            if (cobj == null) {
                Console.WriteLine("Can't find object, returning 1");
                return 1;
            }

            rc = GetValue2(theVar, cobj);
        }

        return rc.Value;
    }

    private decimal? GetValue2(arithmeticParser.Indexed_variableContext theVar, XmlNode cobj) {
        var name = theVar.GetText();
        if (theVar.index() != null) {
            // indexed
            var index = theVar.index().GetText();
            var array = theVar.variable().GetText();
            XmlNode cobj2 = null;
            try {
                cobj2 = cobj.SelectSingleNode($"./{array}[@index='{index}']");
            }
            catch {
                // ignored
            }

            if (cobj2 != null) {
                cobj = cobj2;
            }
            else if (int.TryParse(index, out var indexNum)) {
                try {
                    var nodeArray = cobj.SelectNodes($"./{array}");
                    if (indexNum >= nodeArray.Count) {
                        indexNum = nodeArray.Count - 1;
                    }

                    cobj = nodeArray[indexNum];
                }
                catch (Exception exception) {
                    Console.WriteLine(exception);
                    return null;
                }
            }
            else {
                return null;
            }

            try {
                if (cobj.Attributes["value"] == null) {
                    cobj = cobj.SelectSingleNode("./Value");
                }
            }
            catch (Exception e) {
                Console.WriteLine(e);
                return null;
            }

            try {
                return GetDecimal(cobj.Attributes["value"].Value);
            }
            catch (Exception e) {
                Console.WriteLine(e);
                return null;
            }
        }

        if (cobj.Attributes[name] != null) {
            return GetDecimal(cobj.Attributes[name].Value);
        }

        try {
            var cobj2 = cobj.SelectSingleNode($"./{name}");

            if (cobj2 == null) {
                return null;
            }

            cobj = cobj2;
        }
        catch (Exception e) {
            Console.WriteLine(e);
            return null;
        }

        return GetDecimal(cobj.Attributes["value"].Value);
    }

    private decimal ParseConst(string gref) {
        try {
            var reader = new StringReader(gref);
            var antlrInputStream = new AntlrInputStream(reader);

            var lexer = new constLexer(antlrInputStream);
            var tokenStream = new CommonTokenStream(lexer);
            var parser = new constParser(tokenStream);

            var context = parser.file();
            var visitor = new ConstVisitor(_heroCatalog, _referenceCatalog);

            var rc = visitor.Visit(context);
            return rc;
        }
        catch (Exception e) {
            Console.WriteLine(e);
            return 0;
        }
    }

    private static string ToCtype(string cobjName) {
        var idx = cobjName.IndexOfAny([.. "ABCDEFGHIJKLMNOPQRSTUVWXYZ"], 2);
        return idx == -1 ? cobjName[1..] : cobjName[1..idx];
    }
}
