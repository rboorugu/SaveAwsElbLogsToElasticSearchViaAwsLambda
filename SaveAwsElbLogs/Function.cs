using System;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.S3;
using System.IO;
using Amazon.S3.Model;
using Elasticsearch.Net;
using Newtonsoft.Json;
using SaveAwsElbLogs.Logdata;
using Microsoft.Extensions.Configuration;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializerAttribute(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace SaveAwsElbLogs
{
    public class Function
    {
        public static IConfigurationRoot Configuration;
        IAmazonS3 S3Client { get; set; }

        /// <summary>
        /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
        /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
        /// region the Lambda function is executed in.
        /// </summary>
        public Function()
        {
            S3Client = new AmazonS3Client();
        }

        /// <summary>
        /// Constructs an instance with a preconfigured S3 client. This can be used for testing the outside of the Lambda environment.
        /// </summary>
        /// <param name="s3Client"></param>
        public Function(IAmazonS3 s3Client)
        {
            this.S3Client = s3Client;
        }
        
        /// <summary>
        /// This method is called for every Lambda invocation. This method takes in an S3 event object and can be used 
        /// to respond to S3 notifications.
        /// </summary>
        /// <param name="evnt"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<bool> FunctionHandler(S3Event evnt, ILambdaContext context)
        {
            //setting up configuration object (use if required)
           // var builder = new ConfigurationBuilder()
           //.SetBasePath(Directory.GetCurrentDirectory())
           //.AddJsonFile("appsettings.json");
           // Configuration = builder.Build();
            //getting the s3event
            var s3Event = evnt.Records?[0].S3;
            if(s3Event == null)
            {
                return false;
            }

            try
            {
                //setting up elasticsearch search client to save the logs
                var node = new Uri("[elasticserach endpoint goes here]");
                var config = new ConnectionConfiguration(node);
                var esClient = new ElasticLowLevelClient(config);
                var response = await S3Client.GetObjectAsync(s3Event.Bucket.Name, s3Event.Object.Key);
                //this string read all the content from s3 object
                string responseString;
                using (var reader = new StreamReader(response.ResponseStream))
                {
                    responseString = reader.ReadToEnd();
                }
                //get array of the logs by spliting with new line
                string[] allRequests = responseString.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);
                //this will keep track of number of logs added for tracking
                int processCount = 0;
                foreach (var request in allRequests)
                {
                    //reading all the log meta data - they in format below
                    /*timestamp 
                      elb 
                      client:port 
                      backend:port 
                      request_processing_time backend_processing_time 
                      response_processing_time 
                      elb_status_code 
                      backend_status_code 
                      received_bytes 
                      sent_bytes 
                      "request" 
                      "user_agent" 
                      ssl_cipher ssl_protocol*/
                    var allParams = request.Split(new[] { " " }, StringSplitOptions.None);
                    var doc = new Document();
                    for (int i = 0; i < allParams.Length - 1; i++)
                    {
                        if (i == 0) { doc.RequestTime = Convert.ToDateTime(allParams[i]).ToString("yyyy-MM-dd"); }
                        else if (i == 1) { doc.Elb = allParams[i]; }
                        else if (i == 2) { doc.ClientPort = allParams[i]; }
                        else if (i == 3) { doc.BackendPort = allParams[i]; }
                        else if (i == 4) { doc.RequestProcessingTime = allParams[i]; }
                        else if (i == 5) { doc.BackendProcessingTime = allParams[i]; }
                        else if (i == 6) { doc.ResponseProcessingTime = allParams[i]; }
                        else if (i == 7) { doc.ElbStatusCode = allParams[i]; }
                        else if (i == 8) { doc.BackendStatusCode = allParams[i]; }
                        else if (i == 9) { doc.ReceivedBytes = allParams[i]; }
                        else if (i == 10) { doc.SentBytes = allParams[i]; }
                        else if (i == 11) { doc.RequestType = allParams[i].Replace("\"", ""); }
                        else if (i == 12) {doc.RequestUrl = allParams[i];}
                    }
                    var jsonstring = JsonConvert.SerializeObject(doc);
                    //adding the record to elastic search
                    await esClient.IndexAsync<object>("logs", "searchapielb", jsonstring);
                    processCount++;
                }
                Console.WriteLine("Added " + processCount + " number of logs");
                //clean up delete the s3 object added (apply only if required)
                await S3Client.DeleteObjectAsync(new DeleteObjectRequest
                {
                    BucketName = s3Event.Bucket.Name,
                    Key = s3Event.Object.Key
                });
                return true;
            }
            catch(Exception e)
            {
                context.Logger.LogLine($"Error getting object {s3Event.Object.Key} from bucket {s3Event.Bucket.Name}. Make sure they exist and your bucket is in the same region as this function.");
                context.Logger.LogLine(e.Message);
                context.Logger.LogLine(e.StackTrace);
                throw;
            }
        }

        
       
    }
}
 