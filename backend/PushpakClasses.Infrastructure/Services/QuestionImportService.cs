using System.Globalization;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using PushpakClasses.Application.Common.Interfaces;
using PushpakClasses.Application.Features.Quizzes.Dtos;
using PushpakClasses.Domain.Enums;

namespace PushpakClasses.Infrastructure.Services;

public class QuestionImportService : IQuestionImportService
{
    public async Task<(List<CreateQuestionRequestDto> Questions, List<string> Errors)> ParseFileAsync(
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var questions = new List<CreateQuestionRequestDto>();
        var errors = new List<string>();

        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (extension == ".csv")
        {
            ParseCsv(fileStream, questions, errors);
        }
        else if (extension == ".xlsx")
        {
            ParseExcel(fileStream, questions, errors);
        }
        else
        {
            errors.Add($"Unsupported file extension '{extension}'. Only .csv and .xlsx files are supported.");
        }

        return await Task.FromResult((questions, errors));
    }

    private void ParseCsv(Stream stream, List<CreateQuestionRequestDto> questions, List<string> errors)
    {
        using var reader = new StreamReader(stream);
        var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim,
            IgnoreBlankLines = true
        };

        using var csv = new CsvReader(reader, csvConfig);

        if (!csv.Read() || !csv.ReadHeader())
        {
            errors.Add("CSV file is empty or missing valid headers (Text, Type, Marks, Option1, Option2, CorrectOptions).");
            return;
        }

        int rowNumber = 1;
        while (csv.Read())
        {
            rowNumber++;
            try
            {
                var text = csv.GetField<string>("Text");
                var typeStr = csv.GetField<string>("Type");
                var marksStr = csv.GetField<string>("Marks");
                var opt1 = csv.GetField<string>("Option1");
                var opt2 = csv.GetField<string>("Option2");
                var opt3 = csv.TryGetField<string>("Option3", out var o3) ? o3 : null;
                var opt4 = csv.TryGetField<string>("Option4", out var o4) ? o4 : null;
                var correctStr = csv.GetField<string>("CorrectOptions");

                var dto = BuildQuestionDto(text, typeStr, marksStr, opt1, opt2, opt3, opt4, correctStr, rowNumber, errors);
                if (dto != null)
                {
                    questions.Add(dto);
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Row {rowNumber}: Failed to parse row ({ex.Message}).");
            }
        }
    }

    private void ParseExcel(Stream stream, List<CreateQuestionRequestDto> questions, List<string> errors)
    {
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault();
        if (worksheet == null)
        {
            errors.Add("Excel workbook does not contain any worksheets.");
            return;
        }

        var firstRow = worksheet.FirstRowUsed();
        if (firstRow == null)
        {
            errors.Add("Excel sheet is empty.");
            return;
        }

        var headerRow = firstRow;
        var columnMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in headerRow.Cells())
        {
            var headerName = cell.GetString().Trim();
            if (!string.IsNullOrEmpty(headerName))
            {
                columnMap[headerName] = cell.Address.ColumnNumber;
            }
        }

        if (!columnMap.ContainsKey("Text") || !columnMap.ContainsKey("CorrectOptions"))
        {
            errors.Add("Excel file must contain 'Text' and 'CorrectOptions' header columns.");
            return;
        }

        var rows = worksheet.RowsUsed().Skip(1);
        int rowNumber = 1;

        foreach (var row in rows)
        {
            rowNumber++;
            var text = GetCellValue(row, columnMap, "Text");
            if (string.IsNullOrWhiteSpace(text)) continue;

            var typeStr = GetCellValue(row, columnMap, "Type");
            var marksStr = GetCellValue(row, columnMap, "Marks");
            var opt1 = GetCellValue(row, columnMap, "Option1");
            var opt2 = GetCellValue(row, columnMap, "Option2");
            var opt3 = GetCellValue(row, columnMap, "Option3");
            var opt4 = GetCellValue(row, columnMap, "Option4");
            var correctStr = GetCellValue(row, columnMap, "CorrectOptions");

            var dto = BuildQuestionDto(text, typeStr, marksStr, opt1, opt2, opt3, opt4, correctStr, rowNumber, errors);
            if (dto != null)
            {
                questions.Add(dto);
            }
        }
    }

    private static string? GetCellValue(IXLRow row, Dictionary<string, int> columnMap, string columnName)
    {
        return columnMap.TryGetValue(columnName, out var colIndex)
            ? row.Cell(colIndex).GetString().Trim()
            : null;
    }

    private static CreateQuestionRequestDto? BuildQuestionDto(
        string? text,
        string? typeStr,
        string? marksStr,
        string? opt1,
        string? opt2,
        string? opt3,
        string? opt4,
        string? correctStr,
        int rowNumber,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            errors.Add($"Row {rowNumber}: Question text is empty.");
            return null;
        }

        if (!Enum.TryParse<QuestionType>(typeStr, true, out var questionType))
        {
            questionType = QuestionType.SingleChoice; // Default fallback
        }

        decimal marks = 1;
        if (!string.IsNullOrWhiteSpace(marksStr) && decimal.TryParse(marksStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedMarks) && parsedMarks > 0)
        {
            marks = parsedMarks;
        }

        var rawOptions = new List<string?> { opt1, opt2, opt3, opt4 }
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Select(o => o!.Trim())
            .ToList();

        var correctTokens = (correctStr ?? "")
            .Split(new[] { ',', ';', ' ', '|' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim().ToLowerInvariant())
            .ToHashSet();

        var options = new List<CreateOptionDto>();

        if (questionType == QuestionType.TrueFalse)
        {
            // True / False rules
            var isTrueCorrect = correctTokens.Contains("true") || correctTokens.Contains("1") || correctTokens.Contains("t");
            var isFalseCorrect = correctTokens.Contains("false") || correctTokens.Contains("2") || correctTokens.Contains("f");

            if (!isTrueCorrect && !isFalseCorrect)
            {
                errors.Add($"Row {rowNumber}: True/False question must specify 'True' or 'False' as the correct answer.");
                return null;
            }

            options.Add(new CreateOptionDto { Text = "True", IsCorrect = isTrueCorrect });
            options.Add(new CreateOptionDto { Text = "False", IsCorrect = isFalseCorrect });
        }
        else
        {
            if (rawOptions.Count < 2)
            {
                errors.Add($"Row {rowNumber}: Question '{text}' has fewer than 2 options.");
                return null;
            }

            for (int i = 0; i < rawOptions.Count; i++)
            {
                var optText = rawOptions[i];
                var optionIndexStr = (i + 1).ToString();
                var optionLetterStr = ((char)('a' + i)).ToString();

                var isCorrect = correctTokens.Contains(optionIndexStr)
                             || correctTokens.Contains(optionLetterStr)
                             || correctTokens.Contains(optText.ToLowerInvariant());

                options.Add(new CreateOptionDto
                {
                    Text = optText,
                    IsCorrect = isCorrect
                });
            }

            var correctCount = options.Count(o => o.IsCorrect);
            if (questionType == QuestionType.SingleChoice && correctCount != 1)
            {
                errors.Add($"Row {rowNumber}: SingleChoice question '{text}' must have exactly 1 correct option (found {correctCount}).");
                return null;
            }

            if (questionType == QuestionType.MultiChoice && correctCount < 1)
            {
                errors.Add($"Row {rowNumber}: MultiChoice question '{text}' must have at least 1 correct option.");
                return null;
            }
        }

        return new CreateQuestionRequestDto
        {
            Text = text.Trim(),
            Type = questionType,
            Marks = marks,
            Options = options
        };
    }
}
