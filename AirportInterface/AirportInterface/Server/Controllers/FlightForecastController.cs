using AirportInterface.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http;
using System.IO;
using System.Xml;
using Newtonsoft.Json;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Linq;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Authentication;
using System.Xml.Linq;
using Radzen.Blazor.Rendering;
using Microsoft.AspNetCore.Mvc.Diagnostics;

namespace AirportInterface.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FlightForecastController : ControllerBase
    {
        [HttpGet("{info_provider}/{selected_direction}/{filter}")]
        public async Task<IEnumerable<FlightForecast>> GetAsync(string info_provider, string selected_direction, string filter)
        {
            try
            {
                if (info_provider == "Heathrow") {

                    //Website: https://www.heathrow.com/arrivals
                    var api_call_url = "https://gateway.api.boutique.heathrow.com/hal/flight/search";
                    
                    //Specific flight
                    if (filter != "null")
                    {
                        switch (selected_direction)
                        {
                            case "Both":
                                api_call_url = "https://gateway.api.boutique.heathrow.com/hal/flight/search?channel=master&q=" + filter + "&locale=en_GB";                                                
                                break;
                            case "Arriving":
                                api_call_url = "https://gateway.api.boutique.heathrow.com/hal/flight/search?channel=master&q=" + filter + "&direction=A&locale=en_GB";
                                break;
                            case "Departing":
                                api_call_url = "https://gateway.api.boutique.heathrow.com/hal/flight/search?channel=master&q=" + filter + "&direction=D&locale=en_GB";
                                break;
                        }
                    }

                    var client = new WebClient();
                    var result_raw = client.DownloadString(api_call_url);
                    dynamic result_json = JsonConvert.DeserializeObject<dynamic>(result_raw);

                    //string property_path_example = result_json["results"]["flight"]["hits"][0]["document"]["primaryFlightNumber"].ToString();
                    JArray records = result_json["results"]["flight"]["hits"];

                    List<FlightForecast> list = new List<FlightForecast>();

                    foreach (JObject hit in records)
                    {
                        string hit_str = hit.GetValue("document").ToString();
                        dynamic hit_array = JsonConvert.DeserializeObject<dynamic>(hit_str);

                        FlightForecast new_record = new FlightForecast();

                        if(filter == "null")
                        {
                            new_record.Flight_id = hit_array.GetValue("primaryFlightNumber").ToString();
                        }
                        else
                        {
                            new_record.Flight_id = hit_array.GetValue("matchedFlightNumber").ToString();
                        }
                        new_record.Direction = hit_array.GetValue("direction").ToString();
                        if (new_record.Direction=="A")
                        {
                            new_record.Direction = "Arriving";
                        }
                        else if(new_record.Direction == "D"){
                            new_record.Direction = "Departing";
                        }
                        new_record.Date = hit_array.GetValue("scheduledDateTime").ToString();
                        new_record.City = hit_array.GetValue("cityName").ToString();
                        new_record.Status = hit_array.GetValue("status").ToString();
                        new_record.Terminal = hit_array.GetValue("terminal").ToString();
                        new_record.Airline = hit_array.GetValue("airlineName").ToString();

                        add_to_list(ref list, ref new_record, ref selected_direction, ref filter, ref info_provider);
                    }
                    if (list.Capacity > 0)
                    {
                        return list;
                    }
                    else
                    {
                        return null;
                    }
                }
                else if (info_provider == "LondonLuton")
                {
                    /*// Webservice:
                     * https://www.london-luton.co.uk/flights
                     * https://www.london-luton.co.uk/WebServices/FlightInformation.asmx
                     * https://www.london-luton.co.uk/WebServices/FlightInformation.asmx?WSDL                        
                    */

                    List<FlightForecast> list = new List<FlightForecast>();

                    //Search specific flight
                    if (filter != "null")
                    {
                        ServiceReference_LLArrivals.FlightInformationSoapClient flightInformationSoapClient = new ServiceReference_LLArrivals.FlightInformationSoapClient(ServiceReference_LLArrivals.FlightInformationSoapClient.EndpointConfiguration.FlightInformationSoap);
                        var response = await flightInformationSoapClient.FlightArrivalsAndDeparturesAsync(filter, true);

                        if (response.Length > 0)
                        {
                            foreach (ServiceReference_LLArrivals.FlightInfo rec in response)
                            {
                                FlightForecast new_record = new FlightForecast();
                                new_record.Flight_id = rec.fltnmbr;
                                new_record.City = rec.origdest;
                                new_record.Date = rec.DateofFlight.ToString().Substring(0, 12) + " " + rec.schedtime.ToString();
                                if (rec.isArrival)
                                {
                                    new_record.Direction = "Arriving";
                                }
                                else
                                {
                                    new_record.Direction = "Departing";
                                }
                                add_to_list(ref list, ref new_record, ref selected_direction, ref filter, ref info_provider);
                            }
                        }
                    }

                    //Current flights
                    int from = 0;
                    int to = 0;
                    if (selected_direction == "Both")
                    {
                        from = 0;
                        to = 2;
                    }
                    else if (selected_direction == "Arriving")
                    {
                        from = 0;
                        to = 1;
                    }
                    else if (selected_direction == "Departing")
                    {
                        from = 1;
                        to = 2;
                    }

                    string current_direction = "";
                    for (int i = from; i < to; i++)
                    {
                        ServiceReference_LLArrivals.FlightInformationSoapClient flightInformationSoapClient = new ServiceReference_LLArrivals.FlightInformationSoapClient(ServiceReference_LLArrivals.FlightInformationSoapClient.EndpointConfiguration.FlightInformationSoap);
                        var response = "";
                        if (i == 0)
                        {
                            response = await flightInformationSoapClient.GeneratePlasmaScreenArrivalsTableAsync();                           
                            current_direction = "Arriving";
                        }
                        else if (i == 1)
                        {
                            response = await flightInformationSoapClient.GeneratePlasmaScreenDeparturesTableAsync();
                            current_direction = "Departing";
                        }

                        using (StringReader reader = new StringReader(response.ToString()))
                        {
                            FlightForecast new_record = new FlightForecast();
                            bool first_record = true;
                            string line = string.Empty;
                            do
                            {
                                line = reader.ReadLine();
                                if (line != null)
                                {
                                    if (line.Contains("<tr class='firstPart show"))
                                    {
                                        if (first_record)
                                        {
                                            first_record = false;
                                        }
                                        else
                                        {
                                            new_record.Direction = current_direction;
                                            add_to_list(ref list, ref new_record, ref selected_direction, ref filter, ref info_provider);
                                        }
                                        new_record = new FlightForecast();
                                    }
                                    else if (line.Contains("<td class='firstCol'>"))
                                    {
                                        DateTime today = DateTime.Today;
                                        string today_str = today.ToString("yyyy. MM. dd ");

                                        int length = line.Length - 26;
                                        new_record.Date = today_str + line.Substring(21, length);
                                    }
                                    else if (line.Contains("<td class='flightNo'>"))
                                    {
                                        int length = line.Length - 26;
                                        new_record.Flight_id = line.Substring(21, length);
                                    }
                                    else if (line.Contains("<td class='departures'>"))
                                    {
                                        int length = line.Length - 28;
                                        new_record.City = line.Substring(23, length);
                                    }
                                }
                            } while (line != null);
                            if (!first_record)
                            {
                                new_record.Direction = current_direction;
                                add_to_list(ref list, ref new_record, ref selected_direction, ref filter, ref info_provider);
                            }
                        }                      
                    }
                    if (list.Capacity > 0)
                    {
                        return list;
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
        }

        private void add_to_list(ref List<FlightForecast> list, ref FlightForecast record, ref string selected_direction, ref string filter, ref string info_provider)
        {
            //TODO allow_multi
            if (record != null && (selected_direction == "Both" || (record.Direction == selected_direction)) && (filter=="null" || record.City==filter || record.Flight_id==filter))
            {
                record.InfoProvider = info_provider;
                record.PullDate = DateTime.Now;

                bool exists = false;
                if (list.Count > 0)
                {
                    foreach(FlightForecast existing_record in list)
                    {
                        if (existing_record.Flight_id == record.Flight_id && existing_record.Date == record.Date)
                        {
                            exists = true;
                        }
                    }
                }
                if (!exists)
                {
                    list.Add(record);
                }
            }
        }
    }
}
