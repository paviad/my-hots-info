using System.Globalization;

namespace CascScraperCore.Schema;

internal class ConstVisitor(ConstResolver resolver) : constBaseVisitor<decimal> {
    public override decimal VisitFile(constParser.FileContext context) {
        var rc = base.Visit(context.expression());
        return rc;
    }

    public override decimal VisitNegex(constParser.NegexContext context) {
        var ex1 = base.Visit(context.expression());
        return -ex1;
    }

    public override decimal VisitFuncex(constParser.FuncexContext context) {
        var ex1 = base.Visit(context.expression());
        switch (context.func().GetText()) {
            case "floor":
                return Math.Floor(ex1);
            case "ceil":
                return Math.Ceiling(ex1);
            case "round":
                return Math.Round(ex1, MidpointRounding.AwayFromZero);
            default:
                throw new Exception("wtf");
        }
    }

    public override decimal VisitFunc2ex(constParser.Func2exContext context) {
        var ex1 = base.Visit(context.expression()[0]);
        var ex2 = base.Visit(context.expression()[1]);
        switch (context.func2().GetText()) {
            case "min":
                return Math.Min(ex1, ex2);
            case "max":
                return Math.Max(ex1, ex2);
            default:
                throw new Exception("wtf");
        }
    }

    public override decimal VisitNumex(constParser.NumexContext context) {
        return decimal.Parse(context.GetText(), NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    public override decimal VisitOpex(constParser.OpexContext context) {
        var ex1 = base.Visit(context.expression()[0]);
        var ex2 = base.Visit(context.expression()[1]);
        var op = context.op().GetText();
        switch (op) {
            case "+":
                return ex1 + ex2;
            case "-":
                return ex1 - ex2;
            case "*":
                return ex1 * ex2;
            case "/":
                return ex1 / ex2;
            default:
                throw new Exception("wtf");
        }
    }

    public override decimal VisitVarex(constParser.VarexContext context) {
        return resolver.Resolve(context.GetText());
    }
}
