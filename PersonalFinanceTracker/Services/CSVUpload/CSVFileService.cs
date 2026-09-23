using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.FileSystemGlobbing.Internal;
using System.Text.RegularExpressions;
using System.Threading.Tasks.Sources;

namespace PersonalFinanceTracker.Services.CSVUpload
{
    public static class CSVFileService
    {
        public static string FolderContentRootPath = "";
        private static string _csvFileUploadPath = "";
        private static string _patternForCommaInsideQuotes = @"(['""])(.*?),(.*?)\1";

        // private static string _patternForCommaInsideQuotes = @"(['""])[^'""]*?,[^'""]*?\1";
        // private static string _patternForCommaInsideQuotes = @"(['""])([^'""\r\n]*),([^'""\r\n]*)\1";


        // Returns the current user's CSV File Upload Folder Path
        private static string GetUserCSVUploadFolderPath(ulong userId) 
        {

            string folderPath = Path.Combine(FolderContentRootPath, "Uploads", userId.ToString(), "CSVUpload");

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            return folderPath;

        }

        // Save the CSV data File as ("Current User's UserId Name") to current user's upload folder
        public static void UploadCSVFile(ulong userId, IFormFile file)
        {

            _csvFileUploadPath = GetUserCSVUploadFolderPath(userId);

            DeleteExistingCsvFiles(_csvFileUploadPath);

            string filePath = Path.Combine(_csvFileUploadPath, string.Join("", userId.ToString(), ".csv"));

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                file.CopyTo(stream);
            }

        }

        // Get the CSV Contents
        public static (List<string[]>? contents, string? errorMessage) GetCSVFileContent(ulong userId)
        {

            string filePath = Path.Combine(_csvFileUploadPath, string.Join("", userId.ToString(), ".csv"));
            
            if (!System.IO.File.Exists(filePath))
                return (null, "File no longer exists, please re-upload");

            (bool result, string? errorMessage) isValidCSVFile = IsAValidCSVFile(filePath);

            if (!isValidCSVFile.result)
                return (null, isValidCSVFile.errorMessage);

            return ( GetCsvNonEmptyRows(filePath), null );

        }


        // Get the CSV file Content by elimination of the empty rows!!!
        // Returns null when all the rows are empty of the supplied file!!!
        private static List<string[]>? GetCsvNonEmptyRows(string filePath)
        {

            var rows = new List<string[]>();
            
            using (var reader = new StreamReader(filePath))
            {

                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();

                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    
                    // If the file is quoted
                    if (line.ToCharArray().Count("\"") > 3)
                    {
                        if (line.StartsWith("\""))
                        {
                            line = line.Substring(1);
                        }
                        if (line.EndsWith("\""))
                        {
                            line = line.Substring(0, line.Length - 1);
                        }

                        line = line.Replace("\",", ",");
                        line = line.Replace(",\"", ",");
                    }

                    // If any field contains comma (,) in-between single (') or double (") quotes
                    if(Regex.IsMatch(line, _patternForCommaInsideQuotes))
                    {
                        
                        line = ReplaceCommaWithinFieldValue(line);
                    }

                    var values = line.Split(',');

                    if (values.All(v => string.IsNullOrWhiteSpace(v)))
                        continue;

                    for(int i = 0; i < values.Length; i++)
                    {
                        
                        values[i] = values[i].Trim();
                        int contentLength = values[i].Length;

                        // Remove value identifiers
                        if (contentLength > 0 && values[i].Substring(0, 1) == "\"" && values[i].Substring(contentLength - 1, 1) == "\"")
                        {
                            values[i] = values[i].Substring(1, contentLength - 2);
                        }

                    }

                    rows.Add(values);

                }
            }

            return rows.Count() > 0 ? rows : null;

        }

        // Checks for empty or more than required fields!!!
        private static (bool result, string? errorMessage) IsAValidCSVFile(string filePath)
        {

            using (var reader = new StreamReader(filePath))
            {

                if (reader.EndOfStream)
                {
                    return (false, "File has no contents)");    // If empty file
                }
                else
                {

                    var dataRows = File.ReadAllLines(filePath);

                    // Check for more than required fields!!!
                    if (dataRows.Any(f => f.Split(",").Count() > 8))
                    {
                        return (false, "File has more than 8 columns");
                    }

                    // If quoted file then all the lines must be start with '"'
                    if (dataRows[0].StartsWith(@"""") && dataRows.Any(r => !r.StartsWith(@"""")))      // !dataRows[1].StartsWith(@"""")
                    {
                        return (false, "Not all the records is in quoted string format");
                    }
                }
            }

            return (true, null);

          }

        // Delete all the existing CSV files 
        public static void DeleteExistingCsvFiles(string folderPath)
        {

            var csvFiles = Directory.GetFiles(folderPath, "*.csv");

            foreach (var file in csvFiles)
            {
                File.Delete(file);
            }

        }

        // Single field that contains comma, must be single/double quoted
        // dataRowLine: can be a single field value or the whole row that contains more than one comma-separated fields 
        public static string ReplaceCommaWithinFieldValue(string dataRowLine)
        {
            string result = dataRowLine;
            string previous;

            // Loop ensures all commas inside the same quoted string are replaced sequentially
            do
            {
                previous = result;
                result = Regex.Replace(result, _patternForCommaInsideQuotes, m =>
                {
                    string quote = m.Groups[1].Value;
                    string part1 = m.Groups[2].Value;
                    string part2 = m.Groups[3].Value;

                    return $"{quote}{part1}|{part2}{quote}";
                });
            }
            while (result != previous);

            return result;

        }

    }
}
