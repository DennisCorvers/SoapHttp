using System.Text;

namespace SoapHttp
{
    public class SoapClient
    {
        public static async Task FakeSoapRequest(string url, string soapAction, string soapRequest)
        {
            Console.WriteLine($"SOAP-action {soapAction} : Sending request to server: {url}");
            var soapWebRequest = new StringContent(soapRequest, Encoding.UTF8, "text/xml");
            soapWebRequest.Headers.Add("SOAPAction", soapAction);

            try
            {
                Console.WriteLine($"SOAP-action {soapAction} : Initiating send request.");
                var client = new HttpClient();
                var response = await client.PostAsync(url, soapWebRequest);

                Console.WriteLine($"SOAP-action {soapAction} : Send complete, waiting for response");

                var responseCode = response.StatusCode;
                var responseText = response.Content.ReadAsStringAsync();
                // Translate to xml?

                Console.WriteLine($"SOAP - action {soapAction}: Response received.");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
