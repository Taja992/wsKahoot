using NJsonSchema.CodeGeneration.TypeScript;
using NSwag.CodeGeneration.TypeScript;
using NSwag.Generation;

namespace Api.Documentation;

public static class GenerateTypescriptClient
{
    public static async Task GenerateTypeScriptClient(this WebApplication app, string path)
    {
        var document = await app.Services.GetRequiredService<IOpenApiDocumentGenerator>()
            .GenerateAsync("v1");

        var settings = new TypeScriptClientGeneratorSettings
        {
            Template = TypeScriptTemplate.Fetch,
            TypeScriptGeneratorSettings =
            {
                TypeStyle = TypeScriptTypeStyle.Interface,
                DateTimeType = TypeScriptDateTimeType.Date,
                NullValue = TypeScriptNullValue.Undefined,
                TypeScriptVersion = 5.2m,
                GenerateCloneMethod = false
            }
        };

        var generator = new TypeScriptClientGenerator(document, settings);
        var code = generator.GenerateFile();

        // Split lines using different line endings
        var lines = code.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None).ToList();
        
        Console.WriteLine("Generated TypeScript Client Code:");
        foreach (var line in lines)
        {
            Console.WriteLine(line);
        }
        
        // Debugging output to check each line
        for (int i = 0; i < lines.Count; i++)
        {
            Console.WriteLine($"Line {i}: '{lines[i]}'");
        }

        var startIndex = lines.FindIndex(l => l.Trim().Contains("export interface BaseDto {"));
        Console.WriteLine("Start Index: " + startIndex);
        if (startIndex >= 0)
        {
            Console.WriteLine("Found 'export interface BaseDto {' at line: " + startIndex);
            lines.RemoveRange(startIndex, 4); // Remove 4 lines (interface declaration and two properties)
        }
        else
        {
            Console.WriteLine("Did not find 'export interface BaseDto {' in the generated code.");
        }

        lines.Insert(0, "import { BaseDto } from 'ws-request-hook';");

        var modifiedCode = string.Join(Environment.NewLine, lines);

        var outputPath = Path.Combine(Directory.GetCurrentDirectory() + path);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        await File.WriteAllTextAsync(outputPath, modifiedCode);
        Console.WriteLine("TypeScript client generated at: " + outputPath);
    }
}