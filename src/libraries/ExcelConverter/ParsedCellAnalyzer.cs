﻿// <autogenerated>
// Use autogenerated to suppress stylecop warnings

using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Wordprocessing;
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
        private bool hasFuncBeenCalled;
        public ParsedCellAnalyzer()
        {
            isFormula = false;
            hasFuncBeenCalled = false;
        }

        public ParsedCellAnalyzer(ExcelParser.ParsedCell cell)
        {
            isFormula = false;
            analyzedCell = cell;
        }

        // Parses formula passed in as a string and then converts it to PFX
        // Wraps around Analyze(TexlNode, ParsedCell)
        public static string Analyze(string formula, ExcelParser.ParsedCell cell = null)
        {
            var engine = new Engine(new PowerFxConfig());
            String updatedFormula = Utils.ReformatRange(formula); // If formula has a range, preprocess and reformat it
            var parseResult = engine.Parse(updatedFormula);
            return Analyze(parseResult.Root, cell);
        }

        // Uses the Visitor design pattern to recursively convert node to PFX, outputting String of converted output
        public static string Analyze(TexlNode node, ExcelParser.ParsedCell cell = null)
        {
            var analyzer = new ParsedCellAnalyzer(cell);
            return node.Accept(analyzer, Precedence.None);
        }

        // Process UnaryOpNode, converting operator enum val to appropriate char and recursively processing operand
        public override String Visit(UnaryOpNode node, Precedence context)
        {
            isFormula = true;
            // If UnaryOp is a percent sign, add it after the operand
            if (node.Op == UnaryOp.Percent)
            {
                return node.Child.Accept(this, Precedence.None) + Utils.ConvertUnaryOp(node.Op);
            }
            // Otherwise, it's a minus sign or similar and we add it beforehand
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
            // if FirstName within a func call, treat as cell reference and convert to generically named var (temporary feature)
            if (isFormula && analyzedCell != null) 
            {
                // #1 thing to do at the get go is to optimize for maintainability
                Match match;

                if (Utils.TryGetRangeMatch(node, out match)) // if we found a preprocessed range, return unfurled range
                {
                   
                    return Utils.ExpandRange(char.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), char.Parse(match.Groups[3].Value), int.Parse(match.Groups[4].Value), analyzedCell.SheetName);
                }
                else 
                {
                    // Generate the generic name (eg. "sheet1_C8") for the given sheetName and cell
                    // If we have a defined name established for this in our map, we use that instead

                    return Utils.GenerateName(analyzedCell.SheetName, node.Ident.Name.Value);
                }
            }
            // otherwise, treat it as a new String variable creation for cell with just text in it
            else
            {
                // this is potentially INCORRECT !!!!!!!!!!!
                return Utils.CreateVariable(analyzedCell.SheetName, analyzedCell.CellId, Utils.QuoteWrap(node.Ident.Name.Value));
            }
        }

        public override String Visit(StrLitNode node, Precedence context)
        {
            // if within a func call, wrap text in quotes so it is treated as literal text and not accidentally as variable
            if (isFormula && analyzedCell != null) 
            {
                return Utils.QuoteWrap(node.Value);
            }
            else
            {
                return Utils.CreateVariable(analyzedCell.SheetName, analyzedCell.CellId, node);
            }
        }

        // Process BinaryOpNode, converting operator's enum val to appropriate char and recursively processing both operands
        public override String Visit(BinaryOpNode node, Precedence context)
        {
            isFormula = true;

            bool withinFunc = hasFuncBeenCalled;

            // recurse and return converted string for left and right operands
            // Then, add the appropriate binary op in between and return
            String opString = Utils.ConvertBinaryOp(node.Op);

            // If this is not within a function definition 
            // and is just a binary op within a cell, we need to modify existing cells

            // if either of the operands is a FirstNameNode (indicating it's either a cell reference or range)
            if (node.Left.Kind == NodeKind.FirstName || node.Right.Kind == NodeKind.FirstName)
            {
                String handledRange = HandleBinaryOpRange(node, opString, withinFunc);
                if (handledRange != null)
                {
                    return handledRange;
                }
            }

            // ranges not working with multiterm statements (eg. 3 + A4:C7 * 5)
            // in this case the right string gets expanded but then the left wont be distributed
            // to all of them but one
            // maybe add in a check/boolean to see if its expanded range then distribute? unsure
            String left = node.Left.Accept(this, Precedence.None);
            String right = node.Right.Accept(this, Precedence.None);
            
            // Add parentheses to correct semantics 
            // Sort of hacky fix for parentheses being dropped during recursion
            return "(" + left + " " + opString + " " + right + ")";
        }

        public override String Visit(CallNode node, Precedence context)
        {
            // Mark this as a formula so we convert cell references, etc properly
            isFormula = true;
            hasFuncBeenCalled = true;
            ListNode funcArgs = node.Args;

            // Swap the function name from Excel func name to PowerFx func name if possible
            // Eg. VLOOKUP -> LOOKUP
            String funcName = Utils.AlignFunctionName(node.Head.Name);

            // First make the function name the lowercased func name to fit PFX style
            StringBuilder adjustedFuncName = new StringBuilder(Utils.AdjustFuncName(funcName.ToString())); 
            adjustedFuncName.Append("("); // then add opening parentheses for the arguments

            IReadOnlyList<TexlNode> children = funcArgs.ChildNodes;

            // Iterate through function arguments and recursively convert them to PFX style
            TexlNode arg = children[0];
            for (int i = 0; i < children.Count; i++) 
            {
                arg = children[i];

                // append represents the new portion (obtained recursively) to which to add to our return string
                String append = arg.Accept(this, Precedence.None);

                // if only one argument or this is the last arg, close the parentheses
                if (i == (children.Count - 1)) 
                {
                    adjustedFuncName.Append(append); 
                    adjustedFuncName.Append(")");
                }
                // if not the last arg, add a comma and space for the next up arg
                else if (i >= 0 && i < (children.Count - 1)) 
                {
                    adjustedFuncName.Append(append);
                    adjustedFuncName.Append(", ");
                }
            }

            return adjustedFuncName.ToString();
        }

        private String HandleBinaryOpRange(BinaryOpNode node, String opString, bool withinFunc)
        {
            // Identify range
            String leftStr = node.Left.ToString(), rightStr = node.Right.ToString();
            Match leftMatch, rightMatch;
          
            Utils.TryGetRangeMatch(node, leftStr, rightStr, out leftMatch, out rightMatch);

            // if there are no ranges whatsoever in the expression, abort early
            // otherwise if either left or right not a range, call the normal recursive Accept function.
            if (!leftMatch.Success && !rightMatch.Success) return null;
            
            if (!leftMatch.Success)
            {
                leftStr = node.Left.Accept(this, Precedence.None);
            }
            if (!rightMatch.Success)
            {
                rightStr = node.Right.Accept(this, Precedence.None);
            }

            // Construct a list of the range, interpolated with whatever binary operator
            List<String> strList = Utils.Interpolate(leftStr, rightStr, opString, leftMatch, rightMatch, analyzedCell.SheetName);

            // If this is a range operation within a new cell and NOT a function
            // NOTE: DOESN'T WORK FOR EG. 5 + (B2:D3 * SUM(1, 2))
            // might have to recurse and then add the result in the very end? unsure
            if (!withinFunc && (leftMatch.Success || rightMatch.Success))
            {
                Utils.ProcessDynamicRange(analyzedCell, strList, leftMatch, rightMatch);

                // return no to terminate processing of this cell
                // this should prevent overwriting of processed dynamic range
                return null; 
            }

            // Iterate through items of the output list and put together the interpolated range
            StringBuilder retn = new StringBuilder("");
            for (int i = 0; i < strList.Count; i++)
            {
                if (i == (strList.Count - 1))
                {
                    retn.Append(strList[i]);
                }
                
                // if not the last item, add a comma and space for the next up arg
                else
                {
                    retn.Append(strList[i]);
                    retn.Append(", ");
                }
            }

            return retn.ToString();
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


// Might need to add error handling in case someone passes in wrong kind of Match object?
public class Range
{
    //public Range()
    //{
    //    // assign placeholder values in generic constructor
    //    startChar = 'a';
    //    endChar = 'a';
    //    startNum = 0;
    //    endNum = 0;
    //}

    public Range(char startChar, int startNum, char endChar, int endNum)
    {
        this.startChar = startChar;
        this.startNum = startNum;
        this.endChar = endChar;
        this.endNum = endNum;
    }

    // Initiate range object from total range match object
    // ie. match for A3_RANGE_C9 should be the input here, not any other kind of match
    public Range(Match match)
    {
        if (match.Success)
        {
            startChar = char.Parse(match.Groups[1].Value);
            endChar = char.Parse(match.Groups[3].Value);
            startNum = int.Parse(match.Groups[2].Value);
            endNum = int.Parse(match.Groups[4].Value);
        }
        else
        {
            startChar = 'a';
            endChar = 'a';
            startNum = 0;
            endNum = 0;
        }
    }

    public void InitFromMatch(Match match)
    {
        if (match.Success)
        {
            startChar = char.Parse(match.Groups[1].Value);
            endChar = char.Parse(match.Groups[3].Value);
            startNum = int.Parse(match.Groups[2].Value);
            endNum = int.Parse(match.Groups[4].Value);
        }
    }

    // Combine the split up member variables into a singular range string
    // Returns eg. "A4:C9"
    public String GetRangeString()
    {
        String cell1 = startChar + startNum.ToString();
        String cell2 = endChar + endNum.ToString();
        return cell1 + ":" + cell2;
    }

    // Get the starting cell of the range
    // Eg. for range object representing "A4:C9", return "A4"
    public String GetStartCellString()
    {
        return startChar + startNum.ToString();
    }

    // Get the ending cell of the range
    // Eg. for range object representing "A4:C9", return "C9"
    public String GetEndCellString()
    {
        return endChar + endNum.ToString();
    }

    public char startChar;
    public char endChar;
    public int startNum;
    public int endNum;
}
