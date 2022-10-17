using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Newtonsoft.Json;

namespace WeatherChecker_DataCollectionJob
{
    class Program
    {
        static void Main()
        {
            var config = new JobHostConfiguration();

            if (config.IsDevelopment)
            {
                config.UseDevelopmentSettings();
            }

            var host = new JobHost(config);
            host.Start();

            WriteOutput("Beginning data collection task");
            Task dataTask = GetLatestDataAsync().ContinueWith(innerTask =>
            {
                if (innerTask.IsFaulted)
                {
                    WriteOutput($"GetLatestData failed with expection {innerTask.Exception}");
                }
                else
                {
                    WriteOutput($"GetLatestData completed successfully");
                }
            });

            WriteOutput("Waiting for data collection task");
            dataTask.Wait();
            WriteOutput("Data collection task completed.");

            host.Stop();
        }

        static Task GetLatestDataAsync()
        {
            List<Task> locationTasks = new List<Task>();
            foreach (Location location in Constants.AccuWeather.Locations)
            {
                locationTasks.Add(GetLocationData(location));
            }

            return Task.WhenAll(locationTasks);
        }

        static Task GetLocationData(Location location)
        {
            return Task.WhenAll(new List<Task>()
            {
                GetPast24HrData(location),
                GetFiveDayForecastData(location)
            });
        }

        static async Task GetPast24HrData(Location location)
        {
            string currentConditionsUrl = string.Format(Constants.AccuWeather.CurrentConditionsURLFormat, location.AccuWeatherLocationKey, PrivateConstants.AccuWeatherAPIKey);
            Task<WebResponse> currentConditionsResponseTask = WebRequest.CreateHttp(currentConditionsUrl).GetResponseAsync();

            WebResponse currentConditionsResponse = await currentConditionsResponseTask;
            using (StreamReader stream = new StreamReader(currentConditionsResponse.GetResponseStream()))
            {
                string jsonText = stream.ReadToEnd();
                WeatherObservation[] observations = JsonConvert.DeserializeObject<WeatherObservation[]>(jsonText);
                Debug.Assert(observations.Length > 0);

                // Get the first observation and use it to get the high and low from the previous 24 hours
                WeatherObservation firstObservation = observations[0];
                int highTemp = (int)firstObservation.TemperatureSummary.Past24HourRange.Maximum.Imperial.Value;
                int lowTemp = (int)firstObservation.TemperatureSummary.Past24HourRange.Minimum.Imperial.Value;

                WeatherData daytimeData = GetWeatherData(observations, true, highTemp, lowTemp);
                WeatherData nighttimeData = GetWeatherData(observations, false, highTemp, lowTemp);

                // Setting DaysBefore = -1 indicates that this is the actual observation data for the target date
                using (SqlConnection dbConnection = new SqlConnection(GetDatabaseConnectionString()))
                {
                    dbConnection.Open();

                    // This data represents the actual result for the previous day
                    DateTime yesterday = NormalizeDate(DateTime.Now).Subtract(new TimeSpan(1, 0, 0, 0));

                    InsertWeatherData(dbConnection, location.ZipCode, yesterday, -1, daytimeData, nighttimeData);
                }
            }
        }

        static async Task GetFiveDayForecastData(Location location)
        {
            string forecastUrl = string.Format(Constants.AccuWeather.FiveDayForecastURLFormat, location.AccuWeatherLocationKey, PrivateConstants.AccuWeatherAPIKey);
            Task<WebResponse> forecastResponseTask = WebRequest.CreateHttp(forecastUrl).GetResponseAsync();

            WebResponse forecastResponse = await forecastResponseTask;
            using (SqlConnection dbConnection = new SqlConnection(GetDatabaseConnectionString()))
            using (StreamReader stream = new StreamReader(forecastResponse.GetResponseStream()))
            {
                dbConnection.Open();

                string jsonText = stream.ReadToEnd();
                FiveDayForecast fiveDayForecast = JsonConvert.DeserializeObject<FiveDayForecast>(jsonText);

                int daysBefore = 0;
                foreach (Forecast forecast in fiveDayForecast.DailyForecasts)
                {
                    WeatherData dayData = new WeatherData(forecast.Temperature.Maximum.Value,
                        forecast.Temperature.Minimum.Value,
                        forecast.Day.Rain.Value + forecast.Day.Snow.Value + forecast.Day.Ice.Value,
                        forecast.Day.CloudCover,
                        (int)Math.Round((double)forecast.Day.HoursOfPrecipitation, 0),
                        forecast.Day.Wind.Speed.Value,
                        forecast.Day.Wind.Direction.Degrees,
                        forecast.Day.WindGust.Speed.Value);

                    WeatherData nightData = new WeatherData(forecast.Temperature.Maximum.Value,
                        forecast.Temperature.Minimum.Value,
                        forecast.Night.Rain.Value + forecast.Night.Snow.Value + forecast.Night.Ice.Value,
                        forecast.Night.CloudCover,
                        (int)Math.Round((double)forecast.Night.HoursOfPrecipitation, 0),
                        forecast.Night.Wind.Speed.Value,
                        forecast.Night.Wind.Direction.Degrees,
                        forecast.Night.WindGust.Speed.Value);

                    InsertWeatherData(dbConnection, location.ZipCode, NormalizeDate(forecast.Date), daysBefore, dayData, nightData);
                    daysBefore++;
                }
            }
        }

        static void InsertWeatherData(SqlConnection dbConnection, int locationZipCode, DateTime targetDate, int numDaysBeforeTarget, WeatherData daytimeData, WeatherData nightData)
        {
            try
            {
                string queryStringFormat = "INSERT INTO dbo.WeatherData (zipCode, targetDate, daysBefore, high, low, precipitationAmount, avgCloudCover, hoursOfPrecipitation, windSpeed, windDirection, windGustSpeed, isDayTime) " +
                    "VALUES ({0}, '{1}', {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, '{11}')";

                string daytimeDataQuery = string.Format(queryStringFormat,
                    locationZipCode,
                    targetDate,
                    numDaysBeforeTarget,
                    daytimeData.HighTemp,
                    daytimeData.LowTemp,
                    daytimeData.PrecipitationAmount,
                    daytimeData.AverageCloudCover,
                    daytimeData.NumHoursOfPrecipitation,
                    daytimeData.WindSpeed,
                    daytimeData.WindDirection,
                    daytimeData.WindGustSpeed,
                    true.ToString().ToUpper());
                SqlCommand daytimeCommand = new SqlCommand(daytimeDataQuery, dbConnection);
                int numRowsAffected = daytimeCommand.ExecuteNonQuery();
                Debug.Assert(numRowsAffected == 1);

                string nightDataQuery = string.Format(queryStringFormat,
                    locationZipCode,
                    targetDate,
                    numDaysBeforeTarget,
                    nightData.HighTemp,
                    nightData.LowTemp,
                    nightData.PrecipitationAmount,
                    nightData.AverageCloudCover,
                    nightData.NumHoursOfPrecipitation,
                    nightData.WindSpeed,
                    nightData.WindDirection,
                    nightData.WindGustSpeed,
                    false.ToString().ToUpper());
                SqlCommand nightCommand = new SqlCommand(nightDataQuery, dbConnection);
                numRowsAffected = nightCommand.ExecuteNonQuery();
                Debug.Assert(numRowsAffected == 1);
            }
            catch (Exception e)
            {
                WriteOutput($"Failed to insert weather data for {targetDate.ToShortDateString()} ({numDaysBeforeTarget} days before) with error: \n\t{e}");
            }
        }

        /// <summary>
        /// Returns a date with just the year, month, and day of the given date parameter
        /// </summary>
        static DateTime NormalizeDate(DateTime date)
        {
            return new DateTime(date.Year, date.Month, date.Day);
        }

        static WeatherData GetWeatherData(WeatherObservation[] observations, bool getDaytimeData, int highTemp, int lowTemp)
        {
            float precipitation = 0;

            int numMatchingHours = 0;
            int numPrecipHours = 0;
            float windGustMax = 0;

            // These are sums of the values for all hours, which we'll use to get an average across all hours.
            float windSpeedTotal = 0;
            float windDirectionTotal = 0;
            float cloudCoverTotal = 0;
            foreach (WeatherObservation observation in observations)
            {
                if (observation.IsDayTime == getDaytimeData)
                {
                    numMatchingHours++;

                    if (observation.PrecipitationSummary.PastHour.Imperial.Value > 0)
                    {
                        numPrecipHours++;
                    }

                    if (observation.WindGust.Speed.Imperial.Value > windGustMax)
                    {
                        windGustMax = observation.WindGust.Speed.Imperial.Value;
                    }

                    precipitation += observation.PrecipitationSummary.PastHour.Imperial.Value;

                    windSpeedTotal += observation.Wind.Speed.Imperial.Value;
                    windDirectionTotal += observation.Wind.Direction.Degrees;
                    cloudCoverTotal += observation.CloudCover;
                }
            }

            float windSpeedAvg = windSpeedTotal / numMatchingHours;
            float windDirectionAvg = windDirectionTotal / numMatchingHours;
            float cloudCoverAverage = cloudCoverTotal / numMatchingHours;

            return new WeatherData(highTemp, lowTemp, precipitation, cloudCoverAverage, numPrecipHours, windSpeedAvg, windDirectionAvg, windGustMax);
        }

        /// <summary>
        /// Writes the specified message to both Debug and Console output.
        /// Debug output is nice when testing local changes.
        /// Console output will show up in the Azure logs for official runs.
        /// </summary>
        static void WriteOutput(string message)
        {
            Debug.WriteLine(message);
            Console.WriteLine(message);
        }

        static string GetDatabaseConnectionString()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();

            builder.DataSource = PrivateConstants.DatabaseConnection;
            builder.InitialCatalog = PrivateConstants.DatabaseName;

            #if DEBUG
                // The local SQL server instance for testing is set up to use Windows Authentication
                builder.IntegratedSecurity = true;
            #else
                builder.UserID = PrivateConstants.DatabaseUserId;
                builder.Password = PrivateConstants.DatabasePassword;
            #endif

            return builder.ConnectionString;
        }
    }
}
