using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.CognitiveSearch.WebApiSkills;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace CognitiveSkill
{
    public static class Function1
    {
        public static string BingMapsKey = "<BING MAPS API KEY>";
        [FunctionName("ReviewDataCleanup")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)]HttpRequestMessage req, TraceWriter log, ILogger logger, ExecutionContext executionContext)
        {
            
            string skillName = executionContext.FunctionName;
            IEnumerable<WebApiRequestRecord> requestRecords = WebApiSkillHelpers.GetRequestRecords(req);
            if (requestRecords == null)
            {

                return req.CreateErrorResponse(HttpStatusCode.BadRequest, $"{skillName} - Invalid request record array.");
            }
            WebApiSkillResponse resp = new WebApiSkillResponse();
            resp.Values = new List<WebApiResponseRecord>();
            foreach (WebApiRequestRecord reqRec in requestRecords) {
                double lat = Convert.ToDouble(reqRec.Data["latitude"]);
                double lng = Convert.ToDouble(reqRec.Data["longitude"]);
                double review = Convert.ToDouble(reqRec.Data["reviews_rating"]);
                string language = (string)reqRec.Data["language"];
                WebApiResponseRecord output = new WebApiResponseRecord();
                try
                {
                  
                    if (review > 5)
                        review = review / 2;

                    Address addr = await reverseGeocode(lat, lng, logger);
                    if (addr != null)
                    {
                        output.Data["state"] = addr.adminDistrict;
                        output.Data["country"] = addr.countryRegion;
                    }
                    else
                    {
                        output.Data["state"] = "UNKNOWN";
                        output.Data["country"] = "UNKNOWN";
                    }

                    output.RecordId = reqRec.RecordId;

                    output.Data["reviews_rating"] = review;

                    
                    resp.Values.Add(output);
                    
                }
                catch (System.Exception ex)
                {
                    log.Info($"EXCEPTION !!!! {ex.Message}");
                    log.Info(ex.StackTrace);
                    output.RecordId = reqRec.RecordId;
                    //output.Errors = new List<WebApiErrorWarningContract>();
                    //output.Errors.Add(new WebApiErrorWarningContract() { Message = ex.Message });
                    output.Data["state"] = "UNKNOWN";
                    output.Data["country"] = "UNKNOWN";
                    output.Data["reviews_rating"] = review;
                    
                    resp.Values.Add(output);

                    return req.CreateResponse(HttpStatusCode.OK, resp);
                }
            }



            log.Info($"Successful Run  returning {resp.Values.Count} records");
            return req.CreateResponse(HttpStatusCode.OK, resp);
        }
        
        static async Task<Address> reverseGeocode(double lat, double lng, ILogger logger)
        {
            HttpClient client = new HttpClient();
            string uri = $"http://dev.virtualearth.net/REST/v1/Locations/{lat},{lng}?key={BingMapsKey}";

            HttpResponseMessage response = await client.GetAsync(uri);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();
            // Above three lines can be replaced with new helper method below
            // string responseBody = await client.GetStringAsync(uri);
            Address addr = null;
            try
            {
                BingMapsResponse bingResp = JsonConvert.DeserializeObject<BingMapsResponse>(responseBody);
                addr = bingResp.resourceSets.First().resources.First().address;
            }
            catch(Exception ex)
            {
                logger.LogInformation("Exception Url={uri} ", uri);
                logger.LogInformation("Exception Response={responseBody} ", responseBody);
            }
            
            return addr;
        }
    }

    public class BingMapsResponse
    {
        public string authenticationResultCode { get; set; }
        public string brandLogoUri { get; set; }
        public string copyright { get; set; }
        public Resourceset[] resourceSets { get; set; }
        public int statusCode { get; set; }
        public string statusDescription { get; set; }
        public string traceId { get; set; }
    }

    public class Resourceset
    {
        public int estimatedTotal { get; set; }
        public Resource[] resources { get; set; }
    }

    public class Resource
    {
        public string __type { get; set; }
        public float[] bbox { get; set; }
        public string name { get; set; }
        public Point point { get; set; }
        public Address address { get; set; }
        public string confidence { get; set; }
        public string entityType { get; set; }
        public Geocodepoint[] geocodePoints { get; set; }
        public string[] matchCodes { get; set; }
    }

    public class Point
    {
        public string type { get; set; }
        public float[] coordinates { get; set; }
    }

    public class Address
    {
        public string addressLine { get; set; }
        public string adminDistrict { get; set; }
        public string adminDistrict2 { get; set; }
        public string countryRegion { get; set; }
        public string formattedAddress { get; set; }
        public string locality { get; set; }
        public string postalCode { get; set; }
    }

    public class Geocodepoint
    {
        public string type { get; set; }
        public float[] coordinates { get; set; }
        public string calculationMethod { get; set; }
        public string[] usageTypes { get; set; }
    }

    

    
   
}
