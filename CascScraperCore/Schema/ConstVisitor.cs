using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Antlr4.Runtime;
using CascScraperCore;

namespace CascScraper.Schema
{
    internal class ConstVisitor : constBaseVisitor<decimal>
    {
        private readonly XmlDocument _heroCatalog;
        private readonly Dictionary<string, XmlDocument> _referenceCatalog;

        public ConstVisitor(
            XmlDocument heroCatalog,
            Dictionary<string, XmlDocument> referenceCatalog)
        {
            _heroCatalog = heroCatalog;
            _referenceCatalog = referenceCatalog;
        }

        public override decimal VisitFile(constParser.FileContext context)
        {
            var rc = base.Visit(context.expression());
            return rc;
        }

        public override decimal VisitNegex(constParser.NegexContext context)
        {
            var ex1 = base.Visit(context.expression());
            return -ex1;
        }

        public override decimal VisitNumex(constParser.NumexContext context)
        {
            return decimal.Parse(context.GetText());
        }

        public override decimal VisitOpex(constParser.OpexContext context)
        {
            var ex1 = base.Visit(context.expression()[0]);
            var ex2 = base.Visit(context.expression()[1]);
            var op = context.op().GetText();
            switch (op)
            {
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

        public override decimal VisitVarex(constParser.VarexContext context)
        {
            var constName = context.GetText();
            var rc = GetConst(constName);
            return rc;
        }

        private decimal GetConst(string value)
        {
            var docs = new[] { _heroCatalog }.Concat(_referenceCatalog.Values);
            foreach (var doc in docs)
            {
                var constNodes = doc.SelectNodes($"//const[@id='{value}']");
                if (constNodes.Count == 0)
                {
                    Console.WriteLine("wtf");
                    return 0;
                }

                if (constNodes.Count > 1)
                {
                    Console.WriteLine("wtf");
                    return 0;
                }

                var constNode = constNodes[0];
                var evalAsExpression = constNode.Attributes["evaluateAsExpression"]?.Value == "1";
                var val = constNode.Attributes["value"];
                return evalAsExpression
                    ? ParseConst(val.Value)
                    : decimal.Parse(val.Value);
            }

            return 0;
        }

        private decimal ParseConst(string gref)
        {
            try
            {
                var sr = new StringReader(gref);
                var ais = new AntlrInputStream(sr);

                var lexer = new constLexer(ais);
                var tokenStream = new CommonTokenStream(lexer);
                var parser = new constParser(tokenStream);

                var context = parser.file();
                var visitor = new ConstVisitor(_heroCatalog, _referenceCatalog);

                return visitor.Visit(context);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return 0;
            }
        }
    }
}
