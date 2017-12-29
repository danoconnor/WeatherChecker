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
            public static string APIKeyEnvironmentVariable = "WeatherChecker_AccuWeather_APIKey";

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

        public struct Database
        {
            public static string UserIDEnvironmentVariable = "WeatherChecker_Database_UserID";

            public static string PasswordEnvironmentVariable = "WeatherChecker_Database_Password";

            public static string DatabaseConnectionEnvironmentVariable = "WeatherChecker_Database_Connection";

            public static string DatabaseNameEnvironmentVariable = "WeatherChecker_Database_Name";
        }
    }
}
