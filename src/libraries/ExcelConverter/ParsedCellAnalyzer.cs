﻿// <autogenerated>
// Use autogenerated to suppress stylecop warnings

using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.PowerFx;
using Microsoft.PowerFx.Core;
using Microsoft.PowerFx.Syntax;


namespace ExcelConverter
{
    /*
     * 1. ParsedCellAnalyzer extends TexlFunctionalVisitor (supports Accept returning values)
     * 2. ParsedCellAnalyzer.Analyze(string formula): use engine to parse nodes of formula
     * 3. Analyze(TexlNode node): "general recursion" - Accept new ParsedCellAnalyzer instance in child nodes/args
     * 4. ParsedCellAnalyzer: hold any object structures needed for that specific node, implement any specific functions
     *  e.g., Variable names <-> Identifier map, ranges as list of cells, calling pretty print, etc.
     *  
     *  Flow:
     *      - Parse Excel workbook
     *      - Use PFX Engine to parse cell objects
     *      - Pass parsed cell objects and parsed nodes to ParsedCellAnalyzer.Analyze()
     *      - Accept function called on node
     *      - Visitor pattern and recursion then used to convert to PowerFX equivalents
     */

    public class ParsedCellAnalyzer : TexlFunctionalVisitor<String, Precedence>
    {
        private ExcelParser.ParsedCell analyzedCell;
        private bool isFormula;

        public ParsedCellAnalyzer()
        {
            isFormula = false;
        }

        public ParsedCellAnalyzer(ExcelParser.ParsedCell cell)
        {
            isFormula = false;
            analyzedCell = cell;
        }


        public static string Analyze(string formula, ExcelParser.ParsedCell cell = null)
        {
            var engine = new Engine(new PowerFxConfig());
            var parseResult = engine.Parse(formula);
            return Analyze(parseResult.Root, cell);
        }

        public static string Analyze(TexlNode node, ExcelParser.ParsedCell cell = null)
        {
            var analyzer = new ParsedCellAnalyzer(cell);
            return node.Accept(analyzer, Precedence.None);
        }


        public override String Visit(UnaryOpNode node, Precedence context)
        {
            if (node.Op == UnaryOp.Percent)
            {
                return node.Child.Accept(this, Precedence.None) + Utils.ConvertUnaryOp(node.Op);
            }
            else
            {
                return Utils.ConvertUnaryOp(node.Op) + node.Child.Accept(this, Precedence.None);
            }
        }

        public override String Visit(BoolLitNode node, Precedence context)
        {
            return node.ToString();
        }

        public override String Visit(NumLitNode node, Precedence context)
        {
            return node.ActualNumValue.ToString();
        }

        public override String Visit(FirstNameNode node, Precedence context)
        {
            if (isFormula && analyzedCell != null) // if FirstName within a func call, treat as cell reference and convert to generically named var (temporary feature)
            {
                return Utils.GenerateGenericName(analyzedCell.SheetName, node.Ident.Name.Value);
            }
            else // otherwise, treat it as a new String variable creation and a cell with text in it
            {
                //return "";
                return Utils.CreateVariable(analyzedCell.SheetName, analyzedCell.CellId, "\"" + node.Ident.Name.Value + "\"");
            }
        }

        public override String Visit(StrLitNode node, Precedence context)
        {
            if (isFormula && analyzedCell != null) // if number within a func call, treat it literally (temporary feature)
            {
                return "\"" + node.Value + "\""; // wrap text in quotes so it is treated as literal text and not accidentally as variable
            }
            else
            {
                return Utils.CreateVariable(analyzedCell.SheetName, analyzedCell.CellId, node);
            }
        }

        public override String Visit(BinaryOpNode node, Precedence context)
        {
            isFormula = true;

         

            String opString = Utils.ConvertBinaryOp(node.Op);
            String left = node.Left.Accept(this, Precedence.None);
            String right = node.Right.Accept(this, Precedence.None);

            if (node.Left.ToString()[0] == '(')
            {
                left = "(" + left + ")";
            }
            if (node.Right.ToString()[0] == '(')
            {
                right = "(" + right + ")";
            }

            return left + " " + opString + " " + right;
        }

        public override String Visit(CallNode node, Precedence context)
        {
            isFormula = true; // Mark this as a function so we treat values literally and don't generate generic vraibles
            ListNode funcArgs = node.Args;
            var funcName = node.Head.Name;

            String adjustedFuncName = Utils.AdjustFuncName(funcName.ToString()) + "("; // convert func name to PowerFX style
            IReadOnlyList<TexlNode> children = funcArgs.ChildNodes;

            TexlNode arg = children[0];
            for (int i = 0; i < children.Count; i++) // iterate over args and append them to output string
            {
                arg = children[i];
                String append = "";

                append += arg.Accept(this, Precedence.None);

                if (i == (children.Count - 1)) // if only one argument or this is the last arg, close the parentheses
                {
                    adjustedFuncName += append + ")"; // injects arg.ToString()
                }
                else if (i >= 0 && i < (children.Count - 1)) // if not the last arg, add a comma and space for the next up arg
                {
                    adjustedFuncName += append + ", ";
                }

            }

            return adjustedFuncName;
        }

        public override String Visit(ListNode node, Precedence context)
        {
            return node.ToString();
        }

        public override string Visit(TableNode node, Precedence context)
        {
            throw new NotImplementedException();
        }


        public override String Visit(AsNode node, Precedence context)
        {
            return node.ToString();
        }


        public override String Visit(StrInterpNode node, Precedence context)
        {
            return node.ToString();
        }


        public override String Visit(SelfNode node, Precedence context)
        {
            return node.ToString();
        }

        public override String Visit(RecordNode node, Precedence context)
        {
            return node.ToString();
        }
        public override String Visit(ParentNode node, Precedence context)
        {
            return node.ToString();
        }


        public override String Visit(ErrorNode node, Precedence context)
        {
            return node.ToString();
        }

        public override String Visit(BlankNode node, Precedence context)
        {
            return node.ToString();
        }

        public override String Visit(DottedNameNode node, Precedence context)
        {
            return node.ToString();
        }

        public override String Visit(VariadicOpNode node, Precedence context)
        {
            return node.ToString();
        }
    }
}
