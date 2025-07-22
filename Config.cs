using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SwaggerUnityGenerator
{
    public class Config
    {
        public string[] SwaggerUrl { get; set; } = new string[] { "http://TargetURL" };
        public string OutputRoot { get; set; } = "./Client";
        public string Namespace { get; set; } = "ApiService";
        public string ModelFileName { get; set; } = "Model";
        public string ClassName { get; set; } = "{tag}ApiClient";
    }
}
