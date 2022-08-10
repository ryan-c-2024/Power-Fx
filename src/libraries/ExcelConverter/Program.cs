﻿// <autogenerated>
// Use autogenerated to suppress stylecop warnings 

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.InkML;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.PowerFx;
using Microsoft.PowerFx.Core;
using Microsoft.PowerFx.Syntax;
using Microsoft.PowerFx.Types;
using Microsoft.PowerFx.Preview;
using Microsoft.PowerFx.Interpreter;
using Newtonsoft.Json;
using DocumentFormat.OpenXml.Office2010.PowerPoint;
using DocumentFormat.OpenXml.Drawing.Charts;

namespace ExcelConverter
{
    public class Converter
    {
        public static void Main(string[] args)
        {
            // would it be more efficient to run some of the processing AS WE ARE PARSING instead of after we're done?
            // Also, sometimes ExcelConverter doesn't run past ParseSpreadsheet for some reason

            ExcelParser.ParsedExcelData data = ExcelParser.ParseSpreadsheet(@"test.xlsx"); // parse Excel spreadsheet and extract data
            var engine = new Engine(new PowerFxConfig());

            foreach (ExcelParser.ParsedDefinedNames d in data.DefinedNames)
            {
                // Parse defined name to get the generic name (sheetname_cellnum) it corresponds to
                // If it's not a named cell, it's a named range...
                String parsedGenericName = Utils.ParseDefinedName(d);
                if (parsedGenericName != null)
                {
                    definedNamesMap.Add(parsedGenericName, d.Name); // Add to our map
                }
                else
                {
                    // Add range data to our map so we can convert the DefinedRange to a A3_RANGE_C9 style object
                    // In the current implementation a defined range ends up decomposed into its constituents when converted to PowerFX
                    // Eg. SUM(definedRange1) -> (eventually ...) -> Sum(Sheet1_C3, Sheet1_C4, ..., Sheet1_D9)
                    String parsedDefinedRange = Utils.ParseDefinedRange(d);
                    definedRangesMap.Add(d.Name, parsedDefinedRange);
                }
            }

            // Iterate through tables, running preprocessing and preliminary conversion work
            foreach (ExcelParser.ParsedTable p in data.Tables)
            {
                // Add table to map so we can get table object from the table name later
                tableMap.Add(p.Name, p);

                // Get and adjust general table range information
                Range tableRange = Utils.DecomposeRange(p.Range);
                int startRowNum = tableRange.startNum + 1; // Correct starting row by 1 to skip over header row

                char columnChar = tableRange.startChar;
                // Establish and initialize ranges for each column in the table
                foreach (ExcelParser.ParsedTableColumn c in p.Columns)
                {
                    // Define spans (ie. ranges) for each column ... used to resolve table references later
                    c.columnSpan = new Range(columnChar, startRowNum, columnChar, tableRange.endNum);
                    p.ColumnMap.Add(c.Name, c);

                    // increment column char so we go from eg. B4:B8 -> C4:C8 -> D4:D8 as we're defining ranges for columns
                    columnChar = (char)(columnChar + 1); 
                }

                // Build table output and add it to the output list
                String tableFormula = Utils.BuildTableOutput(p);
                String tableName = Utils.CreateVariable(p.SheetName, p.Name, tableFormula);
                outputList.Add(tableName); // add sheet name eg. sheet1_table1
            }


            // Iterate through all parsed cells and convert to PFX if applicable            
            foreach (ExcelParser.ParsedCell c in data.Cells)
            {
                if (c == null || processedSet.Contains(c.CellId)) continue;

                ParseResult p;

                if (c.Formula != null) 
                {
                    // If formula has a range, preprocess and reformat it
                    // Otherwise, the engine parser gets tripped up by the range colon

                    c.Formula = Utils.TableToRange(c.Formula, c);
                    c.Formula = Utils.ReformatRange(c.Formula);
                    p = engine.Parse(c.Formula);
                }
                else
                {
                    p = engine.Parse(c.Value);
                }
                
                // only want to run PFX conversion if either a formula or a literal number node
                // Currently not converting StringLits because it often spams output with non-formula related cells
                if (c.Formula != null || p.Root.Kind == NodeKind.NumLit) 
                {
                    // Convert to PFX then add it to our output list
                    String result = ParsedCellAnalyzer.Analyze(p.Root, c);

                    if (!processedSet.Contains(c.CellId))
                    {
                        outputList.Add(Utils.CreateVariable(c.SheetName, c.CellId, result.ToString()));
                    }
                    processedSet.Add(c.CellId);
                }
            }

            foreach (String converted in outputList)
            {
                Console.WriteLine(converted);
            }
        }

        public static List<String> outputList = new List<String>();
        public static HashSet<String> processedSet = new HashSet<String>();

        // Maps generic variable names (String) to the defined name (String)
        // eg. Sheet1_C8 -> MyVariable
        public static Dictionary<String, String> definedNamesMap = new Dictionary<String, String>();

        // Maps range defined name (String) to the parsed range object (String)
        // eg. MyRange1 -> A3_RANGE_C9
        public static Dictionary<String, String> definedRangesMap = new Dictionary<String, String>();

        // Maps name of table names to table objects
        public static Dictionary<String, ExcelParser.ParsedTable> tableMap = new Dictionary<String, ExcelParser.ParsedTable>();
    }
}
