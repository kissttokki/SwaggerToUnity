using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SwaggerUnityGenerator
{
    public class Config
    {
        public string SwaggerUrl { get; set; } = "http://TargetURL";
        public string OutputRoot { get; set; } = "./Client";
        public string OutputModel { get; set; } = "{OutputRoot}/Model";
        public string Namespace { get; set; } = "ApiService.Model";
        public string ClassNamespace { get; set; } = "ApiService.{tag}";
        public string ClassName { get; set; } = "{tag}ApiClient";
    }
}
