﻿// <autogenerated>
// Use autogenerated to suppress stylecop warnings 

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Office2016.Drawing.Command;
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

        // Expands a range, outputting a string which is a comma-separated list of cells
        // Eg. ExpandRange('A', 3, 'B', 5) -> "A3, A4, A5, B3, B4, B5"
        // what happens when excel gets to ~30 columns? it might break        
        public static String ExpandRange(char rangeStartChar, int rangeStartNum, char rangeEndChar, int rangeEndNum, String sheetName)
        {
            StringBuilder str = new StringBuilder("");
 
            // Iterate through the whole range (eg. A4 to C7) 
            // Expand the whole range with a nested for loop, appending to str
            for (int i = (int)rangeStartChar; i <= (int)rangeEndChar; i++)
            {
                for (int j = rangeStartNum; j <= rangeEndNum; j++)
                {
                    // If this is the last cell to expand, skip comma separation
                    if (i == (int)rangeEndChar && j == rangeEndNum)
                    {
                        String newVar = GenerateName(sheetName, $"{(char)i}{j}");
                        str.Append(newVar);
                    }
                    else
                    {
                        String newVar = GenerateName(sheetName, $"{(char)i}{j}, ");
                        str.Append(newVar);
                    }
                }
            }
         
            return str.ToString();
        }

        // Given a binary op node containing a range, expand range and interpolate the opString and other node
        // Returns a List of strings
        // example output: ["5 + Sheet1_C3", "5 + Sheet1_C4", ... "5 + Sheet1_D8", "5 + Sheet1_D9"]
        public static List<String> Interpolate(String leftStr, String rightStr, String opString, Match leftMatch, Match rightMatch, String sheetName)
        { 
            var strList = new List<String>();

            // If both left and right are ranges, interpolate them together
            if (leftMatch.Success && rightMatch.Success)
            {
                // What letter and number the left range starts with and ends with
                // eg. for A3:B6 startChar is A, startNum is 3 and endNum is B, endNum is 6

                Range leftRange = new Range(leftMatch);
                Range rightRange = new Range(rightMatch);  

                // if there is a size mismatch between the two ranges we need to throw an exception
                int leftRangeSize = (leftRange.endChar - leftRange.startChar + 1) * (leftRange.endNum - leftRange.startNum + 1);
                int rightRangeSize = (rightRange.endChar - rightRange.startChar + 1) * (rightRange.endNum - rightRange.startNum + 1);
                if (leftRangeSize != rightRangeSize)
                {
                    throw (new Exception("ERROR: MISMATCH BETWEEN TWO RANGE SIZES IN BINARY OPERATOR"));
                }

                // Iterate through the letters (A-Z) and numbers of the range
                // i, j are char, num of each of left range's cells ; k, j are char, num of right's
                int k = (int)rightRange.startChar;
                int l = (int)rightRange.startNum;
                for (int i = (int)leftRange.startChar; i <= (int)leftRange.endChar; i++)
                {
                    l = (int)rightRange.startNum;
                    for (int j = leftRange.startNum; j <= leftRange.endNum; j++)
                    {
                        // Have two matching cells from the expanded range, then
                        // make each cell pair into variable names for PowerFX compatibility
                        String leftVar = Utils.GenerateName(sheetName, $"{(char)i}{j}");
                        String rightVar = Utils.GenerateName(sheetName, $"{(char)k}{l}");

                        // Interpolate with the original operator used for 
                        strList.Add($"{leftVar} {opString} {rightVar}");
                        l++;
                    }

                    k++;
                }

            }
            else if (leftMatch.Success) // if left is a range, right isn't
            {
                Range range = new Range(leftMatch);
             
                // Iterate through the whole range (eg. A4 to C7) 
                // Expand the whole range with a nested for loop, appending to str
                for (int i = (int)range.startChar; i <= (int)range.endChar; i++)
                {
                    for (int j = range.startNum; j <= range.endNum; j++)
                    {
                        String newVar = Utils.GenerateName(sheetName, $"{(char)i}{j}");
                        strList.Add($"{newVar} {opString} {rightStr}");
                    }
                }
            }
            else if (rightMatch.Success) // right is a range but left isn't
            {
                Range range = new Range(rightMatch);

                // Iterate through the whole range (eg. A4 to C7) 
                // Expand the whole range with a nested for loop, appending to str
                for (int i = (int)range.startChar; i <= (int)range.endChar; i++)
                {
                    for (int j = range.startNum; j <= range.endNum; j++)
                    {
                        String newVar = Utils.GenerateName(sheetName, $"{(char)i}{j}");
                        strList.Add($"{leftStr} {opString} {newVar}");
                    }
                }
            }

            return strList;
        }

        public static void ProcessDynamicRange(ExcelParser.ParsedCell currCell, List<String> interpolatedList, Match leftMatch, Match rightMatch)
        {
            Range rg = leftMatch.Success ? new Range(leftMatch) : new Range(rightMatch);

            Match matchCurrCell;
            Utils.TryGetCellMatch(currCell.CellId, out matchCurrCell);

            // Iterate through the letters (A-Z) and numbers of the range
            // i, j are char, num of each of left range's cells ; k, l are char, num of right's
            char currCellChar = char.Parse(matchCurrCell.Groups[1].Value);
            int currCellNum = int.Parse(matchCurrCell.Groups[2].Value);
            int k = (int)currCellChar;
            int l = (int)currCellNum;
            for (int i = (int)rg.startChar; i <= (int)rg.endChar; i++)
            {
                l = (int)currCellNum;
                for (int j = rg.startNum; j <= rg.endNum; j++)
                {
                    String currCellId = "" + (char)k + l;

                    int currIteration = (i - rg.startChar) * (rg.endNum - rg.startNum + 1) + (j - rg.startNum + 1);
                    String newCellFormula = interpolatedList[currIteration - 1]; // index out of range
                    Converter.outputList.Add(Utils.CreateVariable(currCell.SheetName, currCellId, newCellFormula));
                    Converter.processedSet.Add(currCellId);

                    l++;
                }
                k++;
            }
        }

        private void ReplaceVar(String definedName)
        {

        }

        public static String CreateVariable(String sheetName, String cellNum, String variableValue)
        {
            String varName = GenerateName(sheetName, cellNum);
            return varName + " = " + variableValue;
        }

        public static String CreateVariable(String sheetName, String cellNum, TexlNode node)
        {
            return CreateVariable(sheetName, cellNum, node.ToString());
        }

        // Generate the generic PowerFX default varuabke name (eg. "sheet1_C8") for a given sheetName and cell
        // Eg. Cell B2 on Sheet1 -> Sheet1_B2
        // If we have a defined name established for this in our map, we use that instead
        public static String GenerateName(String sheetName, String cellNum)
        {
            if (sheetName == null || sheetName == "" || cellNum == null || cellNum == "") return "";

            // If a defined name "eg. Variable1" was passed in for cellNum, return it immediately
            if (Converter.definedNamesMap.ContainsValue(cellNum)) return cellNum;


            String genericName = RemoveAllWhitespace(sheetName) + "_" + cellNum;
            String definedName = null;

            // automatically corrects all references to a defined name to the actual defined name
            // Not sure if this is the behavior we are going for
            // eg. Sheet1_C8 is assigned the variable name Variable1. If Sheet1_C8 is referenced in a formula,
            // we will output Variable1 instead
            if (Converter.definedNamesMap.TryGetValue(genericName, out definedName))
            {
                return definedName;
            }
            else
            {
                return genericName;
            }
        }

        public static String RemoveAllWhitespace(String text)
        {
            return Regex.Replace(text, @"\s+", String.Empty);
        }

        // accepts all caps Excel function, converts to PowerFX style function name
        // EG: SUM -> Sum
        public static String AdjustFuncName(String funcName)
        {
            // Make the function name all lowercase and then capitalize the first letter
            char[] retn = (funcName.ToLower()).ToCharArray();
            try
            {
                retn[0] = char.ToUpper(retn[0]);
            }
            catch (IndexOutOfRangeException e)
            {
                // function has no name (is invalid) as indicated by retn[0] not existing
                Console.WriteLine("ERROR {0}: \"{1}\" passed as function name, returning empty string", e.Message, funcName);
                return "";
            }

            return new string(retn);
        }
     
        // Converts a BinaryOp enum value to the actual BinaryOp char
        public static String ConvertBinaryOp(BinaryOp op)
        {
            return binaryOpMap[op];
        }

        // Converts a UnaryOp enum value to the actual UnaryOp char
        public static String ConvertUnaryOp(UnaryOp op)
        {
            return unaryOpMap[op];
        }

        // Reformat range colon, replacing with text for parsing later on
        // eg. A4:C9 -> A4_RANGE_C9
        public static String ReformatRange(String input)
        {
            return colonRangeRegex.Replace(input, "$1_RANGE_$2");
        }

        // Takes in a FirstNameNode (input) and Match object (output)
        // Returns TRUE if successfully found the regex match, FALSE if not
        // Looking for A3_RANGE_C8 style pattern that is NOT in quotes
        public static bool TryGetRangeMatch(FirstNameNode node, out Match matchOutput)
        {
            matchOutput = parsedRangeRegex.Match(node.Ident.Name.Value);
            return matchOutput.Success;
        }

        // Takes in a BinaryOpNode and outputs Match objects for left AND right operands
        // Returns TRUE if any Regex match has been found in either left or right
        // Looking for A3_RANGE_C8 style pattern that is NOT in quotes
        public static bool TryGetRangeMatch(BinaryOpNode node, String leftStr, String rightStr, out Match matchOutLeft, out Match matchOutRight)
        {
            matchOutLeft = parsedRangeRegex.Match(leftStr);
            matchOutRight = parsedRangeRegex.Match(rightStr);

            return matchOutLeft.Success || matchOutRight.Success;
        }

        public static bool TryGetCellMatch(String cellId, out Match matchOut)
        {
            matchOut = currCellRegex.Match(cellId);
            return matchOut.Success;
        }

        // Takes in a defined name object and extracts the sheet and cell that it corresponds to
        public static String ParseDefinedName(ExcelParser.ParsedDefinedNames definedName)
        {
            Match match = definedNameRegex.Match(definedName.Value);

            if (!match.Success) return null;

            String sheetName = match.Groups[2].Value;
            String cellId = match.Groups[4].Value + match.Groups[5].Value;
            return GenerateName(sheetName, cellId);
        }

        // regex that detects a A3:C7 style range, ignoring if it is within quotes
        private static Regex colonRangeRegex = new Regex(@"([A-Z]\d+):([A-Z]\d+)(?=([^""']*[""'][^""']*[""'])*[^""']*$)");

        private static Regex parsedRangeRegex = new Regex(@"([A-Z])(\d+)_RANGE_([A-Z])(\d+)(?=([^""']*[""'][^""']*[""'])*[^""']*$)");

        private static Regex currCellRegex = new Regex(@"([A-Z])(\d+)");

        private static Regex definedNameRegex = new Regex(@"(['']?)([^'']+)(['']?)!\$([A-Z])\$(\d+)");


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
