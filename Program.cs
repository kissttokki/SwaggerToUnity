using NSwag;
using NSwag.CodeGeneration.CSharp;
using NJsonSchema.CodeGeneration.CSharp;
using NJsonSchema;
using System.Text.Json;
using NSwag.CodeGeneration.OperationNameGenerators;
using System.Diagnostics.CodeAnalysis;
using Fluid;
using NSwag.CodeGeneration;

namespace SwaggerUnityGenerator
{
    class SwaggerUnityGenerator
    {

        public static async Task Main()
        {
            var config = LoadConfig();

            string swaggerUrl = config.SwaggerUrl;
            string outputRoot = config.OutputRoot;

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


            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, true);
            }
            Directory.CreateDirectory(outputRoot);


            var generator = new CSharpClientGenerator(document, new CSharpClientGeneratorSettings
            {
                CSharpGeneratorSettings = 
                {
                    TemplateDirectory = "./Templates",
                    Namespace = config.DTONamespace 
                }
            });
            var dtoCode = generator.GenerateFile(NSwag.CodeGeneration.ClientGeneratorOutputType.Contracts);
            var outPath = Path.Combine(outputRoot, $"Model.cs");
            File.WriteAllText(outPath, dtoCode);

            var groupedByTag = document.Paths
                .SelectMany(path => path.Value.ActualPathItem.Select(op => new {
                    Path = path.Key,
                    Operation = op,
                    Tag = op.Value.Tags.FirstOrDefault() ?? "Default"
                }))
                .GroupBy(x => x.Tag);

            foreach (var group in groupedByTag)
            {
                var controllerName = group.Key; // Controller 이름
                var className = $"{controllerName}Client"; // 예: UserClient

                var controllerDoc = new OpenApiDocument
                {
                    Info = document.Info,
                };

                foreach (var item in group)
                {
                    controllerDoc.Paths[item.Path] = document.Paths[item.Path];
                }

                foreach (var schema in document.Components.Schemas)
                    controllerDoc.Components.Schemas.Add(schema.Key, schema.Value);

                foreach (var response in document.Components.Responses)
                    controllerDoc.Components.Responses.Add(response.Key, response.Value);

                foreach (var parameter in document.Components.Parameters)
                    controllerDoc.Components.Parameters.Add(parameter.Key, parameter.Value);

                foreach (var requestBody in document.Components.RequestBodies)
                    controllerDoc.Components.RequestBodies.Add(requestBody.Key, requestBody.Value);



                var settings = new CSharpClientGeneratorSettings
                {
                    ClassName = className,
                    UseBaseUrl = true,
                    AdditionalNamespaceUsages = new[] { "UnityEngine.Networking", "Cysharp.Threading.Tasks" },
                    CSharpGeneratorSettings = {
            TemplateDirectory = "./Templates",
            Namespace = config.DTONamespace
        }
                };

                generator = new CSharpClientGenerator(controllerDoc, settings);
                var code = generator.GenerateFile(ClientGeneratorOutputType.Implementation);

                var fileName = $"{className}.cs";
                File.WriteAllText(Path.Combine(outputRoot, fileName), code);
                Console.WriteLine($"[Controller] {className} → {fileName}");
            }



            var parser = new FluidParser();
            var templateText = File.ReadAllText("Templates/NetworkConfig.liquid");

            if (parser.TryParse(templateText, out var template, out var errors))
            {
                var context = new TemplateContext();
                context.SetValue("namespace", config.DTONamespace);
                context.SetValue("baseUrl", "https://localhost");
                context.SetValue("timeout", 30);

                var output = await template.RenderAsync(context);


                File.WriteAllText($"{outputRoot}/NetworkConfig.cs", output);
            }
            else
            {
                Console.WriteLine("Liquid 파싱 에러 발생: " + string.Join("\n", errors));
            }


            static string PathToClassName(string path)
            {
                var parts = path.Trim('/')
                                .Split(new[] { '/', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(p => char.ToUpperInvariant(p[0]) + p.Substring(1));
                return string.Concat(parts);
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