using NSwag;
using NSwag.CodeGeneration.CSharp;
using NJsonSchema.CodeGeneration.CSharp;
using NJsonSchema;
using System.Text.Json;

namespace SwaggerUnityGenerator
{
    class SwaggerUnityGenerator
    {

        public static async Task Main()
        {
            var config = LoadConfig();

            string swaggerUrl = config.SwaggerUrl;
            string outputRoot = config.OutputRoot;
            string outputModel = config.OutputModel.Replace("{OutputRoot}", outputRoot);

            OpenApiDocument document;


            try
            {

                document = await OpenApiDocument.FromUrlAsync(swaggerUrl);
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"Wrong Swagger Address : {swaggerUrl}");
                return;
            }

            var tags = document.Operations
                .Select(op => op.Operation.Tags.FirstOrDefault() ?? "Default")
                .Distinct();

            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, true);
            }
            Directory.CreateDirectory(outputRoot);
            Directory.CreateDirectory(outputModel);


            { ///DTO 생성
                var dtoSettings = new CSharpGeneratorSettings
                {
                    Namespace = config.Namespace,
                    //TypeNameGenerator = new TypeNameGenerator()
                };

                foreach (var dto in document.Components.Schemas)
                {
                    var schemaName = dto.Key;
                    var schema = dto.Value;
                    dto.Value.Title = dto.Key;
                    var dtoGenerator = new CSharpGenerator(schema, dtoSettings);
                    var dtoCode = dtoGenerator.GenerateFile();

                    var outputPath = Path.Combine(outputModel, $"{schemaName}.cs");
                    await File.WriteAllTextAsync(outputPath, dtoCode);

                }
            }


            foreach (var tag in tags)
            {
                var settings = new CSharpClientGeneratorSettings
                {
                    ClassName = config.ClassName.Replace("{tag}",tag),
                    InjectHttpClient = true,
                    GenerateDtoTypes = false,
                    CSharpGeneratorSettings =
                    {
                        GenerateDataAnnotations = false,
                        Namespace = config.ClassNamespace.Replace("{tag}",tag)
                    }
                };

                var generator = new CSharpClientGenerator(document, settings);
                var code = generator.GenerateFile();

                var outFile = Path.Combine(outputRoot, $"{tag}ApiClient.cs");
                await File.WriteAllTextAsync(outFile, code);

                Console.WriteLine($"[UnityGenerator] {tag} → {outFile}");
            }
        }

        private static Config LoadConfig(string path = "config.json")
        {
            string json;
            if (!File.Exists(path))
            {
                Config defaultConfig = new Config();
                json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);

                Console.WriteLine($"[Config] 설정 파일이 없어서 기본 config.json을 생성했습니다. 설정을 확인해주세요 \r\n[Config] No configuration file found. A default config.json file will be created automatically please check and update it as needed.");
                return defaultConfig;
            }


            json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Config>(json);
        }
    }
}