using NSwag;
using NSwag.CodeGeneration.CSharp;
using NJsonSchema.CodeGeneration.CSharp;
using NJsonSchema;
using System.Text.Json;
using NSwag.CodeGeneration.OperationNameGenerators;
using System.Diagnostics.CodeAnalysis;

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
                CSharpGeneratorSettings = { Namespace = config.DTONamespace }
            });
            var dtoCode = generator.GenerateFile(NSwag.CodeGeneration.ClientGeneratorOutputType.Contracts);
            var outPath = Path.Combine(outputRoot, $"Model.cs");
            File.WriteAllText(outPath, dtoCode);


            // 1. path 별로 Implementation 생성
            foreach (var kv in document.Paths)
            {
                var path = kv.Key;      // ex) "/WeatherForecast"
                var item = kv.Value;    // 해당 OpenApiPathItem
                var className = config.ClassName.Replace("{tag}", PathToClassName(path));

                // 1-1. 이 path 만 남기는 새 문서 복제
                var oneDoc = new OpenApiDocument
                {
                    Info = document.Info,           // 메타정보 복제
                };

                foreach (var com in document.Components.Schemas)
                {
                    oneDoc.Components.Schemas.Add(com.Key, com.Value);
                }



                oneDoc.Paths.Clear();                    // 기존 Paths 전부 지우고
                oneDoc.Paths.Add(path, item);            // 이 경로만 추가

                // 1-2. Implementation 단일 클래스 생성
                var implSettings = new CSharpClientGeneratorSettings
                {
                    ClassName = className,
                    CSharpGeneratorSettings = { Namespace = config.DTONamespace }
                };
                var implGen = new CSharpClientGenerator(oneDoc, implSettings);
                var implCode = implGen.GenerateFile(NSwag.CodeGeneration.ClientGeneratorOutputType.Implementation);

                Console.WriteLine(implCode);

                // 1-3. 파일로 쓰기
                outPath = Path.Combine(outputRoot, $"{className}.cs");
                File.WriteAllText(outPath, implCode);
                Console.WriteLine($"[Impl] {path} → {className}.cs");
            }

            // 헬퍼: "/foo-bar/{id}" → "FooBarById" 같은 식으로 변환
            static string PathToClassName(string path)
            {
                // 1) 맨 앞 슬래시 제거
                // 2) 중괄호, 특수문자 제거 / CamelCase
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