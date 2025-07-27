using NSwag;
using NSwag.CodeGeneration.CSharp;
using NJsonSchema.CodeGeneration.CSharp;
using NJsonSchema;
using System.Text.Json;
using NSwag.CodeGeneration.OperationNameGenerators;
using System.Diagnostics.CodeAnalysis;
using Fluid;
using NSwag.CodeGeneration;
using System.Reflection.Metadata;
using NJsonSchema.CodeGeneration;
using Newtonsoft.Json.Linq;

namespace SwaggerUnityGenerator
{
    class SwaggerUnityGenerator
    {

        public static async Task Main(params string[] args)
        {
            var config = LoadConfig();

            string outputRoot = config.OutputRoot;

            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, true);
            }
            Directory.CreateDirectory(outputRoot);


            string[] targetUrls = args == null || args.Length == 0 ? config.SwaggerUrl : args;

            OpenApiDocument totalDocument = await LoadAndMergeSwaggersAsync(config.SwaggerUrl);

            #region CS파일 생성
            var groupedByTag = totalDocument.Paths
                .SelectMany(path => path.Value.ActualPathItem.Select(op => new
                {
                    Path = path.Key,
                    Operation = op,
                    Tag = op.Value.Tags.FirstOrDefault() ?? "Default"
                }))
                .GroupBy(x => x.Tag);

            foreach (var group in groupedByTag)
            {
                var controllerName = group.Key; // Controller 이름
                var className = $"{controllerName}Client"; // 예: UserClient

                var controllerDoc = new OpenApiDocument();

                foreach (var item in group)
                    controllerDoc.Paths[item.Path] = totalDocument.Paths[item.Path];

                foreach (var schema in totalDocument.Components.Schemas)
                    controllerDoc.Components.Schemas.Add(schema.Key, schema.Value);

                foreach (var response in totalDocument.Components.Responses)
                    controllerDoc.Components.Responses.Add(response.Key, response.Value);

                foreach (var parameter in totalDocument.Components.Parameters)
                    controllerDoc.Components.Parameters.Add(parameter.Key, parameter.Value);

                foreach (var requestBody in totalDocument.Components.RequestBodies)
                    controllerDoc.Components.RequestBodies.Add(requestBody.Key, requestBody.Value);



                var settings = new CSharpClientGeneratorSettings
                {
                    ClassName = className,
                    UseBaseUrl = true,
                    AdditionalNamespaceUsages = new[] { "UnityEngine.Networking", "Cysharp.Threading.Tasks" },
                    CSharpGeneratorSettings = {
                        TemplateDirectory = "./Templates",
                        Namespace = config.Namespace,
                        EnumNameGenerator = new SwaggerEnumNameGenerator(),
        }
                };

                var code = new CSharpClientGenerator(controllerDoc, settings).GenerateFile(ClientGeneratorOutputType.Implementation);

                var fileName = $"{className}.cs";
                File.WriteAllText(Path.Combine(outputRoot, fileName), code);
                Console.WriteLine($"[Controller] {className} → {fileName}");
            }
            #endregion

            #region DTO 생성
            var generator = new CSharpClientGenerator(totalDocument, new CSharpClientGeneratorSettings
            {
                CSharpGeneratorSettings =
                {
                    TemplateDirectory = "./Templates",
                    Namespace = config.Namespace,
                    EnumNameGenerator = new SwaggerEnumNameGenerator()
                }
            });

            var dtoCode = generator.GenerateFile(NSwag.CodeGeneration.ClientGeneratorOutputType.Contracts);
            var outPath = Path.Combine(outputRoot, $"Model.cs");
            File.WriteAllText(outPath, dtoCode);
            #endregion

            #region Config 파일 생성
            var parser = new FluidParser();
            var templateText = File.ReadAllText("Templates/NetworkConfig.liquid");

            if (parser.TryParse(templateText, out var template, out var errors))
            {
                var context = new TemplateContext();
                context.SetValue("namespace", config.Namespace);
                context.SetValue("baseUrl", "https://localhost");
                context.SetValue("timeout", 30);

                var output = await template.RenderAsync(context);


                File.WriteAllText($"{outputRoot}/NetworkConfig.cs", output);
            }
            else
            {
                Console.WriteLine("Liquid 파싱 에러 발생: " + string.Join("\n", errors));
            }
            #endregion

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

        public static async Task<OpenApiDocument> LoadAndMergeSwaggersAsync(string[] swaggerUrls)
        {
            JObject merged = new JObject
            {
                ["openapi"] = "3.0.1",
                ["info"] = new JObject { ["title"] = "Merged API", ["version"] = "1.0" },
                ["paths"] = new JObject(),
                ["components"] = new JObject
                {
                    ["schemas"] = new JObject(),
                    ["responses"] = new JObject(),
                    ["parameters"] = new JObject(),
                    ["requestBodies"] = new JObject()
                }
            };

            using var http = new HttpClient();

            foreach (var url in swaggerUrls)
            {
                var json = await http.GetStringAsync(url).ConfigureAwait(false);
                var doc = JObject.Parse(json);

                // paths 병합
                foreach (var prop in doc["paths"]!.Children<JProperty>())
                    merged["paths"]![prop.Name] = prop.Value;

                // schemas, responses, parameters, requestBodies 병합
                var compsSource = doc["components"] as JObject;
                var compsTarget = merged["components"] as JObject;

                foreach (var section in new[] { "schemas", "responses", "parameters", "requestBodies" })
                {
                    var src = (compsSource![section] as JObject)!;
                    var dst = (compsTarget![section] as JObject)!;

                    if (src == null) continue;
                    foreach (var prop in src.Children<JProperty>())
                        dst[prop.Name] = prop.Value;
                }
            }

            var mergedJson = merged.ToString();

            return await OpenApiDocument.FromJsonAsync(mergedJson).ConfigureAwait(false);
        }

    public class SwaggerEnumNameGenerator : IEnumNameGenerator
        {
            public string Generate(int index, string? name, object? value, JsonSchema schema)
            {
                if (schema.ExtensionData != null && schema.ExtensionData.TryGetValue("x-enum-varnames", out object? varnameToken))
                {
                    if (varnameToken is object[] arr && index < arr.Length)
                    {
                        var rawName = arr[index]?.ToString();
                        if (!string.IsNullOrWhiteSpace(rawName))
                            return NormalizeEnumKey(rawName);
                    }
                }

                if (value != null)
                    return $"Value{value}";

                return $"Unknown{index}";
            }


            private string NormalizeEnumKey(string raw)
            {
                var cleaned = raw.Replace("-", "_")
                                 .Replace(" ", "_")
                                 .Replace(".", "_");

                var parts = cleaned.Split('_', StringSplitOptions.RemoveEmptyEntries);
                return string.Concat(parts.Select(p => char.ToUpperInvariant(p[0]) + p.Substring(1)));
            }
        }

    }
}