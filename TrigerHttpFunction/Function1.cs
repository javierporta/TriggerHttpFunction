using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

namespace TriggerHttpFunction
{
    public static class Function1
    {
        private static HttpClient httpClient = new HttpClient();

        [FunctionName("Function1")]
        public static async Task<IActionResult> Run(HttpRequest req, ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string requestBody = string.Empty;
            using (StreamReader streamReader = new StreamReader(req.Body))
            {
                requestBody = await streamReader.ReadToEndAsync();
            }
            var data = JsonConvert.DeserializeObject<List<RequestModel>>(requestBody);

            //Get max and min temp from request (current values)
            var maxTemperature = data.Max(x => x.Temperature);
            var minTemperature = data.Min(x => x.Temperature);

            //1. call my api and get client threshold
            var cosmosResponse = await httpClient.GetAsync("https://bcn-iot-webapp.azurewebsites.net/api/Clients/MY_CLIENT");
            var jsonString = await cosmosResponse.Content.ReadAsStringAsync();
            dynamic cosmosObject = JsonConvert.DeserializeObject<object>(jsonString);
            var temperatureHighThreshold = (double)cosmosObject.temperatureHighThreshold;
            var temperatureLowThreshold = (double)cosmosObject.temperatureLowThreshold;



            //2. compare values with req
            log.LogInformation($"temperatureHighThreshold is {temperatureHighThreshold}");
            log.LogInformation($"temperatureLowThreshold is {temperatureLowThreshold}");
            log.LogInformation($"Current temperature is {maxTemperature}");

            var alertType = string.Empty;
            if (maxTemperature > temperatureHighThreshold)
            {
                alertType = "high";
            }
            if (minTemperature < temperatureLowThreshold)
            {
                alertType = "low";
            }

            if (alertType == string.Empty)
            {
                //All good. No need to push notification
                log.LogInformation($"No sending push notification");
            }
            else
            {
                log.LogInformation($"Sending push notification");

                var expoData = new ExpoData
                {
                    To = "ExponentPushToken[MY_TOKEN]",
                    Title = $"Temperature is too {alertType}",
                    Body = $"Current temperature between {maxTemperature}ºC and {minTemperature}ºC"
                };
                //Call expo and send push notification
                var json = JsonConvert.SerializeObject(expoData); // or JsonSerializer.Serialize if using System.Text.Json
                var stringContent = new StringContent(json, UnicodeEncoding.UTF8, "application/json"); // use MediaTypeNames.Application.Json in Core 3.0+ and Standard 2.1+

                var postResult = await httpClient.PostAsync("https://exp.host/--/api/v2/push/send", stringContent);

                log.LogInformation(await postResult.Content.ReadAsStringAsync());
                postResult.EnsureSuccessStatusCode();

                log.LogInformation($"Push notification sent correctly");
            }



            return new OkObjectResult("ok");

        }

    }
    public class RequestModel
    {
        public double Temperature { get; set; }
        public DateTime Timestamp { get; set; }
        public string Mac { get; set; }
    }
    public class ExpoData
    {
        [JsonProperty("to")]
        public string To { get; set; }
        [JsonProperty("title")]
        public string Title { get; set; }
        [JsonProperty("body")]
        public string Body { get; set; }
    }

}
