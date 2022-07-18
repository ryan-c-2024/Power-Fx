﻿// <autogenerated>
// Use autogenerated to suppress stylecop warnings 

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PowerFx;
using Microsoft.PowerFx.Core;
using Microsoft.PowerFx.Syntax;
using Microsoft.PowerFx.Types;

namespace ExcelConverter
{
    public class Utils
    {
        private String ConvertFormula(String formula)
        {
            return formula;
        }

        private String ExpandRange(String range)
        {
            String ret = "";
            return ret;
        }
        private void ReplaceVar(String definedName)
        {

        }

        public static String CreateVariable(String sheetName, String cellNum, String variableValue)
        {
            String genericName = GenerateGenericName(sheetName, cellNum);
            return genericName + " = " + variableValue;
        }

        public static String CreateVariable(String sheetName, String cellNum, TexlNode node)
        {
            return CreateVariable(sheetName, cellNum, node.ToString());
        }

        // Takes sheet name and cell number and creates a generic default PowerFX variable name for it
        // Eg. Cell B2 on Sheet1 -> Sheet1_B2
        // QUESTION: Should we keep the first letter of the variable uppercase at all times? lowercase? or base it on sheet name casing
        public static String GenerateGenericName(String sheetName, String cellNum)
        {
            if (sheetName == null || sheetName == "" || cellNum == null || cellNum == "") return "";
            String output = sheetName + "_" + cellNum;
            return output;
        }

        // accepts all caps Excel function, converts to PowerFX style function name
        // EG: SUM -> Sum
        public static String AdjustFuncName(String funcName)
        {
            // avoid exceptions so check the length beforehand
            // if name is empty (zero length) check first bc exceptions cause performance hit, confusion
            char[] retn = (funcName.ToLower()).ToCharArray();
            try
            {
                retn[0] = char.ToUpper(retn[0]);
            }
            catch (IndexOutOfRangeException e)
            {
                // function has no name (is invalid)
                Console.WriteLine("ERROR {0}: \"{1}\" passed as function name, returning empty string", e.Message, funcName);
                return "";
            }

            return new string(retn);
        }

        // Processes and converts a function to PowerFX equivalent, taking in a call node
        // DOES NOT CHECK IF OUTPUT IS A VALID POWERFX function
        // WIP - will support nested functions later
        public static String ProcessFunc(CallNode node, ExcelParser.ParsedCell c)
        {
            // add second project as unit test?
            // node = SUM(1, 2+2, 3)
            // ???
            // Sum(1, 2, 3)

            // SUM(C1:C5) -> Sum([C1, C2, C3, C4, C5])
            // SUM(A3:D9) -> Sum([A3, A4, A5, ... D9])

            // for now maybe unroll to Sum(A1, A2, A3, A4) instead of table


            // parser vs lexer
            // parser: toxenizes and notes identifiers and operators after scanning thru
            // then makes tree with identifiers operators and identifiers

            // ways of parsing range:

            // 1. hacky way
            // 
            // 2. right way
            // a1:a3 -> Range(A1, A3) (design new RangeNode object)

            // debug parsing of A1+A2 and figure out how parsing works behind the scenes



            ListNode funcArgs = node.Args;
            var funcName = node.Head.Name;

            String adjustedFuncName = AdjustFuncName(funcName.ToString()) + "("; // convert func name to PowerFX style
            IReadOnlyList<TexlNode> children = funcArgs.ChildNodes;

            TexlNode arg = children[0];
            for (int i = 0; i < children.Count; i++) // iterate over args and append them to output string
            {
                arg = children[i];
                String append = "";

                if (arg.Kind == NodeKind.Call) // if nested function call, we have to recurse
                {
                    append += ProcessFunc((CallNode)arg, c);
                }
                else if (arg.Kind == NodeKind.FirstName)
                {
                    // assume its a cell
                    // change cell name to generic variable and add 
                    // append = GenerateGenericName(c.SheetName, c.CellID);
                    // SUM(1, SUM(5,B13))
                    // -> B13 to string 
                    append = GenerateGenericName(c.SheetName, arg.ToString());
                }
                else
                {
                    append = arg.ToString();
                }

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
        public static String ProcessFunc(String formula, Engine engine) // overload for ProcessFunc that wraps around and takes String input and engine
        {
            ParseResult p = engine.Parse(formula); // parse to get type and information using the passed in engine object
            if (p.Root.Kind != NodeKind.Call) return ""; // if not actually a function, return
            CallNode node = (CallNode)p.Root;
            return ProcessFunc(node, null); // fix me

        }

        public static String ProcessFunc(String formula, ParseResult parse) // overload for ProcessFunc that wraps around and takes String input and parseresult
        {
            if (parse.Root.Kind != NodeKind.Call) return ""; // if not actually a function, return
            CallNode node = (CallNode)parse.Root;
            return ProcessFunc(node, null); // fix me
        }

        public static String ConvertBinaryOp(BinaryOp op)
        {
            return binaryOpMap[op];
        }

        public static String ConvertUnaryOp(UnaryOp op)
        {
            return unaryOpMap[op];
        }

        private static Dictionary<BinaryOp, String> binaryOpMap = new Dictionary<BinaryOp, String>()
            {
                {BinaryOp.Or, "||"},
                {BinaryOp.And, "&&"},
                {BinaryOp.Concat, "CONCAT"},
                {BinaryOp.Add, "+"},
                {BinaryOp.Mul, "*"},
                {BinaryOp.Div, "/"},
                {BinaryOp.Power, "^"},
                {BinaryOp.Equal, "="},
                {BinaryOp.NotEqual, "!="},
                {BinaryOp.Less, "<"},
                {BinaryOp.LessEqual, "<="},
                {BinaryOp.Greater, ">"},
                {BinaryOp.GreaterEqual, ">="},
                {BinaryOp.In, "IN"},
                {BinaryOp.Exactin, "EXACTIN"},
                {BinaryOp.Error, "ERROR"}
            };

        private static Dictionary<UnaryOp, String> unaryOpMap = new Dictionary<UnaryOp, String>()
            {
                {UnaryOp.Not, "!"},
                {UnaryOp.Minus, "-"},
                {UnaryOp.Percent, "%"}
            };
    }
}
