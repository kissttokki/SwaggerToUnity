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
        public string DTONamespace { get; set; } = "ApiService";
        public string ClassName { get; set; } = "{tag}ApiClient";
    }
}
