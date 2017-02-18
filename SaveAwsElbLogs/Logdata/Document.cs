using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SaveAwsElbLogs.Logdata
{
    public class Document
    {
        public string RequestTime { get; set; }
        public string Elb { get; set; }
        public string ClientPort { get; set; }
        public string BackendPort { get; set; }
        public string RequestProcessingTime { get; set; }
        public string BackendProcessingTime { get; set; }
        public string ResponseProcessingTime { get; set; }
        public string ElbStatusCode { get; set; }
        public string BackendStatusCode { get; set; }
        public string ReceivedBytes { get; set; }
        public string SentBytes { get; set; }
        public string RequestType { get; set; }
        public string RequestUrl { get; set; }
      
    }
}
