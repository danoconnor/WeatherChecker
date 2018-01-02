using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WeatherChecker_DataCollectionJob
{
    public struct Constants
    {
        public struct AccuWeather
        {
            /// <summary>
            /// Returns the current conditions, including the historical conditions for the past 24 hours.
            /// The first parameter is the AccuWeather location key, the second is the API key.
            /// </summary>
            public static string CurrentConditionsURLFormat = "http://dataservice.accuweather.com/currentconditions/v1/{0}/historical/24?apikey={1}&details=true";

            /// <summary>
            /// Returns predictions for the upcoming five days.
            /// The first parameter is the AccuWeather location key, the second is the API key.
            /// </summary>
            public static string FiveDayForecastURLFormat = "http://dataservice.accuweather.com/forecasts/v1/daily/5day/{0}?apikey={1}&details=true";

            /// <summary>
            /// Map of zip code (how location is stored in our database) to AccuWeather location key
            /// </summary>
            public static List<Location> Locations = new List<Location>()
            {
                new Location(02114, 348735) // Boston
            };
        }
    }
}
